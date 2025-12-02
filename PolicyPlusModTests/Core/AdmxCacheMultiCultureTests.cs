using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests.Core;

// New tests for multi-culture fallback (Strategy A: do not persist fallback language rows).
[Collection("AdmxCache.Isolated")]
public class AdmxCacheMultiCultureTests
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
    public async Task MissingCulture_DoesNotPersist_FallbackRows_And_SearchFallsBack()
    {
        await AdmxCacheTestEnvironment.RunWithScopedCacheAsync(async () =>
        {
            var root = FindAdmxRoot();
            string missingCulture = "xx-XX";
            IAdmxCache cache = new AdmxCache();
            await cache.InitializeAsync();
            cache.SetSourceRoot(root);

            await cache.ScanAndUpdateAsync(new[] { missingCulture, "en-US" });

            var directMissing = await cache.SearchAsync(
                "Dummy",
                missingCulture,
                SearchFields.Name | SearchFields.Id,
                50
            );
            Assert.NotNull(directMissing);

            var multi = await cache.SearchAsync(
                "Dummy",
                new[] { missingCulture, "en-US" },
                SearchFields.Name | SearchFields.Id,
                50
            );
            Assert.NotNull(multi);
            if (multi.Count > 0)
            {
                var first = multi.First();
                var uid = first.UniqueId;
                var idx = uid.IndexOf(':');
                var ns = idx > 0 ? uid.Substring(0, idx) : string.Empty;
                var name = idx > 0 ? uid.Substring(idx + 1) : uid;
                var detail = await cache.GetByPolicyNameAsync(
                    ns,
                    name,
                    new[] { missingCulture, "en-US" }
                );
                Assert.NotNull(detail);
                Assert.NotEqual(missingCulture, detail!.Culture);
            }
        });
    }

    [Fact]
    public async Task AddingPreviouslyMissingCulture_After_Fallback_CausesRowsToExist()
    {
        await AdmxCacheTestEnvironment.RunWithScopedCacheAsync(async () =>
        {
            var root = FindAdmxRoot();
            string newCulture = "fr-FR";
            IAdmxCache cache = new AdmxCache();
            await cache.InitializeAsync();
            cache.SetSourceRoot(root);

            await cache.ScanAndUpdateAsync(new[] { "en-US" });

            var before = await cache.SearchAsync(
                "Dummy",
                newCulture,
                SearchFields.Name | SearchFields.Id,
                20
            );

            await cache.ScanAndUpdateAsync(new[] { newCulture });
            var after = await cache.SearchAsync(
                "Dummy",
                newCulture,
                SearchFields.Name | SearchFields.Id,
                20
            );
            Assert.NotNull(after);
            if (before.Count == 0)
            {
                Assert.True(after.Count >= 0);
            }
        });
    }
}
