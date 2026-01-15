using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal sealed class AdmxCachePolicyQueryService
{
    private readonly AdmxCacheStore _store;

    public AdmxCachePolicyQueryService(AdmxCacheStore store)
    {
        _store = store;
    }

    public async Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        string culture,
        CancellationToken ct
    )
    {
        culture = AdmxCacheCulture.NormalizeCultureName(culture);
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        const string sql =
            @"SELECT p.id, @culture AS culture, p.ns, p.policy_name, s.display_name, s.explain_text,
 s.category_path, p.hive, p.reg_key, p.reg_value, p.value_type, s.presentation_json,
 p.supported_min, p.supported_max, p.deprecated, p.product_hint
FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
WHERE p.ns=@ns AND p.policy_name=@name LIMIT 1";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@ns", ns);
        cmd.Parameters.AddWithValue("@name", policyName);
        cmd.Parameters.AddWithValue("@culture", culture);
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await rdr.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return MapDetail(rdr);
    }

    public async Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        IReadOnlyList<string> cultures,
        CancellationToken ct
    )
    {
        if (cultures == null || cultures.Count == 0)
            return await GetByPolicyNameAsync(ns, policyName, CultureInfo.CurrentUICulture.Name, ct)
                .ConfigureAwait(false);
        if (cultures.Count == 1)
            return await GetByPolicyNameAsync(ns, policyName, cultures[0], ct)
                .ConfigureAwait(false);

        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine("WITH CulturePref AS (");
        for (int i = 0; i < cultures.Count; i++)
        {
            if (i > 0)
                sb.AppendLine("UNION ALL");
            sb.Append("SELECT ")
                .Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(" AS prio, @c")
                .Append(i)
                .Append(" AS culture")
                .AppendLine();
        }
        sb.AppendLine(
            ") SELECT p.id, s.culture, p.ns, p.policy_name, s.display_name, s.explain_text, s.category_path, p.hive, p.reg_key, p.reg_value, p.value_type, s.presentation_json, p.supported_min, p.supported_max, p.deprecated, p.product_hint FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id JOIN CulturePref cp ON cp.culture=s.culture WHERE p.ns=@ns AND p.policy_name=@name ORDER BY cp.prio ASC LIMIT 1;"
        );
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@ns", ns);
        cmd.Parameters.AddWithValue("@name", policyName);
        for (int i = 0; i < cultures.Count; i++)
            cmd.Parameters.AddWithValue(
                "@c" + i,
                AdmxCacheCulture.NormalizeCultureName(cultures[i])
            );
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await rdr.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return MapDetail(rdr);
    }

    public async Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        string culture,
        CancellationToken ct
    )
    {
        culture = AdmxCacheCulture.NormalizeCultureName(culture);
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        const string sql =
            @"SELECT p.id, @culture AS culture, p.ns, p.policy_name, s.display_name, s.explain_text,
 s.category_path, p.hive, p.reg_key, p.reg_value, p.value_type, s.presentation_json,
 p.supported_min, p.supported_max, p.deprecated, p.product_hint
FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
WHERE (p.hive||'\\'||p.reg_key||'\\'||p.reg_value) = @rp LIMIT 1";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@rp", registryPath);
        cmd.Parameters.AddWithValue("@culture", culture);
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await rdr.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return MapDetail(rdr);
    }

    public async Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        IReadOnlyList<string> cultures,
        CancellationToken ct
    )
    {
        if (cultures == null || cultures.Count == 0)
            return await GetByRegistryPathAsync(registryPath, CultureInfo.CurrentUICulture.Name, ct)
                .ConfigureAwait(false);
        if (cultures.Count == 1)
            return await GetByRegistryPathAsync(registryPath, cultures[0], ct)
                .ConfigureAwait(false);

        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine("WITH CulturePref AS (");
        for (int i = 0; i < cultures.Count; i++)
        {
            if (i > 0)
                sb.AppendLine("UNION ALL");
            sb.Append("SELECT ")
                .Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(" AS prio, @c")
                .Append(i)
                .Append(" AS culture")
                .AppendLine();
        }
        sb.AppendLine(
            ") SELECT p.id, s.culture, p.ns, p.policy_name, s.display_name, s.explain_text, s.category_path, p.hive, p.reg_key, p.reg_value, p.value_type, s.presentation_json, p.supported_min, p.supported_max, p.deprecated, p.product_hint FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id JOIN CulturePref cp ON cp.culture=s.culture WHERE (p.hive||'\\' || p.reg_key || '\\' || p.reg_value)=@rp ORDER BY cp.prio ASC LIMIT 1;"
        );
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@rp", registryPath);
        for (int i = 0; i < cultures.Count; i++)
            cmd.Parameters.AddWithValue(
                "@c" + i,
                AdmxCacheCulture.NormalizeCultureName(cultures[i])
            );
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await rdr.ReadAsync(ct).ConfigureAwait(false))
            return null;
        return MapDetail(rdr);
    }

    public async Task<IReadOnlyCollection<string>> GetPolicyUniqueIdsByCategoriesAsync(
        IEnumerable<string> categoryKeys,
        CancellationToken ct
    )
    {
        if (categoryKeys is null)
            return Array.Empty<string>();
        var list = categoryKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
            return Array.Empty<string>();

        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.Append("SELECT ns||':'||policy_name AS uid FROM Policies WHERE ");
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0)
                sb.Append(" OR ");
            sb.Append("LOWER(category_key) = LOWER(@k");
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            sb.Append(")");
        }
        sb.Append(";");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        for (int i = 0; i < list.Count; i++)
        {
            cmd.Parameters.AddWithValue("@k" + i.ToString(CultureInfo.InvariantCulture), list[i]);
        }
        var result = new List<string>(256);
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!rdr.IsDBNull(0))
                result.Add(rdr.GetString(0));
        }
        return result;
    }

    private static PolicyDetail MapDetail(SqliteDataReader rdr)
    {
        var policyId = rdr.GetInt64(0);
        var culture = rdr.GetString(1);
        var ns = rdr.GetString(2);
        var policyName = rdr.GetString(3);
        var displayName = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
        var explain = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5);
        var catPath = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6);
        var hive = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7);
        var regKey = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8);
        var regVal = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9);
        var vtype = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10);
        string? presJson = rdr.IsDBNull(11) ? null : Encoding.UTF8.GetString((byte[])rdr[11]);
        var smin = rdr.IsDBNull(12) ? null : rdr.GetString(12);
        var smax = rdr.IsDBNull(13) ? null : rdr.GetString(13);
        var deprecated = !rdr.IsDBNull(14) && rdr.GetInt32(14) != 0;
        var productHint = rdr.IsDBNull(15) ? string.Empty : rdr.GetString(15);
        return new PolicyDetail(
            policyId,
            culture,
            ns,
            policyName,
            displayName,
            explain,
            catPath,
            hive,
            regKey,
            regVal,
            vtype,
            presJson,
            smin,
            smax,
            deprecated,
            productHint
        );
    }
}
