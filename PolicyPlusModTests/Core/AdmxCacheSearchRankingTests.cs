using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core;

// Focused ranking/precision tests for recent AdmxCache search enhancements.
public class AdmxCacheSearchRankingTests
{
    private static string CreateSyntheticAdmxRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PolicyPlusModTests",
            "SearchRanking",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "en-US"));
        Directory.CreateDirectory(Path.Combine(root, "ja-JP"));

        // Synthetic ADMX with policies targeting ranking scenarios.
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
    <policy name=""VirtualComponentsAllowList"" class=""Machine"" displayName=""$(string.VirtualComponentsAllowList)"" explainText=""$(string.VirtualComponentsAllowList_Explain)"" key=""Software\\Policies\\Test"" valueName=""VirtualComponentsAllowList"" category=""TestCat"">
      <enabledValue><decimal value=""1""/></enabledValue>
      <disabledValue><decimal value=""0""/></disabledValue>
    </policy>
    <policy name=""VirtualExtensionHandling"" class=""Machine"" displayName=""$(string.VirtualExtensionHandling)"" explainText=""$(string.VirtualExtensionHandling_Explain)"" key=""Software\\Policies\\Test"" valueName=""VirtualExtensionHandling"" category=""TestCat"" />
    <policy name=""SuperLongKeyAlpha"" class=""Machine"" displayName=""$(string.SuperLongKeyAlpha)"" explainText=""$(string.SuperLongKeyAlpha_Explain)"" key=""Software\\Policies\\Test"" valueName=""SuperLongKeyAlpha"" category=""TestCat"" />
    <policy name=""CjkPolicy1"" class=""Machine"" displayName=""$(string.CjkPolicy1)"" explainText=""$(string.CjkPolicy1_Explain)"" key=""Software\\Policies\\Test"" valueName=""CjkPolicy1"" category=""TestCat"" />
    <policy name=""TelemetryControlName"" class=""Machine"" displayName=""$(string.TelemetryControlName)"" explainText=""$(string.TelemetryControlName_Explain)"" key=""Software\\Policies\\Test"" valueName=""TelemetryControlName"" category=""TestCat"" />
    <policy name=""DescriptionOnlyTelemetry"" class=""Machine"" displayName=""$(string.DescriptionOnlyTelemetry)"" explainText=""$(string.DescriptionOnlyTelemetry_Explain)"" key=""Software\\Policies\\Test"" valueName=""DescriptionOnlyTelemetry"" category=""TestCat"" />
  </policies>
