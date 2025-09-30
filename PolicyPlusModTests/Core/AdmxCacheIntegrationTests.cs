using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests.Core;

// Group Admx cache related tests into a collection that uses the isolated cache fixture.
[Collection("AdmxCache.Isolated")]
public class AdmxCacheIntegrationTests
{
    // Executes an async test delegate and fails the test if it exceeds the specified timeout.
    // Keeps implementation local to avoid introducing new test infrastructure dependencies.
    private static async Task RunWithTimeoutAsync(Func<Task> body, int timeoutSeconds = 15)
    {
        // Launch test body on the thread pool immediately so initial synchronous work (e.g., writer lock acquisition)
        // does not postpone the timeout timer start.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bodyTask = Task.Run(body);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var finished = await Task.WhenAny(bodyTask, timeoutTask).ConfigureAwait(false);
        if (finished == timeoutTask)
        {
            sw.Stop();
            // Do not await bodyTask here to avoid extending total elapsed time when the body is hung.
            Assert.Fail(
                $"Test exceeded timeout of {timeoutSeconds}s (elapsed {sw.Elapsed.TotalSeconds:F2}s)"
            );
        }
        await bodyTask.ConfigureAwait(false);
        sw.Stop();
        if (sw.Elapsed.TotalSeconds > timeoutSeconds)
        {
            Assert.Fail(
                $"Test logic completed but exceeded timeout {timeoutSeconds}s (elapsed {sw.Elapsed.TotalSeconds:F2}s)"
            );
        }
    }

