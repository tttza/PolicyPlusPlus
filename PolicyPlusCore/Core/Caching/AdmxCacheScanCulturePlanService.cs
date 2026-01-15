namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheScanCulturePlanService
{
    public static async Task<List<string>> BuildCultureListAsync(
        IEnumerable<string>? requestedCultures,
        string defaultCulture,
        bool needGlobalRebuild,
        Func<CancellationToken, Task<IReadOnlyList<string>>> getExistingCulturesAsync,
        CancellationToken ct
    )
    {
        var cultureList = requestedCultures is null
            ? new List<string> { defaultCulture }
            : [.. requestedCultures];

        if (cultureList.Count == 0)
            cultureList.Add(defaultCulture);

        // Global rebuild: preserve existing cultures so they are retained even if caller passed a subset.
        if (needGlobalRebuild)
        {
            try
            {
                var existing = await getExistingCulturesAsync(ct).ConfigureAwait(false);
                if (existing.Count > 0)
                {
                    var set = new HashSet<string>(cultureList, StringComparer.OrdinalIgnoreCase);
                    foreach (var ec in existing)
                    {
                        if (!set.Contains(ec))
                            cultureList.Add(ec);
                    }
                }
            }
            catch { }
        }

        return cultureList;
    }
}
