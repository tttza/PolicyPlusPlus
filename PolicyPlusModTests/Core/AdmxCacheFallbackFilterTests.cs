using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core;

// Tests for fallback filtering logic: second language always allowed, fallback (OS/en-US) only when primary missing.
[Collection("AdmxCache.Isolated")]
public class AdmxCacheFallbackFilterTests
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

    private static (string ns, string name) Split(string uid)
    {
        var idx = uid.IndexOf(':');
        if (idx <= 0)
            return (string.Empty, uid);
        return (uid[..idx], uid[(idx + 1)..]);
    }

    // Helper: perform multi-culture search with given ordered cultures (primary first, second second) and return hit cultures keyed by unique id.
    private static async Task<Dictionary<string, string>> MultiSearchAsync(
        IAdmxCache cache,
        string query,
        string[] orderedCultures
    )
    {
        var hits = await cache.SearchAsync(
            query,
            orderedCultures,
            SearchFields.Name | SearchFields.Id | SearchFields.Registry | SearchFields.Description,
            200
        );
        return hits.ToDictionary(h => h.UniqueId, h => h.Culture, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrimaryPresent_FiltersOut_FallbackCultures_ForThatPolicy()
    {
        var root = FindAdmxRoot();
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        cache.SetSourceRoot(root);

        // Scan several cultures; order matters: primary=en-US, second=fr-FR, then possible fallbacks (ja-JP assumed present for some policies or might be absent).
        await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR", "ja-JP" });

        // Broad query likely to hit multiple policies ("Dummy" used in existing tests/assets)
        var ordered = new[] { "en-US", "fr-FR", "ja-JP", "en-US" }; // duplicate en-US at end is harmless; distinct handled in service/UI normally.
        var hits = await cache.SearchAsync(
            "Dummy",
            ordered,
            SearchFields.Name | SearchFields.Id | SearchFields.Registry | SearchFields.Description,
            300
        );
        Assert.NotNull(hits);

        // Pick at most 5 policies that have en-US culture
        var primaryHitPolicies = hits.Where(h =>
                string.Equals(h.Culture, "en-US", StringComparison.OrdinalIgnoreCase)
            )
            .Take(5)
            .ToList();
        foreach (var ph in primaryHitPolicies)
        {
            // Re-run a targeted query using a term from the policy id (name part) and see that ja-JP (fallback after primary) is not chosen.
            var uid = ph.UniqueId;
            var (ns, name) = Split(uid);
            var idQuery = name; // ID exact search path
            var small = await cache.SearchAsync(idQuery, ordered, SearchFields.Id, 10);
            if (small.Count == 0)
                continue; // Skip if nothing
            var chosen = small.First(h =>
                string.Equals(h.UniqueId, uid, StringComparison.OrdinalIgnoreCase)
            );
            Assert.Equal("en-US", chosen.Culture); // Should stay primary, not fallback culture
        }
    }

    [Fact]
    public async Task MissingPrimary_Allows_FallbackCulture()
    {
        var root = FindAdmxRoot();
        // Use a fresh isolated cache directory so prior tests that scanned en-US do not leak en-US rows.
        var tmp = Path.Combine(
            Path.GetTempPath(),
            "PolicyPlusModTests",
            "Cache",
            "isolated-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tmp);
        var prev = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_DIR");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", tmp);
        try
        {
            IAdmxCache cache = new AdmxCache();
            await cache.InitializeAsync();
            cache.SetSourceRoot(root);

            // First only fr-FR + ja-JP (simulate primary missing scenario if we later search with primary=en-US)
            await cache.ScanAndUpdateAsync(new[] { "fr-FR", "ja-JP" });

            // Search with a primary that we did NOT scan (en-US), second=fr-FR, fallback candidates=ja-JP,en-US.
            var ordered = new[] { "en-US", "fr-FR", "ja-JP", "en-US" }; // en-US first (missing), fr-FR second (exists), ja-JP fallback.
            var hits = await cache.SearchAsync(
                "Dummy",
                ordered,
                SearchFields.Name
                    | SearchFields.Id
                    | SearchFields.Registry
                    | SearchFields.Description,
                200
            );
            Assert.NotNull(hits);

            // At least one hit should have culture fr-FR (second allowed though primary missing)
            Assert.Contains(hits, h => h.Culture == "fr-FR");
            // Because we never scanned en-US in this isolated cache, no en-US hits should appear.
            Assert.DoesNotContain(hits, h => h.Culture == "en-US");
        }
        finally
        {
            // Restore environment variable and cleanup
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", prev);
            // Release pooled handles so the temp cache directory can be deleted on Windows.
            AdmxCacheRuntime.ReleaseSqliteHandles();
            try
            {
                Directory.Delete(tmp, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task SecondLanguage_Always_Allowed_When_Present()
    {
        var root = FindAdmxRoot();
        IAdmxCache cache = new AdmxCache();
        await cache.InitializeAsync();
        cache.SetSourceRoot(root);
        await cache.ScanAndUpdateAsync(new[] { "en-US", "fr-FR" });

        // Search with primary=en-US, second=fr-FR
        var ordered = new[] { "en-US", "fr-FR", "en-US" };
        var hits = await cache.SearchAsync(
            "Dummy",
            ordered,
            SearchFields.Name | SearchFields.Id | SearchFields.Registry | SearchFields.Description,
            100
        );
        Assert.NotNull(hits);
        // If any fr-FR row exists it should show up with culture fr-FR for at least one policy even when en-US also exists.
        if (hits.Any(h => h.Culture == "fr-FR"))
        {
            // Pick a fr-FR hit and ensure an ID-only search still allows fr-FR
            var frHit = hits.First(h => h.Culture == "fr-FR");
            var (ns, name) = Split(frHit.UniqueId);
            var idOnly = await cache.SearchAsync(name, ordered, SearchFields.Id, 10);
            Assert.Contains(idOnly, h => h.Culture == "fr-FR" && h.UniqueId == frHit.UniqueId);
        }
    }
}
