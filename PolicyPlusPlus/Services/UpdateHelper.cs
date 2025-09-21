using System;
using System.Threading.Tasks;
#if USE_STORE_UPDATE
using Windows.System;
#endif
#if USE_VELOPACK
using Velopack;
using Velopack.Sources;
using PolicyPlusPlus.Logging;
#endif

namespace PolicyPlusPlus.Services
{
    internal static class UpdateHelper
    {
        internal enum VelopackUpdateApplyChoice
        {
            RestartNow,
            OnExit,
            Cancel,
        }

#if USE_VELOPACK
        private static UpdateManager? _updateManager;
        private static UpdateInfo? _pendingUpdates; // holds update info instance between check and apply
        private static bool _restartPending; // set after successful download/apply requiring restart
        private static bool _deferredInstall; // true when user chose apply-on-exit path
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

        public static bool IsRestartPending =>
#if USE_VELOPACK
            _restartPending;
#else
            false;
#endif

        public static bool IsDeferredInstall =>
#if USE_VELOPACK
            _deferredInstall;
#else
            false;
#endif

        public static void InitializeIfNeeded()
        {
#if USE_VELOPACK
            if (_updateManager != null)
                return;
            try
            {
                _updateManager = new UpdateManager(
                    new GithubSource(UpdateConfig.VelopackUpdateUrl, null, false)
                );
            }
            catch (Exception ex)
            {
                Log.Debug(
                    "Update",
                    $"Velopack init probing failed: {ex.GetType().Name} {ex.Message}"
                );
            }
#endif
        }

        public static async Task<(
            bool ok,
            bool hasUpdate,
            string? message
        )> CheckVelopackUpdatesAsync()
        {
#if USE_VELOPACK
            try
            {
                InitializeIfNeeded();
                if (_updateManager == null)
                    return (false, false, "Update manager not available");

                // If restart already pending via immediate path, treat as no new updates.
                if (_restartPending && !_deferredInstall)
                    return (true, false, "Update already downloaded. Restart to apply.");

                // If user selected deferred (apply-on-exit) previously, advise manual exit/restart.
                if (_restartPending && _deferredInstall)
                    return (
                        true,
                        false,
                        "Update has been downloaded. Exit and restart the application to complete installation."
                    );

                _pendingUpdates = null;
                var updates = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (updates == null)
                    return (true, false, "No updates available");

                try
                {
                    var prop =
                        updates.GetType().GetProperty("Updates") ?? updates
                            .GetType()
                            .GetProperty("ReleasesToApply");
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

        public static async Task<(
            bool ok,
            bool restart,
            string? message
        )> ApplyVelopackUpdatesAsync()
        {
#if USE_VELOPACK
            try
            {
                InitializeIfNeeded();
                if (_updateManager == null)
                    return (false, false, "Update manager not available");
                if (_pendingUpdates == null)
                    return (false, false, "No pending update");
                if (_restartPending)
                    return (
                        true,
                        !_deferredInstall,
                        _deferredInstall
                            ? "Update has been downloaded. Exit and restart the application to complete installation."
                            : "Restart required"
                    );

                var (ok, restartInitiated, message) = await ApplyVelopackPendingAsync(
                        VelopackUpdateApplyChoice.RestartNow
                    )
                    .ConfigureAwait(false);
                return (ok, restartInitiated, message);
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

        public static async Task<(
            bool ok,
            bool restart,
            string? message
        )> CheckAndApplyVelopackUpdatesAsync()
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

#if USE_VELOPACK
        // Applies the currently pending update according to user choice.
        public static async Task<(
            bool ok,
            bool restartInitiated,
            string? message
        )> ApplyVelopackPendingAsync(VelopackUpdateApplyChoice choice)
        {
            try
            {
                InitializeIfNeeded();
                if (_updateManager == null)
                    return (false, false, "Update manager not available");
                if (_pendingUpdates == null)
                {
                    if (choice == VelopackUpdateApplyChoice.Cancel)
                        return (true, false, "Cancelled");
                    return (false, false, "No pending update");
                }
                if (choice == VelopackUpdateApplyChoice.Cancel)
                {
                    _pendingUpdates = null; // discard
                    return (true, false, "Cancelled");
                }

                if (_restartPending)
                {
                    if (_deferredInstall)
                        return (
                            true,
                            false,
                            "Update has been downloaded. Exit and restart the application to complete installation."
                        );
                    return (true, true, "Restart required");
                }

                var updates = _pendingUpdates;
                await _updateManager.DownloadUpdatesAsync(updates).ConfigureAwait(false);
                bool restartInitiated = false;
                string message;
                if (choice == VelopackUpdateApplyChoice.RestartNow)
                {
                    // Attempt immediate restart; control might not return if restart proceeds quickly.
                    _updateManager.ApplyUpdatesAndRestart(updates);
                    restartInitiated = true;
                    message = "Restarting to apply update...";
                }
                else // OnExit
                {
                    await _updateManager
                        .WaitExitThenApplyUpdatesAsync(updates, false, false)
                        .ConfigureAwait(false);
                    restartInitiated = false;
                    message =
                        "Update will be applied on exit. Please close and restart the application.";
                }
                _restartPending = true;
                _deferredInstall = choice == VelopackUpdateApplyChoice.OnExit;

                return (true, restartInitiated, message);
            }
            catch (Exception ex)
            {
                return (false, false, ex.Message);
            }
        }
#else
        // Stub so callers compile when Velopack is excluded.
        public static Task<(
            bool ok,
            bool restartInitiated,
            string? message
        )> ApplyVelopackPendingAsync(VelopackUpdateApplyChoice choice) =>
            Task.FromResult<(bool, bool, string?)>((false, false, "Velopack not included"));
#endif

        public static string? GetPendingUpdateNotes()
        {
#if USE_VELOPACK
            if (_pendingUpdates == null)
                return null;
            try
            {
                var t = _pendingUpdates.GetType();
                var notesProp =
                    t.GetProperty("ReleaseNotes")
                    ?? t.GetProperty("Changelog")
                    ?? t.GetProperty("Notes");
                if (notesProp != null)
                {
                    var val = notesProp.GetValue(_pendingUpdates) as string;
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
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
                var uri = new Uri(
                    $"ms-windows-store://pdp/?ProductId={UpdateConfig.StoreProductId}"
                );
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