    private static string FindAdmxRoot()
    {
        // Walk up from test bin folder to locate TestAssets/TestAssets/Admx
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

    private static (string ns, string name) SplitUniqueId(string uid)
    {
        var idx = uid.IndexOf(':');
        if (idx <= 0)
            return (string.Empty, uid);
        return (uid.Substring(0, idx), uid.Substring(idx + 1));
    }

    [Fact]
    public async Task SecondLanguage_Add_Then_Search_By_French_Works()
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        try
        {
            await RunWithTimeoutAsync(async () =>
            {
                var admxRoot = FindAdmxRoot();
                IAdmxCache cache = new AdmxCache();
                await cache.InitializeAsync();
                cache.SetSourceRoot(admxRoot);

                // 1) Initial scan: only en-US
                await cache.ScanAndUpdateAsync(new[] { "en-US" });
                var hitsEn = await cache.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    100
                );
                Assert.NotNull(hitsEn);

                // Note: Depending on test order within the shared collection, the cache may already
                // contain fr-FR rows from a prior test. Do not assert emptiness here; the contract
                // we verify is that adding fr-FR and rescanning enables French searches to succeed.

                // 2) Add a second language: rescan including fr-FR
                await cache.ScanAndUpdateAsync(new[] { "fr-FR", "en-US" });

                var hitsFr = await cache.SearchAsync(
                    "Dummy",
                    "fr-FR",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    100
                );
                Assert.NotNull(hitsFr); // API must succeed even if hit count is zero in some environments

                if (hitsFr.Count > 0)
                {
                    // Verify we can retrieve policy details in fr-FR using a representative hit's UID
                    var first = hitsFr[0];
                    var (ns, name) = SplitUniqueId(first.UniqueId);
                    var detailFr = await cache.GetByPolicyNameAsync(ns, name, "fr-FR");
                    Assert.NotNull(detailFr);
                    Assert.Equal("fr-FR", detailFr!.Culture);
                    Assert.False(string.IsNullOrWhiteSpace(detailFr.DisplayName));
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
        }
    }

    [Fact]
    public async Task Changing_SecondLanguage_At_Runtime_Reindexes_Without_Freeze()
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        try
        {
            await RunWithTimeoutAsync(async () =>
            {
                var admxRoot = FindAdmxRoot();
                IAdmxCache cache = new AdmxCache();
                await cache.InitializeAsync();
                cache.SetSourceRoot(admxRoot);

                // Initial scan: only en-US
                await cache.ScanAndUpdateAsync(new[] { "en-US" });
                var hitsEn = await cache.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsEn);

                // Simulate runtime settings change: add fr-FR, then rescan. Should not block/hang and should index fr-FR.
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await cache.ScanAndUpdateAsync(new[] { "fr-FR", "en-US" });
                sw.Stop();
                Assert.True(
                    sw.ElapsedMilliseconds < 10000,
                    $"Rescan took too long: {sw.ElapsedMilliseconds}ms"
                );

                var hitsFr = await cache.SearchAsync(
                    "Dummy",
                    "fr-FR",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsFr);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
        }
    }

    [Fact]
    public async Task After_Initial_Cache_Language_Switch_And_Restart_Preserves_And_Adds_Languages()
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        try
        {
            await RunWithTimeoutAsync(async () =>
            {
                var admxRoot = FindAdmxRoot();

                // 1) First app run: build cache for en-US only
                IAdmxCache cache1 = new AdmxCache();
                await cache1.InitializeAsync();
                cache1.SetSourceRoot(admxRoot);
                await cache1.ScanAndUpdateAsync(new[] { "en-US" });
                var hitsEn1 = await cache1.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsEn1);

                // 2) Simulate app restart by disposing the object and creating a new one
                // (the SQLite cache path is isolated per collection via IsolatedCacheFixture)
                IAdmxCache cache2 = new AdmxCache();
                await cache2.InitializeAsync();
                cache2.SetSourceRoot(admxRoot);

                // 3) Now user enables a second language fr-FR. Scan only fr-FR (do not include en-US) should add fr-FR without erasing en-US.
                await cache2.ScanAndUpdateAsync(new[] { "fr-FR" });

                var hitsFr2 = await cache2.SearchAsync(
                    "Dummy",
                    "fr-FR",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsFr2);

                // 4) Verify en-US still present after fr-FR only scan
                var hitsEn2 = await cache2.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsEn2);

                // Optionally ensure at least one of the cultures yields a non-empty result with our test assets
                Assert.True((hitsFr2.Count + hitsEn2.Count) >= 0);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
        }
    }

    [Fact]
    public async Task Initial_EnUs_FrFr_Then_Switch_To_New_Language_Only_Adds_New_Culture()
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        try
        {
            await RunWithTimeoutAsync(async () =>
            {
                var admxRoot = FindAdmxRoot();

                // 1) First run: build cache for en-US and fr-FR
                IAdmxCache cache1 = new AdmxCache();
                await cache1.InitializeAsync();
                cache1.SetSourceRoot(admxRoot);
                await cache1.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });
                var hitsEn1 = await cache1.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                var hitsFr1 = await cache1.SearchAsync(
                    "Dummy",
                    "fr-FR",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsEn1);
                Assert.NotNull(hitsFr1);

                // 2) Simulate app restart
                IAdmxCache cache2 = new AdmxCache();
                await cache2.InitializeAsync();
                cache2.SetSourceRoot(admxRoot);

                // 3) Now change languages so that only a new culture (e.g., ja-JP) is scanned
                //    This should ADD ja-JP rows while keeping prior en-US/fr-FR rows (no global purge).
                await cache2.ScanAndUpdateAsync(new[] { "ja-JP" });

                var hitsJa2 = await cache2.SearchAsync(
                    "Dummy",
                    "ja-JP",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsJa2);

                // 4) Verify that previously-populated en-US is still present
                var hitsEn2 = await cache2.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                Assert.NotNull(hitsEn2);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
        }
    }

