using System;
using PolicyPlusCore.Core;

namespace PolicyPlusModTests.TestHelpers;

// xUnit shared fixture to isolate AdmxCache SQLite location per test collection.
public sealed class IsolatedCacheFixture : IDisposable
{
    public string CacheDir { get; }

    public IsolatedCacheFixture()
    {
        AdmxCacheTestEnvironment.EnsureInitialized();
        CacheDir = AdmxCacheTestEnvironment.CacheDir;
    }

    public void Dispose()
    {
        AdmxCacheRuntime.ReleaseSqliteHandles();
        AdmxCacheTestEnvironment.Cleanup();
    }
}
