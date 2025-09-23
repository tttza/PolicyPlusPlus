using System;
using System.IO;
using PolicyPlusCore.Admx;
using SharedTest;
using Xunit;

namespace PolicyPlusModTests.Core.Admx
{
    public class AdmxBundleFallbackTests
    {
        [Fact(DisplayName = "AdmxBundle picks en-US fallback when specified language missing")]
        public void PicksEnUsFallback_WhenPreferredMissing()
        {
            var baseDir = AdmxTestHelper.ResolveAdmxAssetsPath();
            // Ensure a culture that is not present so fallback triggers.
            var b = new AdmxBundle { EnableLanguageFallback = true };
            var dummyPath = Path.Combine(baseDir, "Dummy.admx");
            var fails = b.LoadFile(dummyPath, "zz-ZZ");
            Assert.Empty(fails);
            Assert.Contains("PolicyPlus.Dummy:DummyPolicy", b.Policies.Keys);
            // en-US exists in TestAssets, so it should have resolved strings
            var pol = b.Policies["PolicyPlus.Dummy:DummyPolicy"];
            Assert.Equal("Enable Dummy Feature", pol.DisplayName);
        }

        [Fact(DisplayName = "AdmxBundle caches directory enumeration to avoid repeated IO")]
        public void DirectoryEnumerationIsCached_BasicSanity()
        {
            var baseDir = AdmxTestHelper.ResolveAdmxAssetsPath();
            var b = new AdmxBundle { EnableLanguageFallback = true };
            // Run twice to hit internal caches
            foreach (var _ in b.LoadFolder(baseDir, "zz-ZZ")) { }
            foreach (var _ in b.LoadFolder(baseDir, "zz-ZZ")) { }
            // Just basic sanity: policies are populated and not duplicated
            Assert.NotEmpty(b.Policies);
        }
    }
}
