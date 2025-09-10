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

        public static async Task<(bool ok, bool restart, string? message)> CheckAndApplyVelopackUpdatesAsync()
        {
#if USE_VELOPACK
            try
            {
                InitializeIfNeeded();
                if (_updateManager == null)
                    return (false, false, "Update manager not available");

                var updates = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (updates == null)
                    return (true, false, "No updates available");

                await _updateManager.DownloadUpdatesAsync(updates).ConfigureAwait(false);
                await _updateManager.WaitExitThenApplyUpdatesAsync(updates).ConfigureAwait(false);
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
