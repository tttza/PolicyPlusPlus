using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests.Core;

[Collection("AdmxCache.Isolated")]
public sealed class AdmxCacheScanCharacterizationTests
{
    private const string AdmxFileName = "ScanCharacterization.admx";
    private const string AdmxFileName2 = "ScanCharacterization2.admx";
    private const string BrokenAdmxFileName = "ScanCharacterizationBroken.admx";

    private static string CreateSyntheticAdmxRoot(
        bool includeSecondCulture = false,
        bool includeSecondFile = false
    )
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PolicyPlusModTests",
            "ScanCharacterization",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "en-US"));
        if (includeSecondCulture)
        {
            Directory.CreateDirectory(Path.Combine(root, "fr-FR"));
        }

        var admx =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<policyDefinitions revision=""1.0"" schemaVersion=""1.0"">
  <policyNamespaces>
    <target prefix=""Test"" namespace=""Test.Policies"" />
  </policyNamespaces>
  <resources minRequiredRevision=""1.0"" />
  <categories>
    <category name=""TestCat"" displayName=""$(string.TestCat)"" />
  </categories>
  <policies>
    <policy name=""AlphaPolicy"" class=""Machine"" displayName=""$(string.AlphaPolicy)"" explainText=""$(string.AlphaPolicy_Explain)"" key=""Software\\Policies\\Test"" valueName=""AlphaPolicy"" category=""TestCat"" />
    <policy name=""BetaPolicy"" class=""Machine"" displayName=""$(string.BetaPolicy)"" explainText=""$(string.BetaPolicy_Explain)"" key=""Software\\Policies\\Test"" valueName=""BetaPolicy"" category=""TestCat"" />
  </policies>
