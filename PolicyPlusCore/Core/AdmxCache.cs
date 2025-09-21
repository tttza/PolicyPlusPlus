using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Admx;
using PolicyPlusCore.IO;
using PolicyPlusCore.Utilities;

namespace PolicyPlusCore.Core;

public sealed class AdmxCache : IAdmxCache
{
    private readonly AdmxCacheStore _store;
    private string? _sourceRoot;

    public AdmxCache()
    {
        // Allow tests or advanced scenarios to override cache location via env var.
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
            dbPath = Path.Combine(cacheDir, "admxcache.sqlite");
        }
        _store = new AdmxCacheStore(dbPath);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _store.InitializeAsync(ct).ConfigureAwait(false);
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await _store.OptimizeAsync(conn, ct).ConfigureAwait(false);
    }

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

    public async Task ScanAndUpdateAsync(
        IEnumerable<string> cultures,
        CancellationToken ct = default
    )
    {
        string root =
            _sourceRoot ?? Environment.ExpandEnvironmentVariables(@"%WINDIR%\PolicyDefinitions");
        if (!Directory.Exists(root))
            return;

        var cultureList =
            cultures?.ToList() ?? new List<string> { CultureInfo.CurrentUICulture.Name };
        if (cultureList.Count == 0)
            cultureList.Add(CultureInfo.CurrentUICulture.Name);

        // Decide whether to perform a global rebuild based on source root changes.
        bool needGlobalRebuild = await NeedsGlobalRebuildAsync(root, ct).ConfigureAwait(false);

        // If a global rebuild is required (e.g., legacy DB without meta), include any cultures already present
        // in the DB so we don't lose them during the rebuild, even if the caller only asked for a subset.
        if (needGlobalRebuild)
        {
            try
            {
                var existing = await GetExistingCulturesAsync(ct).ConfigureAwait(false);
                if (existing.Count > 0)
                {
                    var set = new HashSet<string>(cultureList, StringComparer.OrdinalIgnoreCase);
                    foreach (var ec in existing)
                    {
                        if (!set.Contains(ec))
                            cultureList.Add(ec);
                    }
                }
            }
            catch { }
        }
        bool didGlobalRebuild = false;

        int i = 0;
        foreach (var cul in cultureList.Select(NormalizeCultureName))
        {
            var bundle = new AdmxBundle();
            try
            {
                foreach (
                    var admx in Directory.EnumerateFiles(
                        root,
                        "*.admx",
                        SearchOption.TopDirectoryOnly
                    )
                )
                {
                    _ = bundle.LoadFile(admx, cul);
                }
            }
            catch
            {
                // Ignore malformed files during scan; individual failures are non-fatal for the cache build.
            }

            bool allowGlobalRebuild = needGlobalRebuild && (i == 0);
            if (allowGlobalRebuild)
                didGlobalRebuild = true;
            await DiffAndApplyAsync(bundle, cul, allowGlobalRebuild, ct).ConfigureAwait(false);
            i++;
        }

        // If we performed a global rebuild, persist the source root into Meta for future comparisons.
        if (didGlobalRebuild)
        {
            try
            {
                await SetMetaAsync("source_root", root, ct).ConfigureAwait(false);
            }
            catch { }
        }
    }

    private async Task DiffAndApplyAsync(
        AdmxBundle bundle,
        string culture,
        bool allowGlobalRebuild,
        CancellationToken ct
    )
    {
        // Serialize writers across processes to avoid WAL write conflicts and cache deletion races.
        using var writerLock = AdmxCacheRuntime.TryAcquireWriterLock(TimeSpan.FromSeconds(30));
        if (writerLock is null)
        {
            // Give up quietly if we cannot obtain the writer lock; readers can continue using existing data.
            return;
        }

        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var tx = await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted)
            .ConfigureAwait(false);
        try
        {
            if (allowGlobalRebuild)
            {
                using var purge = conn.CreateCommand();
                purge.CommandText =
                    "DELETE FROM PolicyIndex; DELETE FROM PolicyIndexMap; DELETE FROM PolicyI18n; DELETE FROM PolicyDeps; DELETE FROM PolicyStringsDeps; DELETE FROM Policies;";
                await purge.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            else
            {
                using (var delI18n = conn.CreateCommand())
                {
                    delI18n.CommandText = "DELETE FROM PolicyI18n WHERE culture=@c";
                    delI18n.Parameters.AddWithValue("@c", culture);
                    await delI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                using (var delIdx = conn.CreateCommand())
                {
                    delIdx.CommandText =
                        "DELETE FROM PolicyIndex WHERE rowid IN (SELECT rowid FROM PolicyIndexMap WHERE culture=@c); DELETE FROM PolicyIndexMap WHERE culture=@c;";
                    delIdx.Parameters.AddWithValue("@c", culture);
                    await delIdx.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                using (var delStr = conn.CreateCommand())
                {
                    delStr.CommandText = "DELETE FROM PolicyStringsDeps WHERE culture=@c";
                    delStr.Parameters.AddWithValue("@c", culture);
                    await delStr.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }

            foreach (var kv in bundle.Policies)
            {
                await UpsertOnePolicyAsync(conn, kv.Value, culture, ct).ConfigureAwait(false);
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
            await _store.OptimizeAsync(conn, ct).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
            }
            catch { }
        }
    }

    private async Task<bool> NeedsGlobalRebuildAsync(string currentRoot, CancellationToken ct)
    {
        try
        {
            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM Meta WHERE key='source_root' LIMIT 1";
            var obj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            var stored = obj as string;
            if (string.IsNullOrWhiteSpace(stored))
                return true; // first run
            // Compare case-insensitively and with normalized full paths where possible
            try
            {
                var a = Path.GetFullPath(stored);
                var b = Path.GetFullPath(currentRoot);
                return !string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return !string.Equals(stored, currentRoot, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // On any error, fall back to global rebuild to be safe
            return true;
        }
    }

    private async Task SetMetaAsync(string key, string value, CancellationToken ct)
    {
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Meta(key, value) VALUES(@k,@v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> GetExistingCulturesAsync(CancellationToken ct)
    {
        var list = new List<string>(4);
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT culture FROM PolicyI18n ORDER BY culture";
        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!rdr.IsDBNull(0))
                list.Add(rdr.GetString(0));
        }
        return list;
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

    private async Task UpsertOnePolicyAsync(
        SqliteConnection conn,
        PolicyPlusPolicy pol,
        string culture,
        CancellationToken ct
    )
    {
        var raw = pol.RawPolicy;
        var ns = raw.DefinedIn.AdmxNamespace;
        var policyName = raw.ID;
        var catKey = pol.Category?.UniqueID ?? string.Empty;
        var hive =
            raw.Section == AdmxPolicySection.Machine ? "HKLM"
            : raw.Section == AdmxPolicySection.User ? "HKCU"
            : string.Empty;
        var regKey = raw.RegistryKey;
        var regVal = raw.RegistryValue;
        var vtype = InferValueType(raw);
        var productHint = pol.SupportedOn?.DisplayName ?? string.Empty;

        long policyId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"INSERT INTO Policies(ns, policy_name, category_key, hive, reg_key, reg_value, value_type, supported_min, supported_max, deprecated, product_hint)
VALUES(@ns,@name,@cat,@hive,@rkey,@rval,@vtype,'','',0,@ph)
ON CONFLICT(ns,policy_name) DO UPDATE SET category_key=excluded.category_key, hive=excluded.hive, reg_key=excluded.reg_key, reg_value=excluded.reg_value, value_type=excluded.value_type, product_hint=excluded.product_hint;
SELECT id FROM Policies WHERE ns=@ns AND policy_name=@name;";
            cmd.Parameters.AddWithValue("@ns", ns);
            cmd.Parameters.AddWithValue("@name", policyName);
            cmd.Parameters.AddWithValue("@cat", catKey);
            cmd.Parameters.AddWithValue("@hive", hive);
            cmd.Parameters.AddWithValue("@rkey", (object?)regKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rval", (object?)regVal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@vtype", vtype);
            cmd.Parameters.AddWithValue("@ph", productHint);
            var obj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            policyId = Convert.ToInt64(obj, CultureInfo.InvariantCulture);
        }

        var dname = pol.DisplayName ?? string.Empty;
        var desc = pol.DisplayExplanation ?? string.Empty;
        var catPath = BuildCategoryPath(pol.Category);
        var presJson = pol.Presentation is null ? null : JsonSerializer.Serialize(pol.Presentation);

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"INSERT OR REPLACE INTO PolicyI18n(policy_id, culture, display_name, explain_text, category_path, reading_kana, presentation_json)
VALUES(@pid,@culture,@dname,@desc,@cat,@kana,@pres)";
            cmd.Parameters.AddWithValue("@pid", policyId);
            cmd.Parameters.AddWithValue("@culture", culture);
            cmd.Parameters.AddWithValue("@dname", dname);
            cmd.Parameters.AddWithValue("@desc", desc);
            cmd.Parameters.AddWithValue("@cat", catPath);
            cmd.Parameters.AddWithValue("@kana", DBNull.Value);
            if (presJson is null)
                cmd.Parameters.AddWithValue("@pres", DBNull.Value);
            else
                cmd.Parameters.AddWithValue("@pres", Encoding.UTF8.GetBytes(presJson));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        var titleNorm = TextNormalization.ToNGramTokens(TextNormalization.NormalizeStrict(dname));
        var descNorm = TextNormalization.ToNGramTokens(TextNormalization.NormalizeStrict(desc));
        var titleLoose = TextNormalization.ToNGramTokens(TextNormalization.NormalizeLoose(dname));
        var descLoose = TextNormalization.ToNGramTokens(TextNormalization.NormalizeLoose(desc));
        var regPath = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeStrict(GetRegistryPath(hive, regKey, regVal))
        );
        var tags = string.Join(
            ' ',
            new[] { ns, vtype, productHint, policyName }.Where(s => !string.IsNullOrWhiteSpace(s))
        );

        long rowid;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"INSERT INTO PolicyIndex(title_norm,desc_norm,title_loose,desc_loose,registry_path,tags) VALUES(@tn,@dn,@tl,@dl,@rp,@tags); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@tn", titleNorm);
            cmd.Parameters.AddWithValue("@dn", descNorm);
            cmd.Parameters.AddWithValue("@tl", titleLoose);
            cmd.Parameters.AddWithValue("@dl", descLoose);
            cmd.Parameters.AddWithValue("@rp", regPath);
            cmd.Parameters.AddWithValue("@tags", tags);
            var obj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            rowid = Convert.ToInt64(obj, CultureInfo.InvariantCulture);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO PolicyIndexMap(rowid,policy_id,culture) VALUES(@rowid,@pid,@culture)";
            cmd.Parameters.AddWithValue("@rowid", rowid);
            cmd.Parameters.AddWithValue("@pid", policyId);
            cmd.Parameters.AddWithValue("@culture", culture);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

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
        return SearchAsync(query, culture, fields, limit, ct);
    }

    public async Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        SearchFields fields,
        int limit = 50,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(query) || fields == SearchFields.None)
            return Array.Empty<PolicyHit>();

        culture = NormalizeCultureName(culture);
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var qExact = query;
        var qPrefix = query + "%";
        var norm = TextNormalization.ToNGramTokens(TextNormalization.NormalizeStrict(query));
        var loose = TextNormalization.ToNGramTokens(TextNormalization.NormalizeLoose(query));

        bool useId = (fields & SearchFields.Id) != 0;
        // Only include registry-path FTS fields when the query looks like a registry path to reduce false positives
        bool useReg = (fields & SearchFields.Registry) != 0 && LooksLikeRegistryQuery(query);
        bool useName = (fields & SearchFields.Name) != 0;
        bool useDesc = (fields & SearchFields.Description) != 0;
        bool enableFts = useName || useDesc || useReg || useId;

        var ftsNormCols = new List<string>();
        if (useName)
            ftsNormCols.Add("title_norm");
        if (useDesc)
            ftsNormCols.Add("desc_norm");
        if (useId && LooksLikeIdQuery(query))
            ftsNormCols.Add("tags");
        if (useReg)
            ftsNormCols.Add("registry_path");

        var ftsLooseCols = new List<string>();
        if (useName)
            ftsLooseCols.Add("title_loose");
        if (useDesc)
            ftsLooseCols.Add("desc_loose");
        if (useId && LooksLikeIdQuery(query))
            ftsLooseCols.Add("tags");
        if (useReg)
            ftsLooseCols.Add("registry_path");

        // Build FTS5 MATCH expressions. Each selected column is prefixed in the MATCH query, combined via OR.
        static string EscapeSqlSingle(string s) => s.Replace("'", "''");
        static string BuildMatch(string grams, List<string> cols)
        {
            if (cols.Count == 0 || string.IsNullOrWhiteSpace(grams))
                return string.Empty;
            var tokens = grams.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            if (tokens.Length == 0)
                return string.Empty;
            // Quote each token to suppress FTS5 parser treating ':' or other punctuation specially.
            var quoted = tokens.Select(t => "\"" + t.Replace("\"", "\"\"") + "\"");
            var inside = string.Join(' ', quoted);
            return string.Join(" OR ", cols.Select(c => $"{c}:(" + inside + ")"));
        }
        var matchNorm = BuildMatch(norm, ftsNormCols);
        var matchLoose = BuildMatch(loose, ftsLooseCols);
        var matchNormEsc = EscapeSqlSingle(matchNorm);
        var matchLooseEsc = EscapeSqlSingle(matchLoose);

        var sb = new StringBuilder();
        sb.AppendLine("WITH K1 AS (");
        sb.AppendLine("  SELECT NULL AS id, @culture AS culture, '' AS display_name, -1 AS score");
        sb.AppendLine("  WHERE 1=0");
        if (useId)
        {
            sb.AppendLine("  UNION ALL");
            sb.AppendLine("  SELECT p.id, @culture AS culture, s.display_name, 1200 AS score");
            sb.AppendLine(
                "  FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture"
            );
            sb.AppendLine("  WHERE p.policy_name = @q_exact");
            sb.AppendLine("  UNION ALL");
            sb.AppendLine("  SELECT p.id, @culture, s.display_name, 300");
            sb.AppendLine(
                "  FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture"
            );
            sb.AppendLine("  WHERE p.policy_name LIKE @q_prefix");
        }
        if ((fields & SearchFields.Registry) != 0 && LooksLikeRegistryQuery(query))
        {
            sb.AppendLine("  UNION ALL");
            sb.AppendLine("  SELECT p.id, @culture, s.display_name, 1000");
            sb.AppendLine(
                "  FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture"
            );
            sb.AppendLine("  WHERE (p.hive||'\\\\'||p.reg_key||'\\\\'||p.reg_value) = @q_exact");
        }
        sb.AppendLine(") ,");
        sb.AppendLine("F1 AS (");
        sb.AppendLine(
            "  SELECT m.policy_id AS id, m.culture, s.display_name, (100 - bm25(PolicyIndex)) AS score"
        );
        sb.AppendLine("  FROM PolicyIndex");
        sb.AppendLine("  JOIN PolicyIndexMap m ON m.rowid = PolicyIndex.rowid");
        sb.AppendLine("  JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=@culture");
        sb.AppendLine("  WHERE m.culture=@culture AND @enableFts = 1");
        if (enableFts && ftsNormCols.Count > 0)
        {
            sb.Append("    AND PolicyIndex MATCH '");
            sb.Append(matchNormEsc);
            sb.AppendLine("'");
        }
        else
        {
            sb.AppendLine("    AND (0)");
        }
        sb.AppendLine(") ,");
        sb.AppendLine("F2 AS (");
        sb.AppendLine(
            "  SELECT m.policy_id AS id, m.culture, s.display_name, (60 - bm25(PolicyIndex)) AS score"
        );
        sb.AppendLine("  FROM PolicyIndex");
        sb.AppendLine("  JOIN PolicyIndexMap m ON m.rowid = PolicyIndex.rowid");
        sb.AppendLine("  JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=@culture");
        sb.AppendLine("  WHERE m.culture=@culture AND @enableFts = 1");
        if (enableFts && ftsLooseCols.Count > 0)
        {
            sb.Append("    AND PolicyIndex MATCH '");
            sb.Append(matchLooseEsc);
            sb.AppendLine("'");
        }
        else
        {
            sb.AppendLine("    AND (0)");
        }
        sb.AppendLine(")");
        sb.AppendLine("SELECT Z.id, Z.culture,");
        sb.AppendLine(
            "       (SELECT ns||':'||policy_name FROM Policies P WHERE P.id = Z.id) AS unique_id,"
        );
        sb.AppendLine("       MAX(Z.score) AS score,");
        sb.AppendLine("       Z.display_name,");
        sb.AppendLine(
            "       (SELECT hive||'\\\\'||reg_key||'\\\\'||reg_value FROM Policies P2 WHERE P2.id = Z.id) AS registry_path,"
        );
        sb.AppendLine(
            "       (SELECT product_hint FROM Policies P3 WHERE P3.id = Z.id) AS product_hint,"
        );
        sb.AppendLine(
            "       (SELECT value_type FROM Policies P4 WHERE P4.id = Z.id) AS value_type"
        );
        sb.AppendLine("FROM (");
        sb.AppendLine("    SELECT id, culture, display_name, score FROM K1");
        sb.AppendLine("    UNION ALL SELECT id, culture, display_name, score FROM F1");
        sb.AppendLine("    UNION ALL SELECT id, culture, display_name, score FROM F2");
        sb.AppendLine(") AS Z");
        sb.AppendLine("GROUP BY Z.id, Z.culture");
        sb.AppendLine("ORDER BY score DESC");
        sb.AppendLine("LIMIT @limit;");

        var list = new List<PolicyHit>(Math.Min(limit, 256));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@culture", culture);
        cmd.Parameters.AddWithValue("@q_exact", qExact);
        cmd.Parameters.AddWithValue("@q_prefix", qPrefix);

        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@enableFts", enableFts ? 1 : 0);

        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            var pid = rdr.GetInt64(0);
            var cul = rdr.GetString(1);
            var uid = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
            var score = rdr.GetDouble(3);
            var dname = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
            var reg = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5);
            var prod = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6);
            var vtype = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7);
            list.Add(new PolicyHit(pid, cul, uid, dname, reg, prod, vtype, score));
        }
        return list;
    }

    public async Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        string culture,
        CancellationToken ct = default
    )
    {
        culture = NormalizeCultureName(culture);
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

    public async Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        string culture,
        CancellationToken ct = default
    )
    {
        culture = NormalizeCultureName(culture);
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

    private static bool LooksLikeRegistryQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;
        var q = query.Trim();
        // Quick signals for registry-like input
        if (q.Contains('\\'))
            return true; // e.g., HKCU\Software\...
        var ql = q.Replace("_", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (
            ql.StartsWith("HKCU")
            || ql.StartsWith("HKLM")
            || ql.StartsWith("HKEYCURRENTUSER")
            || ql.StartsWith("HKEYLOCALMACHINE")
        )
            return true;
        // Common hive + branch keywords
        var qll = q.ToLowerInvariant();
        if (
            qll.Contains("policies\\")
            || qll.Contains("software\\")
            || qll.Contains("system\\")
            || qll.Contains("microsoft\\")
        )
            return true;
        return false;
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

    private static string NormalizeCultureName(string culture)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(culture))
                return CultureInfo.CurrentUICulture.Name;
            // CultureInfo will canonicalize casing (e.g., ja-jp -> ja-JP)
            return CultureInfo.GetCultureInfo(culture).Name;
        }
        catch
        {
            // Fallback: preserve as-is but trim
            return culture.Trim();
        }
    }

    private static bool LooksLikeIdQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;
        // Unique IDs are typically in the form namespace:PolicyName
        // Keep heuristic lightweight to avoid slowing hot path.
        return query.Contains(':');
    }

    public async Task<IReadOnlyCollection<string>> GetPolicyUniqueIdsByCategoriesAsync(
        IEnumerable<string> categoryKeys,
        CancellationToken ct = default
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
            sb.Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(")");
        }
        sb.Append(";");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        for (int i = 0; i < list.Count; i++)
        {
            cmd.Parameters.AddWithValue(
                "@k" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                list[i]
            );
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
}
