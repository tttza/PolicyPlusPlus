using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PolicyPlusCore.Core;

namespace PolicyPlusModTests.TestHelpers;

internal static class AdmxCacheTestEnvironment
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private static string? _cacheDir;
    private static string? _dbPath;

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
        string? dbToPrime = null;
        lock (Gate)
        {
            if (!_initialized)
            {
                _cacheDir ??= Path.Combine(
                    Path.GetTempPath(),
                    "PolicyPlusModTests",
                    "Cache",
                    Guid.NewGuid().ToString("N")
                );
                Directory.CreateDirectory(_cacheDir);
                _dbPath = Path.Combine(_cacheDir, "admxcache.sqlite");

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
            else if (!string.IsNullOrWhiteSpace(_cacheDir) && !Directory.Exists(_cacheDir))
            {
                Directory.CreateDirectory(_cacheDir);
            }

            if (string.IsNullOrWhiteSpace(_dbPath) && !string.IsNullOrWhiteSpace(_cacheDir))
            {
                _dbPath = Path.Combine(_cacheDir, "admxcache.sqlite");
            }

            if (!string.IsNullOrWhiteSpace(_dbPath) && !File.Exists(_dbPath))
            {
                dbToPrime = _dbPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(dbToPrime))
        {
            PrimeCache(dbToPrime!);
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
            _dbPath = null;

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

    public static async Task RunWithScopedCacheAsync(Func<Task> body, string? onlyFiles = null)
    {
        EnsureInitialized();
        var scopedDir = Path.Combine(
            Path.GetTempPath(),
            "PolicyPlusModTests",
            "Cache",
            "scoped-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(scopedDir);
        var overrides = new List<(string Key, string? Value)>
        {
            ("POLICYPLUS_CACHE_DIR", scopedDir),
            ("POLICYPLUS_CACHE_MAX_POLICIES", "200"),
            ("POLICYPLUS_CACHE_FAST", "1"),
            ("POLICYPLUS_CACHE_DISABLE_MAINT", "1"),
            ("POLICYPLUS_CACHE_TRACE", "1"),
            ("POLICYPLUS_CACHE_DISABLE_WRITER_LOCK", "1"),
            ("POLICYPLUS_CACHE_FAST_BUSY_MS", "500"),
            ("POLICYPLUS_CACHE_WRITER_TIMEOUT_MS", "200"),
        };
        if (onlyFiles != null)
        {
            overrides.Add(("POLICYPLUS_CACHE_ONLY_FILES", onlyFiles));
        }

        var priorValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            priorValues[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            await body().ConfigureAwait(false);
        }
        finally
        {
            foreach (var (key, _) in overrides)
            {
                priorValues.TryGetValue(key, out var prior);
                Environment.SetEnvironmentVariable(key, prior);
            }

            AdmxCacheRuntime.ReleaseSqliteHandles();
            try
            {
                if (Directory.Exists(scopedDir))
                    Directory.Delete(scopedDir, recursive: true);
            }
            catch { }
        }
    }

    private static void SetEnv(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value);

    private static void PrimeCache(string path)
    {
        // Serialize priming so multiple tests do not race to create the DB file.
        lock (Gate)
        {
            if (File.Exists(path))
                return;

            try
            {
                var cache = new AdmxCache();
                cache.InitializeAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore priming failures; tests will surface real errors if initialization fails.
            }
        }
    }
}
