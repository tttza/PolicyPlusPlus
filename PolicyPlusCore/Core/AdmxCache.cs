using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core.Caching;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core;

public sealed partial class AdmxCache : IAdmxCache
{
    private readonly AdmxCacheStore _store;
    private readonly AdmxCacheFileUsageStore _fileUsageStore;
    private readonly AdmxCachePolicyQueryService _policyQueryService;
    private string? _sourceRoot;

    internal SqliteConnection OpenStoreConnection() => _store.OpenConnection();

    private static void TraceFileUsageWarning(string message, Exception ex)
    {
        var formatted = $"[AdmxCache] WARN {message}: {ex.GetType().Name}: {ex.Message}";
        try
        {
            Debug.WriteLine(formatted);
        }
        catch { }
        try
        {
            Console.Error.WriteLine(formatted);
        }
        catch { }
    }

    public AdmxCache()
    {
        var overrideDir = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_DIR");
        string dbPath;
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            try
            {
                Directory.CreateDirectory(overrideDir!);
            }
            catch { }
            dbPath = Path.Combine(overrideDir!, "admxcache.sqlite");
        }
        else
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = Path.Combine(baseDir, "PolicyPlusPlus", "Cache");
            Directory.CreateDirectory(cacheDir);
            dbPath = Path.Combine(cacheDir, "admxcache.sqlite");
        }
        _store = new AdmxCacheStore(dbPath);
        _fileUsageStore = new AdmxCacheFileUsageStore(_store);
        _policyQueryService = new AdmxCachePolicyQueryService(_store);
    }

    public Task InitializeAsync(CancellationToken ct = default) =>
        AdmxCacheInitializationService.InitializeAsync(_store, _fileUsageStore, ct);

    public void SetSourceRoot(string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            _sourceRoot = null;
            return;
        }
        _sourceRoot = Path.GetFullPath(baseDirectory);
    }

    public async Task ScanAndUpdateAsync(CancellationToken ct = default)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        await ScanAndUpdateAsync(new[] { culture }, ct).ConfigureAwait(false);
    }

    public Task ScanAndUpdateAsync(IEnumerable<string> cultures, CancellationToken ct = default) =>
        AdmxCacheScanService.ScanAndUpdateAsync(this, cultures, ct);

    internal async Task ScanAndUpdateCoreAsync(IEnumerable<string> cultures, CancellationToken ct)
    {
        using var _traceScan = AdmxCacheTrace.Scope("ScanAndUpdateAsync.Total");
        await AdmxCacheScanOrchestrationService
            .ScanAndUpdateCoreAsync(
                _store,
                _fileUsageStore,
                _sourceRoot,
                cultures,
                AdmxCacheTrace.Scope,
                (m, ex) => TraceFileUsageWarning(m, ex),
                (bundle, culture, allowGlobal, token) =>
                    DiffAndApplyAsync(bundle, culture, allowGlobal, token),
                ct
            )
            .ConfigureAwait(false);
    }

    public async Task<int> PurgeStaleCacheEntriesAsync(
        TimeSpan olderThan,
        CancellationToken ct = default
    )
    {
        return await _fileUsageStore
            .PurgeStaleCacheEntriesAsync(olderThan, ct)
            .ConfigureAwait(false);
    }

    private Task DiffAndApplyAsync(
        AdmxBundle bundle,
        string culture,
        bool allowGlobalRebuild,
        CancellationToken ct
    )
    {
        return AdmxCacheApplyService.DiffAndApplyAsync(
            _store,
            bundle,
            culture,
            allowGlobalRebuild,
            ct,
            InferValueType,
            BuildCategoryPath,
            GetRegistryPath
        );
    }

    private static string GetRegistryPath(string hive, string? key, string? value)
    {
        var k = key ?? string.Empty;
        var v = value ?? string.Empty;
        return !string.IsNullOrEmpty(v) ? $"{hive}\\{k}\\{v}" : $"{hive}\\{k}";
    }

    private static string InferValueType(AdmxPolicy raw)
    {
        if (raw.Elements is { Count: > 0 })
            return string.Join('+', raw.Elements.Select(e => e.GetType().Name));
        var reg = raw.AffectedValues;
        var parts = new List<string>(2);
        if (reg.OnValue is not null)
            parts.Add(reg.OnValue.RegistryType.ToString());
        if (reg.OffValue is not null)
            parts.Add(reg.OffValue.RegistryType.ToString());
        if (reg.OnValueList is not null || reg.OffValueList is not null)
            parts.Add("List");
        return parts.Count > 0 ? string.Join('+', parts) : "Flag";
    }

    private static string BuildCategoryPath(PolicyPlusCategory? cat)
    {
        if (cat is null)
            return string.Empty;
        var stack = new Stack<string>();
        var cur = cat;
        while (cur is not null)
        {
            if (!string.IsNullOrEmpty(cur.DisplayName))
                stack.Push(cur.DisplayName);
            cur = cur.Parent;
        }
        return string.Join("/", stack);
    }

    // Upsert moved to AdmxCachePolicyUpsertService (ADR 0004).

    public Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        int limit = 50,
        CancellationToken ct = default
    )
    {
        var fields = SearchFields.Name | SearchFields.Id | SearchFields.Registry;
        return SearchAsync(query, culture, fields, limit, ct);
    }

    public async Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        IReadOnlyList<string> cultures,
        SearchFields fields,
        int limit = 50,
        CancellationToken ct = default
    ) =>
        await SearchAsync(query, cultures, fields, andMode: false, limit, ct).ConfigureAwait(false);

    public Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        IReadOnlyList<string> cultures,
        SearchFields fields,
        bool andMode,
        int limit = 50,
        CancellationToken ct = default
    ) => AdmxCacheSearchService.SearchAsync(this, query, cultures, fields, andMode, limit, ct);

    public Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        bool includeDescription,
        int limit = 50,
        CancellationToken ct = default
    )
    {
        var fields = SearchFields.Name | SearchFields.Id | SearchFields.Registry;
        if (includeDescription)
            fields |= SearchFields.Description;
        return SearchAsync(query, new[] { culture }, fields, andMode: false, limit, ct);
    }

    public Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        SearchFields fields,
        int limit = 50,
        CancellationToken ct = default
    ) => SearchAsync(query, new[] { culture }, fields, andMode: false, limit, ct);

    public async Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        string culture,
        CancellationToken ct = default
    )
    {
        return await _policyQueryService
            .GetByPolicyNameAsync(ns, policyName, culture, ct)
            .ConfigureAwait(false);
    }

    public async Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        IReadOnlyList<string> cultures,
        CancellationToken ct = default
    )
    {
        return await _policyQueryService
            .GetByPolicyNameAsync(ns, policyName, cultures, ct)
            .ConfigureAwait(false);
    }

    public async Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        string culture,
        CancellationToken ct = default
    )
    {
        return await _policyQueryService
            .GetByRegistryPathAsync(registryPath, culture, ct)
            .ConfigureAwait(false);
    }

    public async Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        IReadOnlyList<string> cultures,
        CancellationToken ct = default
    )
    {
        return await _policyQueryService
            .GetByRegistryPathAsync(registryPath, cultures, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetPolicyUniqueIdsByCategoriesAsync(
        IEnumerable<string> categoryKeys,
        CancellationToken ct = default
    )
    {
        return await _policyQueryService
            .GetPolicyUniqueIdsByCategoriesAsync(categoryKeys, ct)
            .ConfigureAwait(false);
    }
}
