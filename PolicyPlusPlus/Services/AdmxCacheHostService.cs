using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using PolicyPlusPlus.Logging; // logging

namespace PolicyPlusPlus.Services
{
    // Orchestrates the ADMX cache lifecycle for the UI: initialize, scan, and watch for changes.
    internal sealed class AdmxCacheHostService
    {
        public static AdmxCacheHostService Instance { get; } = new();

        private readonly IAdmxCache _cache;
        private AdmxWatcher? _watcher;
        private bool _started;
        private readonly object _gate = new();
        private readonly List<Task> _running = new();
        private IReadOnlyList<string>? _culturesForScan;
        private DateTime _lastWatcherKickUtc = DateTime.MinValue;
        private readonly TimeSpan _watcherDebounce = TimeSpan.FromSeconds(2);

        // Ensures cache initialization is awaited by any rebuild triggers that may arrive early.
        private Task? _initTask;

        // Rebuild serialization & coalescing
        private int _rebuildBusy; // 0 = idle, 1 = busy
        private volatile bool _rebuildPending;

        // Tracks active rebuild operations to allow the UI to fall back while building
        private int _rebuildActive; // >0 means building

        private AdmxCacheHostService()
        {
            _cache = new AdmxCache();
        }

        private const string LogArea = "AdmxCacheHost";

        private static void InvalidateLocalizedTextCache()
        {
            try
            {
                LocalizedTextService.ClearCache();
            }
            catch (Exception ex)
            {
                // Log at Debug level if cache invalidation fails, per logging guidance.
                Log.Debug(LogArea, "Failed to clear localized text cache", ex);
            }
        }

        // Ensures the core cache is initialized; if a previous init failed or was canceled, reattempt.
        private async Task EnsureInitializedAsync()
        {
            Task? init;
            lock (_gate)
            {
                init = _initTask;
            }
            if (init == null)
            {
                try
                {
                    _initTask = _cache.InitializeAsync();
                    init = _initTask;
                    Log.Debug(LogArea, "Init task created");
                }
                catch (Exception ex)
                {
                    Log.Warn(LogArea, "Initial cache InitializeAsync threw", ex);
                }
            }
            if (init != null)
            {
                try
                {
                    await init.ConfigureAwait(false);
                }
                catch (Exception ex1)
                {
                    Log.Warn(LogArea, "Init task failed, retrying once", ex1);
                    try
                    {
                        _initTask = _cache.InitializeAsync();
                        await _initTask.ConfigureAwait(false);
                        Log.Info(LogArea, "Init retry succeeded");
                    }
                    catch (Exception ex2)
                    {
                        Log.Error(LogArea, "Init retry failed", ex2);
                    }
                }
            }
        }

        public IAdmxCache Cache => _cache;
        public bool IsRebuilding => Volatile.Read(ref _rebuildActive) > 0;
        public bool IsStarted
        {
            get
            {
                lock (_gate)
                {
                    return _started;
                }
            }
        }

