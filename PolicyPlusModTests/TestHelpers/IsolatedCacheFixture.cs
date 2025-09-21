using System;
using System.IO;

namespace PolicyPlusModTests.TestHelpers;

// xUnit shared fixture to isolate AdmxCache SQLite location per test collection.
public sealed class IsolatedCacheFixture : IDisposable
{
    public string CacheDir { get; }

    public IsolatedCacheFixture()
    {
        CacheDir = Path.Combine(Path.GetTempPath(), "PolicyPlusModTests", "Cache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(CacheDir);
        Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", CacheDir);
    }

    public void Dispose()
    {
        try
        {
            Environment.SetEnvironmentVariable("POLICYPLUS_CACHE_DIR", null);
            if (Directory.Exists(CacheDir)) Directory.Delete(CacheDir, recursive: true);
        }
        catch { }
    }
}
