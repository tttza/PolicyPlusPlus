using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
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
    private IReadOnlyList<string>? _culturesForScan;

        private AdmxCacheHostService()
        {
            _cache = new AdmxCache();
        }

        public IAdmxCache Cache => _cache;

        public async Task StartAsync()
        {
            lock (_gate)
            {
                if (_started)
                    return;
                _started = true;
            }

            try
            {
                await _cache.InitializeAsync().ConfigureAwait(false);

                // Configure source root and build cultures list: primary + (2nd if enabled) + en-US if fallback enabled
                var st = SettingsService.Instance.LoadSettings();
                var src = st.AdmxSourcePath;
                try { _cache.SetSourceRoot(string.IsNullOrWhiteSpace(src) ? null : src); } catch { }
                string primary = !string.IsNullOrWhiteSpace(st.Language)
                    ? st.Language!
                    : CultureInfo.CurrentUICulture.Name;
                var cultures = new List<string>(3);
                if (st.SecondLanguageEnabled == true && !string.IsNullOrWhiteSpace(st.SecondLanguage))
                {
                    cultures.Add(st.SecondLanguage!);
                }
                cultures.Add(primary);
                if (st.PrimaryLanguageFallbackEnabled == true)
                {
                    cultures.Add("en-US");
                }
                // De-duplicate while preserving order
                var ordered = new List<string>(cultures.Count);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in cultures)
                {
                    if (seen.Add(c)) ordered.Add(c);
                }
                _culturesForScan = ordered;
                await _cache.ScanAndUpdateAsync(_culturesForScan).ConfigureAwait(false);

                // React to runtime language preference changes to refresh cache as needed.
                try
                {
                    SettingsService.Instance.LanguagesChanged += async () =>
                    {
                        try
                        {
                            var st2 = SettingsService.Instance.LoadSettings();
                            // Update source root as it may have changed via settings UI
                            try { _cache.SetSourceRoot(string.IsNullOrWhiteSpace(st2.AdmxSourcePath) ? null : st2.AdmxSourcePath); } catch { }
                            string primary2 = !string.IsNullOrWhiteSpace(st2.Language)
                                ? st2.Language!
                                : CultureInfo.CurrentUICulture.Name;
                            var cultures2 = new List<string>(3);
                            if (st2.SecondLanguageEnabled == true && !string.IsNullOrWhiteSpace(st2.SecondLanguage))
                                cultures2.Add(st2.SecondLanguage!);
                            cultures2.Add(primary2);
                            if (st2.PrimaryLanguageFallbackEnabled == true)
                                cultures2.Add("en-US");
                            var ordered2 = new List<string>(cultures2.Count);
                            var seen2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var c in cultures2)
                                if (seen2.Add(c)) ordered2.Add(c);
                            _culturesForScan = ordered2;
                            await _cache.ScanAndUpdateAsync(_culturesForScan).ConfigureAwait(false);
                        }
                        catch { }
                        try { EventHub.PublishPolicySourcesRefreshed(null); } catch { }
                    };
                    SettingsService.Instance.SourcesRootChanged += async () =>
                    {
                        try
                        {
                            var st3 = SettingsService.Instance.LoadSettings();
                            try { _cache.SetSourceRoot(string.IsNullOrWhiteSpace(st3.AdmxSourcePath) ? null : st3.AdmxSourcePath); } catch { }
                            var langs = _culturesForScan;
                            if (langs == null || langs.Count == 0)
                                langs = new[] { CultureInfo.CurrentUICulture.Name };
                            await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                        }
                        catch { }
                        try { EventHub.PublishPolicySourcesRefreshed(null); } catch { }
                    };
                }
                catch { }
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
                    watchRoot = Environment.ExpandEnvironmentVariables(@"%WINDIR%\PolicyDefinitions");
                if (Directory.Exists(watchRoot))
                {
                    _watcher = new AdmxWatcher(
                        watchRoot,
                        async _ =>
                        {
                            try
                            {
                                var langs = _culturesForScan;
                                if (langs == null || langs.Count == 0)
                                {
                                    langs = new[] { CultureInfo.CurrentUICulture.Name };
                                }
                                await _cache.ScanAndUpdateAsync(langs).ConfigureAwait(false);
                            }
                            catch { }
                            // Notify UI that policy sources were refreshed (null => full refresh)
                            try
                            {
                                EventHub.PublishPolicySourcesRefreshed(null);
                            }
                            catch { }
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
            lock (_gate)
            {
                w = _watcher;
                _watcher = null;
                _started = false;
            }
            if (w != null)
            {
                try
                {
                    await w.DisposeAsync();
                }
                catch { }
            }
        }
    }
}
