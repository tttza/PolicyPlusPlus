namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheScanSignatureService
{
    public static async Task<(bool SkipCulture, string? CurrentSig)> ShouldSkipCultureAsync(
        string root,
        string culture,
        bool allowGlobalRebuild,
        Func<string, CancellationToken, Task<string?>> getMetaAsync,
        Func<string, string, CancellationToken, Task<string>> computeSourceSignatureAsync,
        CancellationToken ct
    )
    {
        if (allowGlobalRebuild)
            return (SkipCulture: false, CurrentSig: null);

        try
        {
            var prior = await getMetaAsync("sig_" + culture, ct).ConfigureAwait(false);
            var currentSig = await computeSourceSignatureAsync(root, culture, ct)
                .ConfigureAwait(false);
            if (
                !string.IsNullOrEmpty(prior)
                && string.Equals(prior, currentSig, StringComparison.Ordinal)
            )
                return (SkipCulture: true, CurrentSig: currentSig);
            return (SkipCulture: false, CurrentSig: currentSig);
        }
        catch
        {
            return (SkipCulture: false, CurrentSig: null);
        }
    }

    public static async Task StoreSignatureAsync(
        string culture,
        string? currentSig,
        string root,
        Func<string, string, CancellationToken, Task<string>> computeSourceSignatureAsync,
        Func<string, string, CancellationToken, Task> setMetaAsync,
        CancellationToken ct
    )
    {
        // Store new source signature (reuse computed value when available).
        try
        {
            var sig =
                currentSig
                ?? await computeSourceSignatureAsync(root, culture, ct).ConfigureAwait(false);
            await setMetaAsync("sig_" + culture, sig, ct).ConfigureAwait(false);
        }
        catch { }
    }
}
