namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheScanService
{
    public static Task ScanAndUpdateAsync(
        AdmxCache cache,
        IEnumerable<string> cultures,
        CancellationToken ct
    )
    {
        if (cache == null)
            throw new ArgumentNullException(nameof(cache));
        return cache.ScanAndUpdateCoreAsync(cultures, ct);
    }
}