</policyDefinitions>";
        File.WriteAllText(Path.Combine(root, "SearchRanking.admx"), admx, Encoding.UTF8);

        // Helper to build ADML string tables.
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
            { "VirtualComponentsAllowList", "Virtual Components Allow List" },
            {
                "VirtualComponentsAllowList_Explain",
                "Allows configuring a virtual components allow list for testing relevance scoring."
            },
            { "VirtualExtensionHandling", "Virtual Extension Handling" },
            {
                "VirtualExtensionHandling_Explain",
                "Controls how virtual extension features are handled. Phrase: virtual extension appears together."
            },
            { "SuperLongKeyAlpha", "SuperLongKeyAlpha" },
            {
                "SuperLongKeyAlpha_Explain",
                "This description mentions Super Long Key split tokens: super long key alpha for noise."
            },
            { "CjkPolicy1", "CjkPolicy1" },
            { "CjkPolicy1_Explain", "JA display name carries meaning; English explain fallback." },
            { "TelemetryControlName", "Telemetry Control Name" },
            { "TelemetryControlName_Explain", "Telemetry is controlled via this policy" },
            { "DescriptionOnlyTelemetry", "Description Only" },
            {
                "DescriptionOnlyTelemetry_Explain",
                "Policy affecting telemetry collection only described here; name omits the token."
            },
        };
        File.WriteAllText(
            Path.Combine(root, "en-US", "SearchRanking.adml"),
            BuildAdml(en),
            Encoding.UTF8
        );

        var ja = new Dictionary<string, string>
        {
            { "TestCat", "テストカテゴリ" },
            { "VirtualComponentsAllowList", "仮想コンポーネント許可リスト" },
            { "VirtualComponentsAllowList_Explain", "仮想コンポーネントの許可リストを構成します" },
            { "VirtualExtensionHandling", "仮想拡張ハンドリング" },
            { "VirtualExtensionHandling_Explain", "仮想 拡張 の機能処理を制御します" },
            { "SuperLongKeyAlpha", "SuperLongKeyAlpha" },
            { "SuperLongKeyAlpha_Explain", "super long key alpha を分割トークンとして含む説明" },
            { "CjkPolicy1", "拡張機能制御" },
            { "CjkPolicy1_Explain", "拡張機能 の 動作を制御" },
            { "TelemetryControlName", "テレメトリ制御" },
            { "TelemetryControlName_Explain", "テレメトリ を制御" },
            { "DescriptionOnlyTelemetry", "説明のみ" },
            { "DescriptionOnlyTelemetry_Explain", "この説明にのみ テレメトリ が含まれる" },
        };
        File.WriteAllText(
            Path.Combine(root, "ja-JP", "SearchRanking.adml"),
            BuildAdml(ja),
            Encoding.UTF8
        );

        return root;
    }

    private static async Task<IAdmxCache> BuildCacheAsync(string root, params string[] cultures)
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        cache.SetSourceRoot(root);
        await cache.ScanAndUpdateAsync(cultures);
        return cache;
    }

    [Fact]
    public async Task CamelCase_Id_Query_Prioritizes_UniqueId()
    {
        var root = CreateSyntheticAdmxRoot();
        try
        {
            var cache = await BuildCacheAsync(root, "en-US");
            var hits = await cache.SearchAsync(
                "VirtualComponentsAllowList",
                "en-US",
                SearchFields.Name | SearchFields.Id | SearchFields.Description,
                10
            );
            Assert.NotNull(hits);
            Assert.True(hits.Count > 0);
            Assert.StartsWith(
                "Test.Policies:VirtualComponentsAllowList",
                hits[0].UniqueId,
                StringComparison.OrdinalIgnoreCase
            );
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Phrase_Search_Ranks_Phrase_Match_First()
    {
        var root = CreateSyntheticAdmxRoot();
        try
        {
            var cache = await BuildCacheAsync(root, "en-US");
            var hits = await cache.SearchAsync(
                "virtual extension",
                "en-US",
                SearchFields.Name | SearchFields.Description,
                10
            );
            Assert.NotNull(hits);
            Assert.True(hits.Count > 0);
            Assert.Contains(
                hits[0].DisplayName.ToLowerInvariant(),
                new[] { "virtual extension handling" }
            );
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Long_Single_Token_Filters_Noise_From_Split_Tokens()
    {
        var root = CreateSyntheticAdmxRoot();
        try
        {
            var cache = await BuildCacheAsync(root, "en-US");
            var hits = await cache.SearchAsync(
                "SuperLongKeyAlpha",
                "en-US",
                SearchFields.Name | SearchFields.Description,
                20
            );
            Assert.NotNull(hits);
            Assert.True(hits.Count > 0);
            Assert.Equal("Test.Policies:SuperLongKeyAlpha", hits[0].UniqueId);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Cjk_Short_Token_Treated_As_Precise()
    {
        var root = CreateSyntheticAdmxRoot();
        try
        {
            var cache = await BuildCacheAsync(root, "ja-JP");
            var hits = await cache.SearchAsync(
                "拡張機能",
                "ja-JP",
                SearchFields.Name | SearchFields.Description,
                20
            );
            Assert.NotNull(hits);
            Assert.True(hits.Count > 0);
            Assert.Equal("Test.Policies:CjkPolicy1", hits[0].UniqueId);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task Description_Only_Match_Ranks_Below_Name_Match_For_Same_Token()
    {
        var root = CreateSyntheticAdmxRoot();
        try
        {
            var cache = await BuildCacheAsync(root, "en-US");
            var hits = await cache.SearchAsync(
                "Telemetry",
                "en-US",
                SearchFields.Name | SearchFields.Description,
                10
            );
            Assert.NotNull(hits);
            var list = hits.ToList();
            var idxName = list.FindIndex(h =>
                h.UniqueId.EndsWith("TelemetryControlName", StringComparison.OrdinalIgnoreCase)
            );
            var idxDescOnly = list.FindIndex(h =>
                h.UniqueId.EndsWith("DescriptionOnlyTelemetry", StringComparison.OrdinalIgnoreCase)
            );
            if (idxName >= 0 && idxDescOnly >= 0)
                Assert.True(
                    idxName < idxDescOnly,
                    "Name match should rank above description-only match"
                );
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch { }
        }
    }
}
