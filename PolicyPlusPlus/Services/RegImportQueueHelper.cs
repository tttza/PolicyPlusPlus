using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.IO;

namespace PolicyPlusPlus.Services
{
    internal static class RegImportQueueHelper
    {
        // Queues pending changes from a .reg-derived RegFile; if replace == true existing pending changes are discarded first.
        public static int Queue(RegFile reg, AdmxBundle bundle, bool replace)
        {
            if (reg == null || bundle == null)
                return 0;
            if (replace)
            {
                int before = PendingChangesService.Instance.Pending.Count;
                PendingChangesService.Instance.DiscardAll();
                int after = PendingChangesService.Instance.Pending.Count;
                if (after != 0)
                {
                    Logging.Log.Warn(
                        "RegImport",
                        "DiscardAll did not clear pending count=" + after
                    );
                    // Force clear leftover entries defensively.
                    var leftovers = PendingChangesService.Instance.Pending.ToArray();
                    if (leftovers.Length > 0)
                        PendingChangesService.Instance.Discard(leftovers);
                    Logging.Log.Info(
                        "RegImport",
                        "Forced discard leftover count=" + leftovers.Length
                    );
                }
                else if (before > 0)
                {
                    Logging.Log.Info("RegImport", "Cleared pending count=" + before);
                }
            }
            // includeClearsForMissing ensures policies configured but absent in reg are cleared.
            return PolicyPolDiffImporter.QueueFromReg(
                reg,
                bundle,
                includeClearsForMissing: replace
            );
        }
    }
}
