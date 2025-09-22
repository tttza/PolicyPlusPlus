using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PolicyPlusCore.Core;

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
                }
                catch
                { /* swallow */
                }
            }
            if (init != null)
            {
                try
                {
                    await init.ConfigureAwait(false);
                }
                catch
                {
                    // Retry once on failure
                    try
                    {
                        _initTask = _cache.InitializeAsync();
                        await _initTask.ConfigureAwait(false);
                    }
                    catch { }
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
                    return; // cache disabled by user settings
                }
            }
            catch { }
            lock (_gate)
            {
                if (_started)
                    return;
                _started = true;
            }

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
                        RunAndTrack(async () =>
                        {
                            Interlocked.Increment(ref _rebuildActive);
                            try
                            {
                                await EnsureInitializedAsync().ConfigureAwait(false);
                            }
                            catch { }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildStarted("languages", null);
                            }
                            catch { }
                            try
                            {
                                SettingsService.Instance.PurgeOldCacheEntries(
                                    TimeSpan.FromDays(30)
                                );
                            }
                            catch { }
                            var st2 = SettingsService.Instance.LoadSettings();
                            try
                            {
                                _cache.SetSourceRoot(
                                    string.IsNullOrWhiteSpace(st2.AdmxSourcePath)
                                        ? null
                                        : st2.AdmxSourcePath
                                );
                            }
                            catch { }
                            string primary2 = !string.IsNullOrWhiteSpace(st2.Language)
                                ? st2.Language!
                                : CultureInfo.CurrentUICulture.Name;
                            var cultures2 = new List<string>(3);
                            if (
                                st2.SecondLanguageEnabled == true
                                && !string.IsNullOrWhiteSpace(st2.SecondLanguage)
                            )
                                cultures2.Add(st2.SecondLanguage!);
                            cultures2.Add(primary2);
                            if (st2.PrimaryLanguageFallbackEnabled == true)
                                cultures2.Add("en-US");
                            var ordered2 = new List<string>(cultures2.Count);
                            var seen2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var c in cultures2)
                                if (seen2.Add(c))
                                    ordered2.Add(c);
                            _culturesForScan = ordered2;
                            await _cache.ScanAndUpdateAsync(_culturesForScan).ConfigureAwait(false);
                            try
                            {
                                EventHub.PublishPolicySourcesRefreshed(null);
                            }
                            catch { }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildCompleted("languages");
                            }
                            catch { }
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
                        RunAndTrack(async () =>
                        {
                            Interlocked.Increment(ref _rebuildActive);
                            try
                            {
                                await EnsureInitializedAsync().ConfigureAwait(false);
                            }
                            catch { }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildStarted("sourcesRoot", null);
                            }
                            catch { }
                            try
                            {
                                SettingsService.Instance.PurgeOldCacheEntries(
                                    TimeSpan.FromDays(30)
                                );
                            }
                            catch { }
                            var st3 = SettingsService.Instance.LoadSettings();
                            try
                            {
                                _cache.SetSourceRoot(
                                    string.IsNullOrWhiteSpace(st3.AdmxSourcePath)
                                        ? null
                                        : st3.AdmxSourcePath
                                );
                            }
                            catch { }
                            var langs = _culturesForScan;
                            if (langs == null || langs.Count == 0)
                                langs = new[] { CultureInfo.CurrentUICulture.Name };
                            await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                            try
                            {
                                EventHub.PublishPolicySourcesRefreshed(null);
                            }
                            catch { }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildCompleted("sourcesRoot");
                            }
                            catch { }
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
                        RunAndTrack(async () =>
                        {
                            Interlocked.Increment(ref _rebuildActive);
                            try
                            {
                                await EnsureInitializedAsync().ConfigureAwait(false);
                            }
                            catch { }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildStarted("cacheCleared", null);
                            }
                            catch { }
                            var st4 = SettingsService.Instance.LoadSettings();
                            try
                            {
                                _cache.SetSourceRoot(
                                    string.IsNullOrWhiteSpace(st4.AdmxSourcePath)
                                        ? null
                                        : st4.AdmxSourcePath
                                );
                            }
                            catch { }
                            var langs = _culturesForScan;
                            if (langs == null || langs.Count == 0)
                                langs = new[] { CultureInfo.CurrentUICulture.Name };
                            await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                            try
                            {
                                EventHub.PublishPolicySourcesRefreshed(null);
                            }
                            catch { }
                            try
                            {
                                EventHub.PublishAdmxCacheRebuildCompleted("cacheCleared");
                            }
                            catch { }
                            finally
                            {
                                Interlocked.Decrement(ref _rebuildActive);
                            }
                        });
                    };
                }
                catch { }

                // Wait for initialization to complete before first scan
                try
                {
                    await EnsureInitializedAsync().ConfigureAwait(false);
                }
                catch { }

                // Configure source root and build cultures list: primary + (2nd if enabled) + en-US if fallback enabled
                var st = SettingsService.Instance.LoadSettings();
                var src = st.AdmxSourcePath;
                try
                {
                    _cache.SetSourceRoot(string.IsNullOrWhiteSpace(src) ? null : src);
                }
                catch { }
                string primary = !string.IsNullOrWhiteSpace(st.Language)
                    ? st.Language!
                    : CultureInfo.CurrentUICulture.Name;
                var cultures = new List<string>(3);
                if (
                    st.SecondLanguageEnabled == true
                    && !string.IsNullOrWhiteSpace(st.SecondLanguage)
                )
                    cultures.Add(st.SecondLanguage!);
                cultures.Add(primary);
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
                    catch { }
                    try
                    {
                        EventHub.PublishAdmxCacheRebuildStarted("initial", null);
                    }
                    catch { }
                    await _cache.ScanAndUpdateAsync(_culturesForScan).ConfigureAwait(false);
                    try
                    {
                        EventHub.PublishAdmxCacheRebuildCompleted("initial");
                    }
                    catch { }
                    try
                    {
                        EventHub.PublishPolicySourcesRefreshed(null);
                    }
                    catch { }
                    finally
                    {
                        Interlocked.Decrement(ref _rebuildActive);
                    }
                });
            }
            catch
            {
                // Ignore cache init failures; UI should continue to function.
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
                    _watcher = new AdmxWatcher(
                        watchRoot,
                        async _ =>
                        {
                            // Debounce rapid file change bursts
                            var now = DateTime.UtcNow;
                            if ((now - _lastWatcherKickUtc) < _watcherDebounce)
                            {
                                await Task.Delay(_watcherDebounce).ConfigureAwait(false);
                            }
                            _lastWatcherKickUtc = DateTime.UtcNow;
                            RunAndTrack(async () =>
                            {
                                Interlocked.Increment(ref _rebuildActive);
                                try
                                {
                                    await EnsureInitializedAsync().ConfigureAwait(false);
                                }
                                catch { }
                                try
                                {
                                    EventHub.PublishAdmxCacheRebuildStarted("watcher", _);
                                }
                                catch { }
                                var langs = _culturesForScan;
                                if (langs == null || langs.Count == 0)
                                {
                                    langs = new[] { CultureInfo.CurrentUICulture.Name };
                                }
                                await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                                try
                                {
                                    EventHub.PublishPolicySourcesRefreshed(null);
                                }
                                catch { }
                                try
                                {
                                    EventHub.PublishAdmxCacheRebuildCompleted("watcher");
                                }
                                catch { }
                                finally
                                {
                                    Interlocked.Decrement(ref _rebuildActive);
                                }
                            });
                            await Task.Yield();
                        }
                    );
                }
            }
            catch
            {
                // Best-effort; watcher is optional.
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
                }
                catch { }
            }
            if (pending.Length > 0)
            {
                try
                {
                    await Task.WhenAll(pending);
                }
                catch { }
            }
            try
            {
                PolicyPlusCore.Core.AdmxCacheRuntime.ReleaseSqliteHandles();
            }
            catch { }
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
                        catch { }
                        try
                        {
                            await work().ConfigureAwait(false);
                        }
                        catch { }
                        finally
                        {
                            System.Threading.Interlocked.Exchange(ref _rebuildBusy, 0);
                            // If a rebuild was requested while busy, run once more.
                            if (_rebuildPending)
                            {
                                _rebuildPending = false;
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
                catch { }
                try
                {
                    EventHub.PublishAdmxCacheRebuildStarted(reason, null);
                }
                catch { }
                var langs = _culturesForScan;
                if (langs == null || langs.Count == 0)
                {
                    langs = new[] { CultureInfo.CurrentUICulture.Name };
                }
                await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                try
                {
                    EventHub.PublishPolicySourcesRefreshed(null);
                }
                catch { }
                try
                {
                    EventHub.PublishAdmxCacheRebuildCompleted(reason);
                }
                catch { }
                finally
                {
                    Interlocked.Decrement(ref _rebuildActive);
                }
            });
            return Task.CompletedTask;
        }
    }
}
