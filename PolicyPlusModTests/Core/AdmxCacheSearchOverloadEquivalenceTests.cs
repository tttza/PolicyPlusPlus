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
public sealed class AdmxCacheSearchOverloadEquivalenceTests
{
    private const string AdmxFileName = "SearchOverloadEquivalence.admx";

    private static string CreateSyntheticAdmxRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PolicyPlusModTests",
            "SearchOverloadEquivalence",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "en-US"));

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
            { "AlphaPolicy_Explain", "Alpha token appears in description too" },
            { "BetaPolicy", "Beta Policy" },
            { "BetaPolicy_Explain", "Description mentions Alpha even though name is Beta" },
        };
        File.WriteAllText(
            Path.Combine(root, "en-US", "SearchOverloadEquivalence.adml"),
            BuildAdml(en),
            Encoding.UTF8
        );

        return root;
    }

    private static async Task<IAdmxCache> BuildCacheAsync(string root)
    {
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        cache.SetSourceRoot(root);
        await cache.ScanAndUpdateAsync(new[] { "en-US" });
        return cache;
    }

    private static Task WithCacheAsync(Func<string, Task> body) =>
        AdmxCacheTestEnvironment.RunWithScopedCacheAsync(
            async () =>
            {
                var root = CreateSyntheticAdmxRoot();
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
            AdmxFileName
        );

    private static HashSet<string> AsUniqueIdSet(IEnumerable<PolicyHit> hits) =>
        hits.Select(h => h.UniqueId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task Simple_Overload_Maps_To_DefaultFields_CanonicalOverload()
    {
        await WithCacheAsync(async root =>
        {
            var cache = await BuildCacheAsync(root);

            var a = await cache.SearchAsync("Alpha", "en-US", 50);

            var defaultFields = SearchFields.Name | SearchFields.Id | SearchFields.Registry;
            var b = await cache.SearchAsync(
                "Alpha",
                new[] { "en-US" },
                defaultFields,
                andMode: false,
                50
            );

            Assert.Equal(AsUniqueIdSet(a), AsUniqueIdSet(b));
        });
    }

    [Fact]
    public async Task IncludeDescription_Overload_Maps_To_DescriptionField_Toggle()
    {
        await WithCacheAsync(async root =>
        {
            var cache = await BuildCacheAsync(root);

            var a = await cache.SearchAsync("Alpha", "en-US", includeDescription: false, 50);
            var b = await cache.SearchAsync(
                "Alpha",
                new[] { "en-US" },
                SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                andMode: false,
                50
            );
            Assert.Equal(AsUniqueIdSet(a), AsUniqueIdSet(b));

            var c = await cache.SearchAsync("Alpha", "en-US", includeDescription: true, 50);
            var d = await cache.SearchAsync(
                "Alpha",
                new[] { "en-US" },
                SearchFields.Name
                    | SearchFields.Id
                    | SearchFields.Registry
                    | SearchFields.Description,
                andMode: false,
                50
            );
            Assert.Equal(AsUniqueIdSet(c), AsUniqueIdSet(d));
        });
    }
}
