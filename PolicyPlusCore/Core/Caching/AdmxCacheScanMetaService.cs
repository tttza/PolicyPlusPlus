namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheScanMetaService
{
    internal static async Task RecordSourceRootIfGlobalRebuildAsync(
        bool didGlobalRebuild,
        string root,
        Func<string, string, CancellationToken, Task> setMetaAsync,
        CancellationToken ct
    )
    {
        if (!didGlobalRebuild)
            return;

        try
        {
            await setMetaAsync("source_root", root, ct).ConfigureAwait(false);
        }
        catch { }
    }
}
