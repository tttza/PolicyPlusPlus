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
    }

    public void Dispose()
    {
        try
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_FAST", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DISABLE_MAINT", null);
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_TRACE", null);
            if (Directory.Exists(CacheDir))
                Directory.Delete(CacheDir, recursive: true);
        }
        catch { }
    }
}