        public async Task StartAsync()
        {
            // Respect setting: allow disabling ADMX cache. Default is enabled.
            try
            {
                var st0 = SettingsService.Instance.LoadSettings();
                if ((st0.AdmxCacheEnabled ?? true) == false)
                {
                    Log.Info(LogArea, "Start skipped - disabled in settings");
                    return; // cache disabled by user settings
                }
            }
            catch (Exception ex)
            {
                Log.Warn(LogArea, "Failed to read settings to decide enablement", ex);
            }
            lock (_gate)
            {
                if (_started)
                    return;
                _started = true;
            }
            Log.Info(LogArea, "StartAsync begin");

            try
            {
                // Kick off initialization and record the task so early triggers can await it.
                _initTask = _cache.InitializeAsync();

                // Wire up event triggers BEFORE any heavy work so we don't miss early cache-cleared signals.
                try
                {
                    SettingsService.Instance.LanguagesChanged += () =>
                    {
                        bool shouldRun;
                        lock (_gate)
                        {
                            shouldRun = _started;
                        }
                        if (!shouldRun)
                            return;
                        Log.Debug(LogArea, "LanguagesChanged trigger");
                        RunAndTrack(async () =>
                        {
                            Interlocked.Increment(ref _rebuildActive);
                            try
                            {
                                await EnsureInitializedAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn(LogArea, "EnsureInitialized failed (languages)", ex);
                            }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildStarted("languages", null);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish start(languages) failed: " + ex.Message
                                );
                            }
                            try
                            {
                                SettingsService.Instance.PurgeOldCacheEntries(
                                    TimeSpan.FromDays(30)
                                );
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(LogArea, "PurgeOldCacheEntries failed: " + ex.Message);
                            }
                            var st2 = SettingsService.Instance.LoadSettings();
                            try
                            {
                                _cache.SetSourceRoot(
                                    string.IsNullOrWhiteSpace(st2.AdmxSourcePath)
                                        ? null
                                        : st2.AdmxSourcePath
                                );
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "SetSourceRoot failed (languages): " + ex.Message
                                );
                            }
                            string primary2 = !string.IsNullOrWhiteSpace(st2.Language)
                                ? st2.Language!
                                : CultureInfo.CurrentUICulture.Name;
                            var cultures2 = new List<string>(4);
                            if (
                                st2.SecondLanguageEnabled == true
                                && !string.IsNullOrWhiteSpace(st2.SecondLanguage)
                            )
                                cultures2.Add(st2.SecondLanguage!);
                            cultures2.Add(primary2);
                            // Insert OS UI culture if different from primary (to allow primary fallback chain: selected -> OS -> en-US)
                            var os2 = CultureInfo.CurrentUICulture.Name;
                            if (
                                !string.IsNullOrWhiteSpace(os2)
                                && !os2.Equals(primary2, StringComparison.OrdinalIgnoreCase)
                            )
                                cultures2.Add(os2);
                            if (st2.PrimaryLanguageFallbackEnabled == true)
                                cultures2.Add("en-US");
                            var ordered2 = new List<string>(cultures2.Count);
                            var seen2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var c in cultures2)
                                if (seen2.Add(c))
                                    ordered2.Add(c);
                            _culturesForScan = ordered2;
                            await _cache.ScanAndUpdateAsync(_culturesForScan).ConfigureAwait(false);
                            InvalidateLocalizedTextCache();
                            try
                            {
                                EventHub.PublishPolicySourcesRefreshed(null);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish sources refreshed (languages) failed: " + ex.Message
                                );
                            }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildCompleted("languages");
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish completed(languages) failed: " + ex.Message
                                );
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _rebuildActive);
                            }
                        });
                    };
                    SettingsService.Instance.SourcesRootChanged += () =>
                    {
                        bool shouldRun;
                        lock (_gate)
                        {
                            shouldRun = _started;
                        }
                        if (!shouldRun)
                            return;
                        Log.Debug(LogArea, "SourcesRootChanged trigger");
                        RunAndTrack(async () =>
                        {
                            Interlocked.Increment(ref _rebuildActive);
                            try
                            {
                                await EnsureInitializedAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn(LogArea, "EnsureInitialized failed (sourcesRoot)", ex);
                            }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildStarted("sourcesRoot", null);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish start(sourcesRoot) failed: " + ex.Message
                                );
                            }
                            try
                            {
                                SettingsService.Instance.PurgeOldCacheEntries(
                                    TimeSpan.FromDays(30)
                                );
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(LogArea, "PurgeOldCacheEntries failed: " + ex.Message);
                            }
                            var st3 = SettingsService.Instance.LoadSettings();
                            try
                            {
                                _cache.SetSourceRoot(
                                    string.IsNullOrWhiteSpace(st3.AdmxSourcePath)
                                        ? null
                                        : st3.AdmxSourcePath
                                );
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "SetSourceRoot failed (sourcesRoot): " + ex.Message
                                );
                            }
                            var langs = _culturesForScan;
                            if (langs == null || langs.Count == 0)
                                langs = new[] { CultureInfo.CurrentUICulture.Name };
                            await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                            InvalidateLocalizedTextCache();
                            try
                            {
                                EventHub.PublishPolicySourcesRefreshed(null);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish sources refreshed (sourcesRoot) failed: " + ex.Message
                                );
                            }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildCompleted("sourcesRoot");
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish completed(sourcesRoot) failed: " + ex.Message
                                );
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _rebuildActive);
                            }
                        });
                    };
                    SettingsService.Instance.CacheCleared += () =>
                    {
                        bool shouldRun;
                        lock (_gate)
                        {
                            shouldRun = _started;
                        }
                        if (!shouldRun)
                            return;
                        Log.Debug(LogArea, "CacheCleared trigger");
                        RunAndTrack(async () =>
                        {
                            Interlocked.Increment(ref _rebuildActive);
                            try
                            {
                                await EnsureInitializedAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn(LogArea, "EnsureInitialized failed (cacheCleared)", ex);
                            }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildStarted("cacheCleared", null);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish start(cacheCleared) failed: " + ex.Message
                                );
                            }
                            var st4 = SettingsService.Instance.LoadSettings();
                            try
                            {
                                _cache.SetSourceRoot(
                                    string.IsNullOrWhiteSpace(st4.AdmxSourcePath)
                                        ? null
                                        : st4.AdmxSourcePath
                                );
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "SetSourceRoot failed (cacheCleared): " + ex.Message
                                );
                            }
                            var langs = _culturesForScan;
                            if (langs == null || langs.Count == 0)
                                langs = new[] { CultureInfo.CurrentUICulture.Name };
                            await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                            InvalidateLocalizedTextCache();
                            try
                            {
                                EventHub.PublishPolicySourcesRefreshed(null);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish sources refreshed (cacheCleared) failed: " + ex.Message
                                );
                            }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildCompleted("cacheCleared");
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(
                                    LogArea,
                                    "Publish completed(cacheCleared) failed: " + ex.Message
                                );
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _rebuildActive);
                            }
                        });
                    };
                }
                catch (Exception ex)
                {
                    Log.Error(LogArea, "Failed wiring settings event handlers", ex);
                }

                // Wait for initialization to complete before first scan
                try
                {
                    await EnsureInitializedAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warn(LogArea, "EnsureInitialized before first scan failed", ex);
                }

                // Configure source root and build cultures list: primary + (2nd if enabled) + en-US if fallback enabled
                var st = SettingsService.Instance.LoadSettings();
                var src = st.AdmxSourcePath;
                try
                {
                    _cache.SetSourceRoot(string.IsNullOrWhiteSpace(src) ? null : src);
                }
                catch (Exception ex)
                {
                    Log.Debug(LogArea, "SetSourceRoot initial failed: " + ex.Message);
                }
                string primary = !string.IsNullOrWhiteSpace(st.Language)
                    ? st.Language!
                    : CultureInfo.CurrentUICulture.Name;
                var cultures = new List<string>(4);
                if (
                    st.SecondLanguageEnabled == true
                    && !string.IsNullOrWhiteSpace(st.SecondLanguage)
                )
                    cultures.Add(st.SecondLanguage!);
                cultures.Add(primary);
                var os = CultureInfo.CurrentUICulture.Name;
                if (
                    !string.IsNullOrWhiteSpace(os)
                    && !os.Equals(primary, StringComparison.OrdinalIgnoreCase)
                )
                    cultures.Add(os);
                if (st.PrimaryLanguageFallbackEnabled == true)
                    cultures.Add("en-US");
                var ordered = new List<string>(cultures.Count);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in cultures)
                    if (seen.Add(c))
                        ordered.Add(c);
                _culturesForScan = ordered;
                // Kick off the initial scan in the background to avoid any chance of UI contention.
                RunAndTrack(async () =>
                {
                    Interlocked.Increment(ref _rebuildActive);
                    try
                    {
                        await EnsureInitializedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(LogArea, "EnsureInitialized failed (initial)", ex);
                    }
                    try
                    {
                        EventHub.PublishAdmxCacheRebuildStarted("initial", null);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(LogArea, "Publish start(initial) failed: " + ex.Message);
                    }
                    await _cache.ScanAndUpdateAsync(_culturesForScan).ConfigureAwait(false);
                    InvalidateLocalizedTextCache();
                    try
                    {
                        EventHub.PublishAdmxCacheRebuildCompleted("initial");
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(LogArea, "Publish completed(initial) failed: " + ex.Message);
                    }
                    try
                    {
                        EventHub.PublishPolicySourcesRefreshed(null);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(
                            LogArea,
                            "Publish sources refreshed (initial) failed: " + ex.Message
                        );
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _rebuildActive);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(LogArea, "StartAsync outer failed - continuing without cache", ex);
            }

            // Attach file system watcher for configured policy definitions root.
            try
            {
                var current = SettingsService.Instance.LoadSettings();
                var watchRoot = current?.AdmxSourcePath;
                if (string.IsNullOrWhiteSpace(watchRoot))
                    watchRoot = Environment.ExpandEnvironmentVariables(
                        @"%WINDIR%\PolicyDefinitions"
                    );
                if (Directory.Exists(watchRoot))
                {
                    Log.Info(LogArea, $"Creating watcher root={watchRoot}");
                    _watcher = new AdmxWatcher(
                        watchRoot,
                        async _ =>
                        {
                            var now = DateTime.UtcNow;
                            if ((now - _lastWatcherKickUtc) < _watcherDebounce)
                            {
                                Log.Debug(LogArea, "Watcher debounce");
                                try
                                {
                                    await Task.Delay(_watcherDebounce).ConfigureAwait(false);
                                }
                                catch (Exception exDelay)
                                {
                                    Log.Debug(LogArea, "Watcher delay failed: " + exDelay.Message);
                                }
                            }
                            _lastWatcherKickUtc = DateTime.UtcNow;
                            Log.Debug(LogArea, "Watcher change trigger");
                            RunAndTrack(async () =>
                            {
                                Interlocked.Increment(ref _rebuildActive);
                                try
                                {
                                    await EnsureInitializedAsync().ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    Log.Warn(LogArea, "EnsureInitialized failed (watcher)", ex);
                                }
                                try
                                {
                                    EventHub.PublishAdmxCacheRebuildStarted("watcher", _);
                                }
                                catch (Exception ex)
                                {
                                    Log.Debug(
                                        LogArea,
                                        "Publish start(watcher) failed: " + ex.Message
                                    );
                                }
                                var langs = _culturesForScan;
                                if (langs == null || langs.Count == 0)
                                {
                                    langs = new[] { CultureInfo.CurrentUICulture.Name };
                                }
                                await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                                InvalidateLocalizedTextCache();
                                try
                                {
                                    EventHub.PublishPolicySourcesRefreshed(null);
                                }
                                catch (Exception ex)
                                {
                                    Log.Debug(
                                        LogArea,
                                        "Publish sources refreshed (watcher) failed: " + ex.Message
                                    );
                                }
                                try
                                {
                                    EventHub.PublishAdmxCacheRebuildCompleted("watcher");
                                }
                                catch (Exception ex)
                                {
                                    Log.Debug(
                                        LogArea,
                                        "Publish completed(watcher) failed: " + ex.Message
                                    );
                                }
                                finally
                                {
                                    Interlocked.Decrement(ref _rebuildActive);
                                }
                            });
                            await Task.Yield();
                        }
                    );
                }
                else
                {
                    Log.Debug(LogArea, $"Watcher root not found path={watchRoot}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn(LogArea, "Watcher setup failed (optional)", ex);
            }
        }

        public async Task StopAsync()
        {
            AdmxWatcher? w;
            Task[] pending;
            lock (_gate)
            {
                w = _watcher;
                _watcher = null;
                _started = false;
                pending = _running.ToArray();
                _running.Clear();
            }
            if (w != null)
            {
                try
                {
                    await w.DisposeAsync();
                    Log.Debug(LogArea, "Watcher disposed");
                }
                catch (Exception ex)
                {
                    Log.Debug(LogArea, "Watcher dispose failed: " + ex.Message);
                }
            }
            if (pending.Length > 0)
            {
                try
                {
                    await Task.WhenAll(pending);
                }
                catch (Exception ex)
                {
                    Log.Debug(LogArea, "Waiting pending tasks failed: " + ex.Message);
                }
            }
            try
            {
                PolicyPlusCore.Core.AdmxCacheRuntime.ReleaseSqliteHandles();
            }
            catch (Exception ex)
            {
                Log.Debug(LogArea, "ReleaseSqliteHandles failed: " + ex.Message);
            }
        }

        // Tracks background work to allow StopAsync to wait for DB-using operations to complete.
        private void RunAndTrack(Func<Task> work)
        {
            // Coalesce: if a rebuild is already in progress, queue a single rerun and return.
            if (System.Threading.Interlocked.CompareExchange(ref _rebuildBusy, 1, 0) != 0)
            {
                _rebuildPending = true;
                return;
            }
            // Run heavy cache work on a dedicated thread to reduce thread pool contention with the UI.
            Task t = Task
                .Factory.StartNew(
                    async () =>
                    {
                        try
                        {
                            System.Threading.Thread.CurrentThread.Priority = System
                                .Threading
                                .ThreadPriority
                                .BelowNormal;
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(LogArea, "Set priority failed: " + ex.Message);
                        }
                        try
                        {
                            await work().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(LogArea, "Background rebuild work failed", ex);
                        }
                        finally
                        {
                            System.Threading.Interlocked.Exchange(ref _rebuildBusy, 0);
                            // If a rebuild was requested while busy, run once more.
                            if (_rebuildPending)
                            {
                                _rebuildPending = false;
                                Log.Debug(LogArea, "Running coalesced rebuild");
                                RunAndTrack(work);
                            }
                        }
                    },
                    System.Threading.CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default
                )
                .Unwrap();
            lock (_gate)
            {
                _running.Add(t);
            }
            _ = t.ContinueWith(
                _ =>
                {
                    lock (_gate)
                    {
                        _running.Remove(t);
                    }
                },
                TaskScheduler.Default
            );
        }

        // Allows explicit rebuild requests with a custom reason (e.g., fallback after cache clear if event was missed).
        public Task RequestRebuildAsync(string reason)
        {
            bool shouldRun;
            lock (_gate)
            {
                shouldRun = _started;
            }
            if (!shouldRun)
            {
                return Task.CompletedTask;
            }
            RunAndTrack(async () =>
            {
                Interlocked.Increment(ref _rebuildActive);
                try
                {
                    if (_initTask != null)
                        await _initTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warn(LogArea, "EnsureInitialized failed (requestRebuild)", ex);
                }
                try
                {
                    EventHub.PublishAdmxCacheRebuildStarted(reason, null);
                }
                catch (Exception ex)
                {
                    Log.Debug(LogArea, "Publish start(request) failed: " + ex.Message);
                }
                var langs = _culturesForScan;
                if (langs == null || langs.Count == 0)
                {
                    langs = new[] { CultureInfo.CurrentUICulture.Name };
                }
                await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                InvalidateLocalizedTextCache();
                try
                {
                    EventHub.PublishPolicySourcesRefreshed(null);
                }
                catch (Exception ex)
                {
                    Log.Debug(LogArea, "Publish sources refreshed (request) failed: " + ex.Message);
                }
                try
                {
                    EventHub.PublishAdmxCacheRebuildCompleted(reason);
                }
                catch (Exception ex)
                {
                    Log.Debug(LogArea, "Publish completed(request) failed: " + ex.Message);
                }
                finally
                {
                    Interlocked.Decrement(ref _rebuildActive);
                }
            });
            return Task.CompletedTask;
        }
    }
}
