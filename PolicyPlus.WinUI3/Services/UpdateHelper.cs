using System;
using System.Threading.Tasks;
using PolicyPlus.WinUI3.Logging;
#if USE_VELOPACK
using Velopack;
#endif
using Windows.System;

namespace PolicyPlus.WinUI3.Services
{
    internal static class UpdateHelper
    {
#if USE_VELOPACK
        private static UpdateManager? _updateManager;
        private static object? _pendingUpdates; // holds update info instance between check and apply
#endif
        public static bool IsVelopackAvailable =>
#if USE_VELOPACK
            true;
#else
            false;
#endif

        public static bool IsStoreBuild =>
#if USE_STORE_UPDATE
            true;
#else
            false;
#endif

        public static void InitializeIfNeeded()
        {
#if USE_VELOPACK
            if (_updateManager != null) return;
            try
            {
                _updateManager = new UpdateManager(UpdateConfig.VelopackUpdateUrl);
            }
            catch (Exception ex)
            {
                Log.Debug("Update", $"Velopack init probing failed: {ex.GetType().Name} {ex.Message}");
            }
#endif
        }

        public static async Task<(bool ok, bool hasUpdate, string? message)> CheckVelopackUpdatesAsync()
        {
#if USE_VELOPACK
            try
            {
                InitializeIfNeeded();
                if (_updateManager == null)
                    return (false, false, "Update manager not available");

                _pendingUpdates = null;
                var updates = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (updates == null)
                    return (true, false, "No updates available");

                try
                {
                    var prop = updates.GetType().GetProperty("Updates") ?? updates.GetType().GetProperty("ReleasesToApply");
                    if (prop != null)
                    {
                        var list = prop.GetValue(updates) as System.Collections.ICollection;
                        if (list == null || list.Count == 0)
                            return (true, false, "No updates available");
                    }
                }
                catch { }

                _pendingUpdates = updates;
                return (true, true, "Update available");
            }
            catch (Exception ex)
            {
                return (false, false, ex.Message);
            }
#else
            await Task.CompletedTask;
            return (false, false, "Velopack not included");
#endif
        }

        public static async Task<(bool ok, bool restart, string? message)> ApplyVelopackUpdatesAsync()
        {
#if USE_VELOPACK
            try
            {
                InitializeIfNeeded();
                if (_updateManager == null)
                    return (false, false, "Update manager not available");
                if (_pendingUpdates == null)
                    return (false, false, "No pending update");

                var updates = _pendingUpdates; // keep original runtime type
                await _updateManager.DownloadUpdatesAsync((dynamic)updates).ConfigureAwait(false);
                await _updateManager.WaitExitThenApplyUpdatesAsync((dynamic)updates, false, false).ConfigureAwait(false);
                _pendingUpdates = null;
                return (true, true, "Restart required");
            }
            catch (Exception ex)
            {
                return (false, false, ex.Message);
            }
#else
            await Task.CompletedTask;
            return (false, false, "Velopack not included");
#endif
        }

        public static async Task<(bool ok, bool restart, string? message)> CheckAndApplyVelopackUpdatesAsync()
        {
#if USE_VELOPACK
            var (ok, hasUpdate, message) = await CheckVelopackUpdatesAsync().ConfigureAwait(false);
            if (!ok)
                return (false, false, message);
            if (!hasUpdate)
                return (true, false, message ?? "No updates available");
            return await ApplyVelopackUpdatesAsync().ConfigureAwait(false);
#else
            await Task.CompletedTask;
            return (false, false, "Velopack not included");
#endif
        }

        public static string? GetPendingUpdateNotes()
        {
#if USE_VELOPACK
            if (_pendingUpdates == null) return null;
            try
            {
                var t = _pendingUpdates.GetType();
                var notesProp = t.GetProperty("ReleaseNotes") ?? t.GetProperty("Changelog") ?? t.GetProperty("Notes");
                if (notesProp != null)
                {
                    var val = notesProp.GetValue(_pendingUpdates) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
            catch { }
            return null;
#else
            return null;
#endif
        }

        public static async Task<(bool ok, string? message)> OpenStorePageAsync()
        {
#if USE_STORE_UPDATE
            try
            {
                var uri = new Uri($"ms-windows-store://pdp/?ProductId={UpdateConfig.StoreProductId}");
                bool launched = await Launcher.LaunchUriAsync(uri);
                return launched ? (true, null) : (false, "Launch declined");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
#else
            await Task.CompletedTask;
            return (false, "Store updates not enabled");
#endif
        }
    }
}
