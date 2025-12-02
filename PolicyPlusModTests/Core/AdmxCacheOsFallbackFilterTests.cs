using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using PolicyPlusModTests.TestHelpers;
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
        await AdmxCacheTestEnvironment.RunWithScopedCacheAsync(async () =>
        {
            var root = FindAdmxRoot();
            IAdmxCache cache = new AdmxCache();
            await cache.InitializeAsync();
            cache.SetSourceRoot(root);

            await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });

            var ordered = new[] { "en-US", "en-US", "fr-FR", "en-US" };

            var hits = await cache.SearchAsync(
                "Activer",
                ordered,
                SearchFields.Name
                    | SearchFields.Id
                    | SearchFields.Registry
                    | SearchFields.Description,
                50
            );
            Assert.NotNull(hits);

            foreach (
                var h in hits.Where(h =>
                    h.Culture.Equals("fr-FR", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                var uid = h.UniqueId;
                var parts = uid.Split(':');
                if (parts.Length != 2)
                    continue;
                var idHits = await cache.SearchAsync(
                    parts[1],
                    new[] { "en-US" },
                    SearchFields.Id,
                    10
                );
                bool primaryExists = idHits.Any(x => x.UniqueId == uid && x.Culture == "en-US");
                Assert.False(
                    primaryExists,
                    "Fallback culture fr-FR surfaced despite existing primary row for policy " + uid
                );
            }
        });
    }
}
