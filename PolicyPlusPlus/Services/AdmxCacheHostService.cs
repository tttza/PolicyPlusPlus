using System;
using System.IO;
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
                await _cache.ScanAndUpdateAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore cache init failures; UI should continue to function.
            }

            // Attach file system watcher for local policy definitions.
            try
            {
                var localDefs = Environment.ExpandEnvironmentVariables(
                    @"%WINDIR%\PolicyDefinitions"
                );
                if (Directory.Exists(localDefs))
                {
                    _watcher = new AdmxWatcher(
                        localDefs,
                        async _ =>
                        {
                            try
                            {
                                await _cache.ScanAndUpdateAsync().ConfigureAwait(false);
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
