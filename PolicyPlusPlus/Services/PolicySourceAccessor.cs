using PolicyPlusCore.IO;
using PolicyPlusPlus.Logging;

namespace PolicyPlusPlus.Services
{
    internal readonly struct PolicySourceContext
    {
        public PolicySourceContext(
            IPolicySource comp,
            IPolicySource user,
            PolicySourceMode mode,
            bool fallbackUsed
        )
        {
            Comp = comp;
            User = user;
            Mode = mode;
            FallbackUsed = fallbackUsed;
        }

        public IPolicySource Comp { get; }
        public IPolicySource User { get; }
        public PolicySourceMode Mode { get; }
        public bool FallbackUsed { get; }
    }

    internal static class PolicySourceAccessor
    {
        // Centralized acquisition. Never silently downgrades a Custom/Temp mode to Local.
        // Returns fallbackUsed=true only if Local GPO had to be created because no sources existed in Local mode.
        public static PolicySourceContext Acquire()
        {
            var mgr = PolicySourceManager.Instance;
            bool fallback = false;
            try
            {
                if (mgr.Mode == PolicySourceMode.CustomPol || mgr.Mode == PolicySourceMode.TempPol)
                {
                    if (mgr.CompSource == null || mgr.UserSource == null)
                    {
                        // This indicates an invariant break; log and attempt to re-switch to restore sources.
                        Log.Warn(
                            "PolicySource",
                            "Sources null in mode=" + mgr.Mode + "; re-switching"
                        );
                        if (
                            mgr.Mode == PolicySourceMode.CustomPol
                            && !string.IsNullOrEmpty(mgr.CustomCompPath)
                            && !string.IsNullOrEmpty(mgr.CustomUserPath)
                        )
                            mgr.SwitchCustomPolFlexible(
                                mgr.CustomCompPath,
                                mgr.CustomUserPath,
                                allowSingle: true
                            );
                        else if (mgr.Mode == PolicySourceMode.TempPol)
                            mgr.Switch(PolicySourceDescriptor.TempPol());
                    }
                }
                else // Local
                {
                    if (mgr.CompSource == null || mgr.UserSource == null)
                    {
                        fallback = true;
                        mgr.Switch(PolicySourceDescriptor.LocalGpo());
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("PolicySource", "Acquire failed", ex);
            }

            var comp = mgr.CompSource;
            var user = mgr.UserSource;
            if (comp == null || user == null)
            {
                // As a last resort create ephemeral Local sources (still flagged as fallback).
                try
                {
                    var localMgr = PolicySourceManager.Instance;
                    if (localMgr.Mode != PolicySourceMode.LocalGpo)
                    {
                        fallback = true;
                        localMgr.Switch(PolicySourceDescriptor.LocalGpo());
                        comp = localMgr.CompSource;
                        user = localMgr.UserSource;
                    }
                }
                catch { }
            }
            if (comp == null || user == null)
            {
                // Give up; create inert dummy sources (empty POL) to avoid null reference crashes.
                var tempComp = new PolicyLoader(
                    PolicyLoaderSource.PolFile,
                    System.IO.Path.GetTempFileName(),
                    false
                ).OpenSource();
                var tempUser = new PolicyLoader(
                    PolicyLoaderSource.PolFile,
                    System.IO.Path.GetTempFileName(),
                    true
                ).OpenSource();
                Log.Warn(
                    "PolicySource",
                    "Created inert fallback sources; application state inconsistent"
                );
                return new PolicySourceContext(tempComp, tempUser, PolicySourceMode.LocalGpo, true);
            }
            return new PolicySourceContext(comp, user, mgr.Mode, fallback);
        }
    }
}
