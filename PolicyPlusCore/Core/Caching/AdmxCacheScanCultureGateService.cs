namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheScanCultureGateService
{
    public static async Task<bool> TryPurgeAndSkipIfCultureMissingAdmlAsync(
        string root,
        string culture,
        Func<string, CancellationToken, Task> purgeCultureAsync,
        CancellationToken ct
    )
    {
        // No fallback persistence: if culture dir missing or empty, purge previous rows and skip.
        try
        {
            var cultureDir = Path.Combine(root, culture);
            bool cultureHasAdml =
                Directory.Exists(cultureDir)
                && Directory
                    .EnumerateFiles(cultureDir, "*.adml", SearchOption.TopDirectoryOnly)
                    .Any();

            if (!cultureHasAdml)
            {
                try
                {
                    await purgeCultureAsync(culture, ct).ConfigureAwait(false);
                }
                catch { }

                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
