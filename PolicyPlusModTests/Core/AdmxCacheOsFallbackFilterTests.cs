using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core;

// Verifies an OS (non-second) fallback culture yields no hits for policies that already have a primary row.
[Collection("AdmxCache.Isolated")]
public class AdmxCacheOsFallbackFilterTests
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
        throw new DirectoryNotFoundException(
            "TestAssets/TestAssets/Admx not found up-tree from " + AppContext.BaseDirectory
        );
    }

    [Fact]
    public async Task OsFallbackCulture_Suppressed_When_PrimaryPresent()
    {
        var root = FindAdmxRoot();
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        cache.SetSourceRoot(root);

        // Scan: load primary en-US first, then fr-FR as an OS-level fallback (second disabled scenario).
        await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });

        // Search culture order: primary en-US, OS fallback fr-FR, duplicate primary placeholder.
        // Duplicate primary as placeholder so fr-FR is treated as fallback (not second).
        var ordered = new[] { "en-US", "en-US", "fr-FR", "en-US" };

        // Search using token from the French DisplayName: "Activer".
        var hits = await cache.SearchAsync(
            "Activer",
            ordered,
            SearchFields.Name | SearchFields.Id | SearchFields.Registry | SearchFields.Description,
            50
        );
        Assert.NotNull(hits);

        // Expect 0 hits: when a primary en-US row exists, fr-FR fallback rows must be filtered.
        // Any hit > 0 indicates fallback suppression failed.
        Assert.True(
            hits.Count == 0,
            "French fallback rows should be suppressed when primary en-US exists; actual hits="
                + hits.Count
        );
    }
}
