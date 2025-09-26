using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PolicyPlusCore.Core;

// Controls which fields participate in cache-backed search.
[System.Flags]
public enum SearchFields
{
    None = 0,
    Name = 1 << 0, // Display name (title_* columns)
    Description = 1 << 1, // Explain text (desc_* columns)
    Id = 1 << 2, // Policy raw ID / namespace via tags
    Registry = 1 << 3, // Registry path (hive/key/value) via registry_path tokens
}

public interface IAdmxCache
{
    Task InitializeAsync(CancellationToken ct = default);

    // Sets the base directory where ADMX/ADML files are loaded from. If null or empty, implementation uses defaults (e.g., %WINDIR%\PolicyDefinitions).
    void SetSourceRoot(string? baseDirectory);
    Task ScanAndUpdateAsync(CancellationToken ct = default);
    Task ScanAndUpdateAsync(IEnumerable<string> cultures, CancellationToken ct = default);

    Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        int limit = 50,
        CancellationToken ct = default
    );

    // Searches the cache with control over whether description text participates in FTS.
    // When includeDescription is false, only title (and non-FTS exact/prefix fields) are considered.
    Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        bool includeDescription,
        int limit = 50,
        CancellationToken ct = default
    );

    // Searches the cache with fine-grained control over participating fields.
    Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        SearchFields fields,
        int limit = 50,
        CancellationToken ct = default
    );

    // Multi-culture search: returns hits resolved using the first culture in cultures that has data; no per-hit mixing.
    // Implementation should attempt each culture in order until results found (or all exhausted).
    Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        IReadOnlyList<string> cultures,
        SearchFields fields,
        int limit = 50,
        CancellationToken ct = default
    );

    // Search with optional AND mode (all tokens must match). When andMode=true, only policies where every token appears in at least one selected field are returned.
    Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        IReadOnlyList<string> cultures,
        SearchFields fields,
        bool andMode,
        int limit = 50,
        CancellationToken ct = default
    );

    Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        string culture,
        CancellationToken ct = default
    );

    // Multi-culture variant: attempts cultures in order; returns first found detail (no merging of fields).
    Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        IReadOnlyList<string> cultures,
        CancellationToken ct = default
    );
    Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        string culture,
        CancellationToken ct = default
    );

    // Multi-culture variant for registry path lookup.
    Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        IReadOnlyList<string> cultures,
        CancellationToken ct = default
    );

    // Returns policy UniqueIds (ns:policy_name) whose category_key is any of the provided categoryKeys.
    // Caller should pass category UniqueIDs; include subcategories by passing all descendant category keys.
    Task<IReadOnlyCollection<string>> GetPolicyUniqueIdsByCategoriesAsync(
        IEnumerable<string> categoryKeys,
        CancellationToken ct = default
    );
}

public sealed record PolicyHit(
    long PolicyId,
    string Culture,
    string UniqueId,
    string DisplayName,
    string RegistryPath,
    string ProductHint,
    string ValueType,
    double Score
);

public sealed record PolicyDetail(
    long PolicyId,
    string Culture,
    string Namespace,
    string PolicyName,
    string DisplayName,
    string ExplainText,
    string CategoryPath,
    string Hive,
    string RegKey,
    string RegValue,
    string ValueType,
    string? PresentationJson,
    string? SupportedMin,
    string? SupportedMax,
    bool Deprecated,
    string ProductHint
);