</policyDefinitions>";
        File.WriteAllText(Path.Combine(root, AdmxFileName), admx, Encoding.UTF8);

        if (includeSecondFile)
        {
            var admx2 =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<policyDefinitions revision=""1.0"" schemaVersion=""1.0"">
    <policyNamespaces>
        <target prefix=""Test2"" namespace=""Test2.Policies"" />
    </policyNamespaces>
    <resources minRequiredRevision=""1.0"" />
    <categories>
        <category name=""TestCat2"" displayName=""$(string.TestCat2)"" />
    </categories>
    <policies>
        <policy name=""GammaPolicy"" class=""Machine"" displayName=""$(string.GammaPolicy)"" explainText=""$(string.GammaPolicy_Explain)"" key=""Software\\Policies\\Test2"" valueName=""GammaPolicy"" category=""TestCat2"" />
        <policy name=""DeltaPolicy"" class=""Machine"" displayName=""$(string.DeltaPolicy)"" explainText=""$(string.DeltaPolicy_Explain)"" key=""Software\\Policies\\Test2"" valueName=""DeltaPolicy"" category=""TestCat2"" />
    </policies>
</policyDefinitions>";
            File.WriteAllText(Path.Combine(root, AdmxFileName2), admx2, Encoding.UTF8);
        }

        static string BuildAdml(Dictionary<string, string> strings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<policyDefinitionResources revision=\"1.0\" schemaVersion=\"1.0\">");
            sb.AppendLine("  <resources>");
            sb.AppendLine("    <stringTable>");
            foreach (var kv in strings)
            {
                sb.Append("      <string id=\"")
                    .Append(kv.Key)
                    .Append("\">")
                    .Append(System.Security.SecurityElement.Escape(kv.Value))
                    .AppendLine("</string>");
            }
            sb.AppendLine("    </stringTable>");
            sb.AppendLine("  </resources>");
            sb.AppendLine("</policyDefinitionResources>");
            return sb.ToString();
        }

        var en = new Dictionary<string, string>
        {
            { "TestCat", "Test Category" },
            { "AlphaPolicy", "Alpha Policy" },
            { "AlphaPolicy_Explain", "Alpha policy explain" },
            { "BetaPolicy", "Beta Policy" },
            { "BetaPolicy_Explain", "Beta policy explain" },
        };
        File.WriteAllText(
            Path.Combine(root, "en-US", "ScanCharacterization.adml"),
            BuildAdml(en),
            Encoding.UTF8
        );

        if (includeSecondCulture)
        {
            var fr = new Dictionary<string, string>
            {
                { "TestCat", "Categorie de test" },
                { "AlphaPolicy", "Politique Alpha" },
                { "AlphaPolicy_Explain", "Explication politique Alpha" },
                { "BetaPolicy", "Politique Beta" },
                { "BetaPolicy_Explain", "Explication politique Beta" },
            };
            File.WriteAllText(
                Path.Combine(root, "fr-FR", "ScanCharacterization.adml"),
                BuildAdml(fr),
                Encoding.UTF8
            );
        }

        if (includeSecondFile)
        {
            var en2 = new Dictionary<string, string>
            {
                { "TestCat2", "Test Category 2" },
                { "GammaPolicy", "Gamma Policy" },
                { "GammaPolicy_Explain", "Gamma policy explain" },
                { "DeltaPolicy", "Delta Policy" },
                { "DeltaPolicy_Explain", "Delta policy explain" },
            };
            File.WriteAllText(
                Path.Combine(root, "en-US", "ScanCharacterization2.adml"),
                BuildAdml(en2),
                Encoding.UTF8
            );
        }

        return root;
    }

    private static async Task<IAdmxCache> BuildCacheAsync(string root)
    {
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        cache.SetSourceRoot(root);
        return cache;
    }

    private static Task WithCacheAsync(
        Func<string, Task> body,
        string? onlyFiles = null,
        bool includeSecondCulture = false,
        bool includeSecondFile = false
    ) =>
        AdmxCacheTestEnvironment.RunWithScopedCacheAsync(
            async () =>
            {
                var root = CreateSyntheticAdmxRoot(
                    includeSecondCulture: includeSecondCulture,
                    includeSecondFile: includeSecondFile
                );
                try
                {
                    await body(root).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        Directory.Delete(root, true);
                    }
                    catch { }
                }
            },
            onlyFiles ?? AdmxFileName
        );

    private static HashSet<string> AsUniqueIdSet(IEnumerable<PolicyHit> hits) =>
        hits.Select(h => h.UniqueId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void AddPolicyToSyntheticAdmx(string root, string policyName, string displayId)
    {
        var admxPath = Path.Combine(root, AdmxFileName);
        var text = File.ReadAllText(admxPath, Encoding.UTF8);
        var insert =
            $"    <policy name=\"{policyName}\" class=\"Machine\" displayName=\"$(string.{displayId})\" explainText=\"$(string.{displayId}_Explain)\" key=\"Software\\Policies\\Test\" valueName=\"{policyName}\" category=\"TestCat\" />\n";

        var idx = text.IndexOf("  </policies>", StringComparison.Ordinal);
        if (idx < 0)
            throw new InvalidOperationException(
                "Synthetic ADMX does not contain </policies> marker."
            );
        text = text.Insert(idx, insert);
        File.WriteAllText(admxPath, text, Encoding.UTF8);

        var admlPath = Path.Combine(root, "en-US", "ScanCharacterization.adml");
        var adml = File.ReadAllText(admlPath, Encoding.UTF8);
        var stringInsert =
            $"      <string id=\"{displayId}\">{System.Security.SecurityElement.Escape(displayId + " Policy")}</string>\n"
            + $"      <string id=\"{displayId}_Explain\">{System.Security.SecurityElement.Escape(displayId + " policy explain")}</string>\n";
        var stIdx = adml.IndexOf("    </stringTable>", StringComparison.Ordinal);
        if (stIdx < 0)
            throw new InvalidOperationException(
                "Synthetic ADML does not contain </stringTable> marker."
            );
        adml = adml.Insert(stIdx, stringInsert);
        File.WriteAllText(admlPath, adml, Encoding.UTF8);
    }

    [Fact]
    public async Task ScanAndUpdateAsync_IsStable_OnRepeatedRuns_ForSameInput()
    {
        await WithCacheAsync(async root =>
        {
            var cache = await BuildCacheAsync(root);

            await cache.ScanAndUpdateAsync(new[] { "en-US" });
            var hits1 = await cache.SearchAsync("Policy", "en-US", limit: 200);
            var set1 = AsUniqueIdSet(hits1);

            Assert.Contains("Test.Policies:AlphaPolicy", set1);
            Assert.Contains("Test.Policies:BetaPolicy", set1);

            var d1 = await cache.GetByPolicyNameAsync("Test.Policies", "AlphaPolicy", "en-US");
            Assert.NotNull(d1);

            await cache.ScanAndUpdateAsync(new[] { "en-US" });
            var hits2 = await cache.SearchAsync("Policy", "en-US", limit: 200);
            var set2 = AsUniqueIdSet(hits2);

            Assert.Equal(set1, set2);

            var d2 = await cache.GetByPolicyNameAsync("Test.Policies", "AlphaPolicy", "en-US");
            Assert.NotNull(d2);
            Assert.Equal(d1!, d2!);
        });
    }

    [Fact]
    public async Task ScanAndUpdateAsync_IsCultureOrderIndependent_ForSameRoot()
    {
        Dictionary<string, HashSet<string>>? resultA = null;
        Dictionary<string, object?>? detailsA = null;

        await WithCacheAsync(
            async root =>
            {
                var cache = await BuildCacheAsync(root);
                await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });

                var enHits = await cache.SearchAsync("Policy", "en-US", limit: 200);
                var frHits = await cache.SearchAsync("Policy", "fr-FR", limit: 200);
                resultA = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["en-US"] = AsUniqueIdSet(enHits),
                    ["fr-FR"] = AsUniqueIdSet(frHits),
                };
                detailsA = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["en-US"] = await cache.GetByPolicyNameAsync(
                        "Test.Policies",
                        "AlphaPolicy",
                        "en-US"
                    ),
                    ["fr-FR"] = await cache.GetByPolicyNameAsync(
                        "Test.Policies",
                        "AlphaPolicy",
                        "fr-FR"
                    ),
                };
            },
            onlyFiles: AdmxFileName,
            includeSecondCulture: true
        );

        Dictionary<string, HashSet<string>>? resultB = null;
        Dictionary<string, object?>? detailsB = null;

        await WithCacheAsync(
            async root =>
            {
                var cache = await BuildCacheAsync(root);
                await cache.ScanAndUpdateAsync(new[] { "fr-FR", "en-US" });

                var enHits = await cache.SearchAsync("Policy", "en-US", limit: 200);
                var frHits = await cache.SearchAsync("Policy", "fr-FR", limit: 200);
                resultB = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["en-US"] = AsUniqueIdSet(enHits),
                    ["fr-FR"] = AsUniqueIdSet(frHits),
                };
                detailsB = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["en-US"] = await cache.GetByPolicyNameAsync(
                        "Test.Policies",
                        "AlphaPolicy",
                        "en-US"
                    ),
                    ["fr-FR"] = await cache.GetByPolicyNameAsync(
                        "Test.Policies",
                        "AlphaPolicy",
                        "fr-FR"
                    ),
                };
            },
            onlyFiles: AdmxFileName,
            includeSecondCulture: true
        );

        Assert.NotNull(resultA);
        Assert.NotNull(resultB);
        Assert.NotNull(detailsA);
        Assert.NotNull(detailsB);

        Assert.Equal(resultA!["en-US"], resultB!["en-US"]);
        Assert.Equal(resultA["fr-FR"], resultB["fr-FR"]);

        Assert.NotNull(detailsA!["en-US"]);
        Assert.NotNull(detailsA["fr-FR"]);
        Assert.Equal(detailsA["en-US"]!, detailsB!["en-US"]!);
        Assert.Equal(detailsA["fr-FR"]!, detailsB["fr-FR"]!);
    }

    [Fact]
    public async Task ScanAndUpdateAsync_OnlyFilesFilter_RestrictsIndexedPolicies()
    {
        await WithCacheAsync(
            async root =>
            {
                var cache = await BuildCacheAsync(root);
                await cache.ScanAndUpdateAsync(new[] { "en-US" });

                var hits = await cache.SearchAsync("Policy", "en-US", limit: 200);
                var ids = AsUniqueIdSet(hits);

                Assert.Contains("Test.Policies:AlphaPolicy", ids);
                Assert.Contains("Test.Policies:BetaPolicy", ids);
                Assert.DoesNotContain("Test2.Policies:GammaPolicy", ids);
                Assert.DoesNotContain("Test2.Policies:DeltaPolicy", ids);
            },
            onlyFiles: AdmxFileName,
            includeSecondFile: true
        );

        await WithCacheAsync(
            async root =>
            {
                var cache = await BuildCacheAsync(root);
                await cache.ScanAndUpdateAsync(new[] { "en-US" });

                var hits = await cache.SearchAsync("Policy", "en-US", limit: 200);
                var ids = AsUniqueIdSet(hits);

                Assert.DoesNotContain("Test.Policies:AlphaPolicy", ids);
                Assert.DoesNotContain("Test.Policies:BetaPolicy", ids);
                Assert.Contains("Test2.Policies:GammaPolicy", ids);
                Assert.Contains("Test2.Policies:DeltaPolicy", ids);
            },
            onlyFiles: AdmxFileName2,
            includeSecondFile: true
        );
    }

    [Fact]
    public async Task ScanAndUpdateAsync_DetectsAdmxChange_AndUpdatesIndex()
    {
        await WithCacheAsync(async root =>
        {
            var cache = await BuildCacheAsync(root);
            await cache.ScanAndUpdateAsync(new[] { "en-US" });

            var before = AsUniqueIdSet(await cache.SearchAsync("Policy", "en-US", limit: 200));
            Assert.DoesNotContain("Test.Policies:GammaPolicy", before);

            AddPolicyToSyntheticAdmx(root, policyName: "GammaPolicy", displayId: "GammaPolicy");

            await cache.ScanAndUpdateAsync(new[] { "en-US" });
            var after = AsUniqueIdSet(await cache.SearchAsync("Policy", "en-US", limit: 200));

            Assert.Contains("Test.Policies:GammaPolicy", after);
            Assert.Contains("Test.Policies:AlphaPolicy", after);
            Assert.Contains("Test.Policies:BetaPolicy", after);
        });
    }

    [Fact]
    public async Task ScanAndUpdateAsync_WhenCultureAdmlMissing_RemovesLocalizedSearchTerms()
    {
        await WithCacheAsync(
            async root =>
            {
                var cache = await BuildCacheAsync(root);
                await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });

                var frBefore = AsUniqueIdSet(
                    await cache.SearchAsync("Politique", "fr-FR", limit: 200)
                );
                Assert.Contains("Test.Policies:AlphaPolicy", frBefore);
                Assert.Contains("Test.Policies:BetaPolicy", frBefore);

                try
                {
                    File.Delete(Path.Combine(root, "fr-FR", "ScanCharacterization.adml"));
                }
                catch { }

                await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });

                var frAfter = AsUniqueIdSet(
                    await cache.SearchAsync("Politique", "fr-FR", limit: 200)
                );
                Assert.DoesNotContain("Test.Policies:AlphaPolicy", frAfter);
                Assert.DoesNotContain("Test.Policies:BetaPolicy", frAfter);
            },
            onlyFiles: AdmxFileName,
            includeSecondCulture: true
        );
    }

    [Fact]
    public async Task ScanAndUpdateAsync_PartialFailures_DoNotAbortWholeScan()
    {
        await WithCacheAsync(
            async root =>
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(root, BrokenAdmxFileName),
                        "<policyDefinitions><policies>",
                        Encoding.UTF8
                    );
                }
                catch { }

                var cache = await BuildCacheAsync(root);

                await cache.ScanAndUpdateAsync(new[] { "en-US" });

                var hits = await cache.SearchAsync("Policy", "en-US", limit: 200);
                var ids = AsUniqueIdSet(hits);

                Assert.Contains("Test.Policies:AlphaPolicy", ids);
                Assert.Contains("Test.Policies:BetaPolicy", ids);
            },
            onlyFiles: $"{AdmxFileName};{BrokenAdmxFileName}"
        );
    }
}
