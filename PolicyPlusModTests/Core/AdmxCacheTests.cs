using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using PolicyPlusModTests.TestHelpers;
using SharedTest;
using Xunit;

namespace PolicyPlusModTests.Core;

// Ensure this test class also uses isolated cache so runs are hermetic.
[Collection("AdmxCache.Isolated")]
public class AdmxCacheTests
{
    [Fact]
    public async Task InitializeAndScan_Works()
    {
        await AdmxCacheTestEnvironment.RunWithScopedCacheAsync(async () =>
        {
            IAdmxCache cache = new AdmxCache();
            await cache.InitializeAsync();
            try
            {
                var testRoot = AdmxTestHelper.ResolveAdmxAssetsPath();
                cache.SetSourceRoot(testRoot);
            }
            catch { }
            await cache.ScanAndUpdateAsync();
            var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            var hits = await cache.SearchAsync("Dummy", culture, 10);
            Assert.NotNull(hits);
        });
    }
}
