using System;
using System.IO;

namespace PolicyPlusModTests.TestHelpers;

// xUnit shared fixture to isolate AdmxCache SQLite location per test collection.
public sealed class IsolatedCacheFixture : IDisposable
{
    public string CacheDir { get; }

    public IsolatedCacheFixture()
    {
        CacheDir = Path.Combine(
            Path.GetTempPath(),
            "PolicyPlusModTests",
            "Cache",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(CacheDir);
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", CacheDir);
        // Enable fast cache mode (short SQLite timeouts, skip heavy maintenance) for unit tests.
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_FAST", "1");
        // Disable maintenance (optimize/vacuum/compact) entirely during unit tests for speed.
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DISABLE_MAINT", "1");
        // Enable tracing for high-level AdmxCache timings (root cause profiling A).
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_TRACE", "1");
        // Limit loaded ADMX files in tests to keep indexing fast.
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", "200");
        // Disable cross-process writer mutex to prevent sporadic 5-10s waits when tests run in parallel
        // with other processes using the cache (speeds up flaky timing scenarios).
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DISABLE_WRITER_LOCK", "1");
        // Aggressively lower SQLite busy timeout to surface real deadlocks quickly while avoiding 5s waits.
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_FAST_BUSY_MS", "500");
    }

    public void Dispose()
    {
        try
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_FAST", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DISABLE_MAINT", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_TRACE", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DISABLE_WRITER_LOCK", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_FAST_BUSY_MS", null);
            if (Directory.Exists(CacheDir))
                Directory.Delete(CacheDir, recursive: true);
        }
        catch { }
    }
}
