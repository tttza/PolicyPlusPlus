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

        // OR (default) mode: 'Dummy' present so expect >0 hits
        var orHits = await cache.SearchAsync(
            "Dummy XyzNotPresentToken",
            new[] { "en-US" },
            SearchFields.Name | SearchFields.Id | SearchFields.Description,
            andMode: false,
            limit: 200
        );
        Assert.NotNull(orHits);
        Assert.True(orHits.Count > 0, "OR mode should yield hits for token 'Dummy'.");

        // AND mode: includes a non-existent token so expect 0 hits
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
