using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;

namespace PolicyPlus.WinUI3.Services
{
    internal static class SaveChangesCoordinator
    {
        public static async Task<(bool ok, string? error, string? compBase64, string? userBase64)> SaveAsync(
            AdmxBundle bundle,
            IEnumerable<PendingChange> changes,
            IElevationService elevation,
            TimeSpan timeout,
            bool triggerRefresh = true)
        {
            if (bundle == null) return (false, "no bundle", null, null);
            if (changes == null) return (true, null, null, null);

            // Build request set
            var req = changes.Select(c => new PolicyChangeRequest
            {
                PolicyId = c.PolicyId,
                Scope = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? PolicyTargetScope.User : PolicyTargetScope.Machine,
                DesiredState = c.DesiredState,
                Options = c.Options
            }).ToList();

            // Build POL payloads
            string? compBase64 = null; string? userBase64 = null;
            try
            {
                var b64 = PolicySavePipeline.BuildLocalGpoBase64(bundle, req);
                compBase64 = b64.machineBase64; userBase64 = b64.userBase64;
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null, null);
            }

            try
            {
                // Elevation call with timeout
                var callTask = elevation.WriteLocalGpoBytesAsync(compBase64, userBase64, triggerRefresh);
                var timeoutTask = Task.Delay(timeout);
                var completed = await Task.WhenAny(callTask, timeoutTask).ConfigureAwait(false);
                if (completed == timeoutTask)
                {
                    return (false, "elevation timeout", compBase64, userBase64);
                }
                var res = await callTask.ConfigureAwait(false);
                return (res.ok, res.error, compBase64, userBase64);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, compBase64, userBase64);
            }
        }
    }
}
