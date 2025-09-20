using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PolicyPlusCore.Core;

public interface IAdmxCache
{
    Task InitializeAsync(CancellationToken ct = default);
    Task ScanAndUpdateAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        int limit = 50,
        CancellationToken ct = default
    );

    Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        string culture,
        CancellationToken ct = default
    );
    Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        string culture,
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
