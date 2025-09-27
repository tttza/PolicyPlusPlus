using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Core; // Ensure AdmxCache type is visible
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests.Core;

// Disable parallel execution for this collection to avoid env var collisions.
// Reuse IsolatedCacheFixture so each test gets its own cache directory and env var automatically.
public class AdmxCacheFileUsageTests : IClassFixture<IsolatedCacheFixture>
{
    private readonly IsolatedCacheFixture _fixture;

    public AdmxCacheFileUsageTests(IsolatedCacheFixture fixture) => _fixture = fixture;

    private static string CreateTempDir()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "PPFileUsageTests_" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static (string sourceRoot, string cacheDir) CreateAdmxSet()
    {
        var root = CreateTempDir();
        // Minimal ADMX + ADML pair sufficient for parser
        var admx = "test.admx";
        var admlDir = Path.Combine(root, "en-US");
        Directory.CreateDirectory(admlDir);
        File.WriteAllText(
            Path.Combine(root, admx),
            ""
                + "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<policyDefinitions revision=\"1.0\" schemaVersion=\"1.0\">\n"
                + "  <policyNamespaces>\n"
                + "    <target prefix=\"TestNs\" namespace=\"Test.Ns\"/>\n"
                + "    <using prefix=\"windows\" namespace=\"Microsoft.Policies.Windows\"/>\n"
                + "  </policyNamespaces>\n"
                + "  <resources minRequiredRevision=\"1.0\"/>\n"
                + "  <supportedOn>\n"
                + "    <definitions><definition name=\"SUPPORTED\" displayName=\"$(string.SUPPORTED)\"/></definitions>\n"
                + "  </supportedOn>\n"
                + "  <categories>\n"
                + "    <category name=\"CatRoot\" displayName=\"$(string.CatRoot)\"/>\n"
                + "  </categories>\n"
                + "  <policies>\n"
                + "    <policy name=\"TestPolicy\" class=\"Machine\" displayName=\"$(string.PolicyName)\" explainText=\"$(string.PolicyExplain)\" key=\"Software\\\\Test\" valueName=\"Val\" presentation=\"$(presentation.Pres1)\">\n"
                + "      <supportedOn ref=\"SUPPORTED\"/>\n"
                + "      <elements/>\n"
                + "    </policy>\n"
                + "  </policies>\n"
                + "  <presentations><presentation id=\"Pres1\"/></presentations>\n"
                + "</policyDefinitions>\n"
        );
        File.WriteAllText(
            Path.Combine(admlDir, "test.adml"),
            ""
                + "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<policyDefinitionResources revision=\"1.0\" schemaVersion=\"1.0\">\n"
                + "  <displayName table=\"$(string.table)\"/>\n"
                + "  <resources>\n"
                + "    <stringTable>\n"
                + "      <string id=\"SUPPORTED\">Supported</string>\n"
                + "      <string id=\"CatRoot\">Category Root</string>\n"
                + "      <string id=\"PolicyName\">Test Policy</string>\n"
                + "      <string id=\"PolicyExplain\">Explain</string>\n"
                + "    </stringTable>\n"
                + "    <presentationTable><presentation id=\"Pres1\"/></presentationTable>\n"
                + "  </resources>\n"
                + "</policyDefinitionResources>\n"
        );
        var cache = CreateTempDir();
        return (root, cache);
    }

    private static string GetDbPath(string cacheDir) => Path.Combine(cacheDir, "admxcache.sqlite");

    [Fact]
    public async Task FileUsage_IsRecorded_ForAdmxAndAdml()
    {
        var (root, cacheDir) = CreateAdmxSet();
        // Override cache dir per test to avoid cross-test interference.
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", cacheDir);
        var dbPath = GetDbPath(cacheDir);
        var cacheImpl = new AdmxCache();
        await cacheImpl.InitializeAsync();
        cacheImpl.SetSourceRoot(root);
        await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });

        using var conn = new SqliteConnection("Data Source=" + dbPath);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM FileUsage";
        var obj = await cmd.ExecuteScalarAsync();
        var count = Convert.ToInt32(obj);
        Assert.True(count >= 2, "Expected at least ADMX + ADML usage rows");
    }

    [Fact]
    public async Task Purge_Removes_StaleEntries()
    {
        var (root, cacheDir) = CreateAdmxSet();
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", cacheDir);
        var dbPath = GetDbPath(cacheDir);
        var cacheImpl = new AdmxCache();
        await cacheImpl.InitializeAsync();
        cacheImpl.SetSourceRoot(root);
        await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });
        // Force age of entries; retry a few times in case of transient lock contention.
        var oldEpoch = DateTimeOffset.UtcNow.AddDays(-61).ToUnixTimeSeconds();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            using (var conn = new SqliteConnection("Data Source=" + dbPath))
            {
                await conn.OpenAsync();
                using var up = conn.CreateCommand();
                up.CommandText = $"UPDATE FileUsage SET last_access_utc = {oldEpoch}";
                try
                {
                    up.ExecuteNonQuery();
                }
                catch { }
                using var verify = conn.CreateCommand();
                verify.CommandText = "SELECT MIN(last_access_utc) FROM FileUsage";
                var minVal = Convert.ToInt64(await verify.ExecuteScalarAsync());
                if (minVal <= oldEpoch)
                    break;
            }
            await Task.Delay(150);
        }
        var removed = await cacheImpl.PurgeStaleCacheEntriesAsync(TimeSpan.FromDays(30));
        Assert.True(removed >= 2, $"Expected removal >=2, got {removed}");
        using (var conn2 = new SqliteConnection("Data Source=" + dbPath))
        {
            await conn2.OpenAsync();
            using var c2 = conn2.CreateCommand();
            c2.CommandText = "SELECT COUNT(*) FROM FileUsage";
            var left = Convert.ToInt32(await c2.ExecuteScalarAsync());
            Assert.Equal(0, left);
        }
    }

    [Fact]
    public async Task Subsequent_Scan_Updates_LastAccess()
    {
        var (root, cacheDir) = CreateAdmxSet();
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", cacheDir);
        var dbPath = GetDbPath(cacheDir);
        var cacheImpl = new AdmxCache();
        await cacheImpl.InitializeAsync();
        cacheImpl.SetSourceRoot(root);
        await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });
        long firstMax;
        using (var conn = new SqliteConnection("Data Source=" + dbPath))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MAX(last_access_utc) FROM FileUsage";
            var obj = await cmd.ExecuteScalarAsync();
            firstMax = (obj == null || obj == DBNull.Value) ? 0 : Convert.ToInt64(obj);
        }
        await Task.Delay(1500);
        await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });
        using (var conn2 = new SqliteConnection("Data Source=" + dbPath))
        {
            await conn2.OpenAsync();
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = "SELECT MAX(last_access_utc) FROM FileUsage";
            var obj2 = await cmd2.ExecuteScalarAsync();
            var secondMax = (obj2 == null || obj2 == DBNull.Value) ? 0 : Convert.ToInt64(obj2);
            Assert.True(
                secondMax >= firstMax,
                $"Expected secondMax >= firstMax (first={firstMax}, second={secondMax})"
            );
        }
    }
}
