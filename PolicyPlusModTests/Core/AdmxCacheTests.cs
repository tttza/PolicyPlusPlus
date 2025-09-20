using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core;

public class AdmxCacheTests
{
    [Fact]
    public async Task InitializeAndScan_Works()
    {
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        await cache.ScanAndUpdateAsync();
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var hits = await cache.SearchAsync("Dummy", culture, 10);
        // On machines without dummy assets in %windir%, hit count may be zero; ensure no crash
        Assert.NotNull(hits);
    }
}
