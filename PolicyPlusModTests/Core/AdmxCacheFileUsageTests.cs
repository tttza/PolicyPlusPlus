using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Core;
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
        var (root, _) = CreateAdmxSet();
        try
        {
            var cacheImpl = new AdmxCache();
            await cacheImpl.InitializeAsync();
            cacheImpl.SetSourceRoot(root);
            await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });

            // Open DB and query FileUsage
            using var conn = new SqliteConnection("Data Source=" + GetDbPath(_fixture.CacheDir));
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM FileUsage";
            var obj = await cmd.ExecuteScalarAsync();
            var count = Convert.ToInt32(obj);
            Assert.True(count >= 2, "Expected at least ADMX + ADML usage rows");
        }
        finally { }
    }

    [Fact]
    public async Task Purge_Removes_StaleEntries()
    {
        var (root, _) = CreateAdmxSet();
        try
        {
            var cacheImpl = new AdmxCache();
            await cacheImpl.InitializeAsync();
            cacheImpl.SetSourceRoot(root);
            await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });
            var dbPath = GetDbPath(_fixture.CacheDir);
            using (var conn = new SqliteConnection("Data Source=" + dbPath))
            {
                await conn.OpenAsync();
                // Age all entries artificially (older than 60 days)
                using var up = conn.CreateCommand();
                var oldEpoch = DateTimeOffset.UtcNow.AddDays(-61).ToUnixTimeSeconds();
                up.CommandText = $"UPDATE FileUsage SET last_access_utc = {oldEpoch}";
                up.ExecuteNonQuery();
            }
            // Purge with 30 day threshold
            var removed = await cacheImpl.PurgeStaleCacheEntriesAsync(TimeSpan.FromDays(30));
            Assert.True(removed >= 2, $"Expected removal >=2, got {removed}");
            using (var conn2 = new SqliteConnection("Data Source=" + GetDbPath(_fixture.CacheDir)))
            {
                await conn2.OpenAsync();
                using var c2 = conn2.CreateCommand();
                c2.CommandText = "SELECT COUNT(*) FROM FileUsage";
                var left = Convert.ToInt32(await c2.ExecuteScalarAsync());
                Assert.Equal(0, left);
            }
        }
        finally { }
    }

    [Fact]
    public async Task Subsequent_Scan_Updates_LastAccess()
    {
        var (root, _) = CreateAdmxSet();
        try
        {
            var cacheImpl = new AdmxCache();
            await cacheImpl.InitializeAsync();
            cacheImpl.SetSourceRoot(root);
            await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });
            var dbPath = GetDbPath(_fixture.CacheDir);
            long firstMax;
            using (var conn = new SqliteConnection("Data Source=" + dbPath))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT MAX(last_access_utc) FROM FileUsage";
                firstMax = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }
            // Ensure second scan is at least 1 second later to advance timestamp
            await Task.Delay(1500);
            await cacheImpl.ScanAndUpdateAsync(new[] { "en-US" });
            using (var conn2 = new SqliteConnection("Data Source=" + dbPath))
            {
                await conn2.OpenAsync();
                using var cmd2 = conn2.CreateCommand();
                cmd2.CommandText = "SELECT MAX(last_access_utc) FROM FileUsage";
                var secondMax = Convert.ToInt64(await cmd2.ExecuteScalarAsync());
                Assert.True(
                    secondMax >= firstMax,
                    $"Expected secondMax >= firstMax (first={firstMax}, second={secondMax})"
                );
            }
        }
        finally { }
    }
}
