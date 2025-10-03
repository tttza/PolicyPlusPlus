using System;
using System.IO;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core;

// Tests cache search behavior for AND mode (all tokens must match)
[Collection("AdmxCache.Isolated")]
public class AdmxCacheAndModeTests
{
    private static string FindAdmxRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent!)
        {
            var candidate = Path.Combine(dir.FullName, "TestAssets", "TestAssets", "Admx");
            if (Directory.Exists(candidate))
                return candidate;
        }
        throw new DirectoryNotFoundException("TestAssets/TestAssets/Admx not found");
    }

    [Fact]
    public async Task AndMode_Eliminates_PartialMatches()
    {
        var root = FindAdmxRoot();
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        cache.SetSourceRoot(root);
        await cache.ScanAndUpdateAsync(new[] { "en-US" });

        // Baseline single-token OR (default) mode: expect hits for 'Dummy'.
        var singleTokenOr = await cache.SearchAsync(
            "Dummy",
            new[] { "en-US" },
            SearchFields.Name | SearchFields.Id | SearchFields.Description,
            andMode: false,
            limit: 200
        );
        Assert.NotNull(singleTokenOr);
        Assert.True(
            singleTokenOr.Count > 0,
            "Single-token OR search should yield hits for 'Dummy'."
        );

        // Phrase OR (contains space) now treated as phrase-substring search (noise suppression); may yield 0 hits if full phrase absent.
        var phraseOr = await cache.SearchAsync(
            "Dummy XyzNotPresentToken",
            new[] { "en-US" },
            SearchFields.Name | SearchFields.Id | SearchFields.Description,
            andMode: false,
            limit: 200
        );
        Assert.NotNull(phraseOr); // No strict count assertion: phrase likely absent.

        // AND mode: multi-token (space-separated) requires each token to appear; second token is absent so expect 0.
        var andHits = await cache.SearchAsync(
            "Dummy XyzNotPresentToken",
            new[] { "en-US" },
            SearchFields.Name | SearchFields.Id | SearchFields.Description,
            andMode: true,
            limit: 200
        );
        Assert.NotNull(andHits);
        Assert.True(andHits.Count == 0, "AND mode should eliminate hits when one token absent.");
    }
}
