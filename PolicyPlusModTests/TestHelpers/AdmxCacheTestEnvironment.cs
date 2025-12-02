using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PolicyPlusModTests.TestHelpers;

internal static class AdmxCacheTestEnvironment
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private static string? _cacheDir;

    public static string CacheDir
    {
        get
        {
            lock (Gate)
            {
                EnsureInitialized();
                return _cacheDir!;
            }
        }
    }

    [ModuleInitializer]
    public static void InitializeModule() => EnsureInitialized();

    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_initialized)
                return;

            _cacheDir ??= Path.Combine(
                Path.GetTempPath(),
                "PolicyPlusModTests",
                "Cache",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(_cacheDir);

            SetEnv("POLICYPLUS_CACHE_DIR", _cacheDir);
            SetEnv("POLICYPLUS_CACHE_FAST", "1");
            SetEnv("POLICYPLUS_CACHE_DISABLE_MAINT", "1");
            SetEnv("POLICYPLUS_CACHE_TRACE", "1");
            SetEnv("POLICYPLUS_CACHE_ONLY_FILES", "Dummy.admx");
            SetEnv("POLICYPLUS_CACHE_MAX_POLICIES", "200");
            SetEnv("POLICYPLUS_CACHE_DISABLE_WRITER_LOCK", "1");
            SetEnv("POLICYPLUS_CACHE_FAST_BUSY_MS", "500");
            SetEnv("POLICYPLUS_CACHE_WRITER_TIMEOUT_MS", "200");

            _initialized = true;
        }
    }

    public static void Cleanup()
    {
        lock (Gate)
        {
            if (!_initialized)
                return;

            var dir = _cacheDir;
            _cacheDir = null;
            _initialized = false;

            SetEnv("POLICYPLUS_CACHE_DIR", null);
            SetEnv("POLICYPLUS_CACHE_FAST", null);
            SetEnv("POLICYPLUS_CACHE_DISABLE_MAINT", null);
            SetEnv("POLICYPLUS_CACHE_TRACE", null);
            SetEnv("POLICYPLUS_CACHE_ONLY_FILES", null);
            SetEnv("POLICYPLUS_CACHE_MAX_POLICIES", null);
            SetEnv("POLICYPLUS_CACHE_DISABLE_WRITER_LOCK", null);
            SetEnv("POLICYPLUS_CACHE_FAST_BUSY_MS", null);
            SetEnv("POLICYPLUS_CACHE_WRITER_TIMEOUT_MS", null);

            try
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch { }
        }
    }

    private static void SetEnv(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value);
}