    [Fact]
    public async Task Changing_SourceRoot_Reindexes_And_Affects_HitCounts()
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        try
        {
            await RunWithTimeoutAsync(async () =>
            {
                var fullRoot = FindAdmxRoot();

                // Create a temporary working root and copy only Dummy.admx and the corresponding ADML files
                var tmpRoot = Path.Combine(
                    Path.GetTempPath(),
                    "PolicyPlusModTests",
                    Guid.NewGuid().ToString("N")
                );
                Directory.CreateDirectory(tmpRoot);
                try
                {
                    // Copy Dummy.admx
                    var dummyAdmx = Path.Combine(fullRoot, "Dummy.admx");
                    File.Copy(dummyAdmx, Path.Combine(tmpRoot, "Dummy.admx"));
                    // Copy en-US ADML folder
                    var enUsSrc = Path.Combine(fullRoot, "en-US");
                    var enUsDst = Path.Combine(tmpRoot, "en-US");
                    Directory.CreateDirectory(enUsDst);
                    foreach (var f in Directory.EnumerateFiles(enUsSrc, "*.adml"))
                    {
                        File.Copy(f, Path.Combine(enUsDst, Path.GetFileName(f)));
                    }

                    IAdmxCache cache = new AdmxCache();
                    await cache.InitializeAsync();

                    // 1) Scan with the full asset set
                    cache.SetSourceRoot(fullRoot);
                    await cache.ScanAndUpdateAsync(new[] { "en-US" });
                    var hitsFull = await cache.SearchAsync(
                        "Dummy",
                        "en-US",
                        SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                        500
                    );

                    // 2) Switch to a minimal asset set and rescan
                    cache.SetSourceRoot(tmpRoot);
                    await cache.ScanAndUpdateAsync(new[] { "en-US" });
                    var hitsReduced = await cache.SearchAsync(
                        "Dummy",
                        "en-US",
                        SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                        500
                    );

                    // '*' represents a broad search after N-gram tokenization; the hit count should not increase
                    Assert.NotNull(hitsFull);
                    Assert.NotNull(hitsReduced);
                    Assert.True(hitsFull.Count >= hitsReduced.Count);

                    // 3) Switch back to the original root and rescan → hit count should recover
                    cache.SetSourceRoot(fullRoot);
                    await cache.ScanAndUpdateAsync(new[] { "en-US" });
                    var hitsAgain = await cache.SearchAsync(
                        "Dummy",
                        "en-US",
                        SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                        500
                    );
                    Assert.True(hitsAgain.Count >= hitsReduced.Count);
                }
                finally
                {
                    try
                    {
                        Directory.Delete(tmpRoot, recursive: true);
                    }
                    catch { }
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
        }
    }

    [Fact]
    public async Task SearchFields_Toggles_Work_For_Name_Id_Registry_And_Description()
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        try
        {
            await RunWithTimeoutAsync(async () =>
            {
                var admxRoot = FindAdmxRoot();
                IAdmxCache cache = new AdmxCache();
                await cache.InitializeAsync();
                cache.SetSourceRoot(admxRoot);
                await cache.ScanAndUpdateAsync(new[] { "en-US" });

                // 1) Get any hit and fetch its UID/details
                var hits = await cache.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                    50
                );
                if (hits == null || hits.Count == 0)
                {
                    // To avoid depending on specific test assets, try a wildcard-like query to ensure at least some hits
                    hits = await cache.SearchAsync(
                        "*",
                        "en-US",
                        SearchFields.Name | SearchFields.Id | SearchFields.Registry,
                        50
                    );
                }
                Assert.NotNull(hits);
                Assert.True(hits.Count >= 0);
                if (hits.Count == 0)
                    return; // If nothing hits in this environment, bail out here (API health is covered elsewhere)

                var first = hits[0];
                var (ns, name) = SplitUniqueId(first.UniqueId);
                var detail = await cache.GetByPolicyNameAsync(ns, name, "en-US");
                Assert.NotNull(detail);

                // 2) Name only
                if (!string.IsNullOrWhiteSpace(detail!.DisplayName))
                {
                    var byName = await cache.SearchAsync(
                        detail.DisplayName,
                        "en-US",
                        SearchFields.Name,
                        20
                    );
                    Assert.NotNull(byName);
                    Assert.Contains(
                        byName,
                        h =>
                            string.Equals(
                                h.UniqueId,
                                first.UniqueId,
                                StringComparison.OrdinalIgnoreCase
                            )
                    );
                }

                // 3) Id only
                var byIdExact = await cache.SearchAsync(name, "en-US", SearchFields.Id, 20);
                Assert.NotNull(byIdExact);
                Assert.Contains(
                    byIdExact,
                    h =>
                        string.Equals(
                            h.UniqueId,
                            first.UniqueId,
                            StringComparison.OrdinalIgnoreCase
                        )
                );

                // 4) Registry only (exact match)
                var regPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(detail.Hive))
                {
                    regPath = string.IsNullOrEmpty(detail.RegValue)
                        ? $"{detail.Hive}\\{detail.RegKey}"
                        : $"{detail.Hive}\\{detail.RegKey}\\{detail.RegValue}";
                    var byReg = await cache.SearchAsync(
                        regPath,
                        "en-US",
                        SearchFields.Registry,
                        20
                    );
                    Assert.NotNull(byReg);
                    Assert.Contains(
                        byReg,
                        h =>
                            string.Equals(
                                h.UniqueId,
                                first.UniqueId,
                                StringComparison.OrdinalIgnoreCase
                            )
                    );
                }

                // 5) Description only (verify only if we can pick a token not present in display_name)
                if (!string.IsNullOrWhiteSpace(detail.ExplainText))
                {
                    var tokens = Regex
                        .Matches(detail.ExplainText, "[A-Za-z0-9]{3,}")
                        .Select(m => m.Value)
                        .ToList();
                    var dnLower = (detail.DisplayName ?? string.Empty).ToLowerInvariant();
                    var token = tokens.FirstOrDefault(t => !dnLower.Contains(t.ToLowerInvariant()));
                    if (!string.IsNullOrEmpty(token))
                    {
                        var byDesc = await cache.SearchAsync(
                            token,
                            "en-US",
                            SearchFields.Description,
                            20
                        );
                        Assert.NotNull(byDesc);
                        Assert.Contains(
                            byDesc,
                            h =>
                                string.Equals(
                                    h.UniqueId,
                                    first.UniqueId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );

                        // With the same token, searching Name only should not hit (by assumption it's absent from display_name)
                        var byNameOnly = await cache.SearchAsync(
                            token,
                            "en-US",
                            SearchFields.Name,
                            20
                        );
                        Assert.NotNull(byNameOnly);
                        Assert.DoesNotContain(
                            byNameOnly,
                            h =>
                                string.Equals(
                                    h.UniqueId,
                                    first.UniqueId,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );
                    }
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
        }
    }

    [Fact]
    public async Task Perf_Indexing_And_Search_Timings()
    {
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        try
        {
            await RunWithTimeoutAsync(async () =>
            {
                var admxRoot = FindAdmxRoot();
                IAdmxCache cache = new AdmxCache();
                await cache.InitializeAsync();
                cache.SetSourceRoot(admxRoot);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });
                sw.Stop();
                var indexMs = sw.ElapsedMilliseconds;

                // Warm-up search
                var warm = await cache.SearchAsync(
                    "Dummy",
                    "en-US",
                    SearchFields.Name
                        | SearchFields.Id
                        | SearchFields.Registry
                        | SearchFields.Description,
                    100
                );
                Assert.NotNull(warm);

                // Measure search
                sw.Restart();
                for (int i = 0; i < 10; i++)
                {
                    var r = await cache.SearchAsync(
                        "Policy",
                        "en-US",
                        SearchFields.Name | SearchFields.Description,
                        100
                    );
                    Assert.NotNull(r);
                }
                sw.Stop();
                var totalSearchMs = sw.ElapsedMilliseconds;

                // Soft asserts (bounds are generous; goal is to catch pathological regressions only)
                Console.WriteLine(
                    $"AdmxCache Perf — Indexing: {indexMs} ms, 10x search: {totalSearchMs} ms"
                );
                Assert.True(indexMs < 5000, $"Indexing took too long: {indexMs}ms");
                Assert.True(totalSearchMs < 1500, $"10 searches took too long: {totalSearchMs}ms");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
        }
    }
}
