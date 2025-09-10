using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;
using PolicyPlus.WinUI3.Logging; // logging

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
            string corr = Log.NewCorrelationId();
            if (bundle == null) { Log.Error("Save", $"{corr} bundle null"); return (false, "no bundle", null, null); }
            if (changes == null) { Log.Info("Save", $"{corr} no changes (null)"); return (true, null, null, null); }

            var changeList = changes.ToList();
            Log.Info("Save", $"{corr} start count={changeList.Count} timeoutMs={(int)timeout.TotalMilliseconds} refresh={triggerRefresh}");
            if (changeList.Count == 0) return (true, null, null, null);

            try
            {
                var req = changeList.Select(c => new PolicyChangeRequest
                {
                    PolicyId = c.PolicyId,
                    Scope = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? PolicyTargetScope.User : PolicyTargetScope.Machine,
                    DesiredState = c.DesiredState,
                    Options = c.Options
                }).ToList();

                string? compBase64 = null; string? userBase64 = null;
                try
                {
                    var b64 = PolicySavePipeline.BuildLocalGpoBase64(bundle, req);
                    compBase64 = b64.machineBase64; userBase64 = b64.userBase64;
                    Log.Debug("Save", $"{corr} built payload compLen={compBase64?.Length ?? 0} userLen={userBase64?.Length ?? 0}");
                }
                catch (Exception ex)
                {
                    Log.Error("Save", $"{corr} build payload failed", ex);
                    return (false, ex.Message, null, null);
                }

                try
                {
                    var callTask = elevation.WriteLocalGpoBytesAsync(compBase64, userBase64, triggerRefresh);
                    var timeoutTask = Task.Delay(timeout);
                    var completed = await Task.WhenAny(callTask, timeoutTask).ConfigureAwait(false);
                    if (completed == timeoutTask)
                    {
                        Log.Warn("Save", $"{corr} timeout after {(int)timeout.TotalMilliseconds}ms");
                        return (false, "elevation timeout", compBase64, userBase64);
                    }
                    var res = await callTask.ConfigureAwait(false);
                    if (!res.Ok)
                        Log.Error("Save", $"{corr} elevation error: {res.Error} code={res.Code}");
                    else
                        Log.Info("Save", $"{corr} success count={changeList.Count}");
                    return (res.Ok, res.Error, compBase64, userBase64);
                }
                catch (Exception ex)
                {
                    Log.Error("Save", $"{corr} elevation call failed", ex);
                    return (false, ex.Message, compBase64, userBase64);
                }
            }
            catch (Exception exOuter)
            {
                Log.Error("Save", $"{corr} unexpected outer failure", exOuter);
                return (false, exOuter.Message, null, null);
            }
        }
    }
}
