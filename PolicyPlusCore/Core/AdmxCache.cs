using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    private readonly string _localDefs;

    public AdmxCache()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        var dir = Path.Combine(localAppData, "PolicyPlusPlus", "Cache");
        _store = new AdmxCacheStore(Path.Combine(dir, "admx_cache.db"));
        _localDefs = Environment.ExpandEnvironmentVariables(@"%WINDIR%\PolicyDefinitions");
        // Central store path can be added later if needed.
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _store.InitializeAsync(ct).ConfigureAwait(false);
    }

    public async Task ScanAndUpdateAsync(CancellationToken ct = default)
    {
        // Rescan folders and apply incremental updates based on Sources table.
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var bundle = new AdmxBundle();
            var lang = CultureInfo.CurrentUICulture.Name;
            bundle.EnableLanguageFallback = true;
            // Load local store
            if (Directory.Exists(_localDefs))
            {
                foreach (var file in Directory.EnumerateFiles(_localDefs, "*.admx"))
                    bundle.LoadFile(file, lang);
            }
            // TODO: central store if configured

            await DiffAndApplyAsync(conn, bundle, lang, ct).ConfigureAwait(false);
            await IncrementCacheVersionAsync(conn, ct).ConfigureAwait(false);
            // Run light optimization outside the transaction because some pragmas are less effective inside.
            await tx.CommitAsync(ct).ConfigureAwait(false);
            // Run optimize+VACUUM after commit for best effect
            await _store.OptimizeAsync(null, ct).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
            }
            catch { }
            throw;
        }
    }

    private static string GetRegistryPath(string hive, string key, string value) =>
        string.Join('\\', new[] { hive, key, value }.Where(x => !string.IsNullOrEmpty(x)));

    private async Task DiffAndApplyAsync(
        SqliteConnection conn,
        AdmxBundle bundle,
        string culture,
        CancellationToken ct
    )
    {
        // 1) Upsert Sources for admx/adml and detect changed files
        var sourceChanged = 0;
        var sourceTotal = 0;
        var sourceMap =
            new Dictionary<
                AdmxFile,
                (long AdmxId, long AdmlId, bool AdmxChanged, bool AdmlChanged)
            >();
        foreach (var pair in bundle.Sources)
        {
            var admx = pair.Key;
            var adml = pair.Value;
            var (admxId, admxChanged) = await UpsertSourceAsync(
                    conn,
                    admx.SourceFile,
                    "neutral",
                    ct
                )
                .ConfigureAwait(false);
            var admlCulture = GetCultureFromAdmlPath(adml.SourceFile) ?? culture;
            var (admlId, admlChanged) = await UpsertSourceAsync(
                    conn,
                    adml.SourceFile,
                    admlCulture,
                    ct
                )
                .ConfigureAwait(false);
            sourceMap[admx] = (admxId, admlId, admxChanged, admlChanged);
            sourceTotal += 2;
            if (admxChanged)
                sourceChanged++;
            if (admlChanged)
                sourceChanged++;
        }

        // 1.5) Handle deletions: find Sources rows under local path that no longer exist on disk
        var presentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in bundle.Sources)
        {
            presentPaths.Add(kv.Key.SourceFile);
            presentPaths.Add(kv.Value.SourceFile);
        }
        var missingAdmxIds = new List<long>();
        var missingAdmlIdsForCulture = new List<long>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, path, culture FROM Sources WHERE path LIKE @p";
            cmd.Parameters.AddWithValue("@p", _localDefs.Replace("\\", "\\\\") + "%");
            using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rdr.ReadAsync(ct).ConfigureAwait(false))
            {
                var id = rdr.GetInt64(0);
                var path = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                var cul = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                if (!presentPaths.Contains(path))
                {
                    if (string.Equals(cul, "neutral", StringComparison.OrdinalIgnoreCase))
                        missingAdmxIds.Add(id);
                    else if (string.Equals(cul, culture, StringComparison.OrdinalIgnoreCase))
                        missingAdmlIdsForCulture.Add(id);
                }
            }
        }
        if (missingAdmxIds.Count > 0)
        {
            // Delete policies tied to missing ADMX sources (all cultures)
            var idParams = string.Join(",", missingAdmxIds.Select((_, i) => "@x" + i));
            long impacted = 0;
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText =
                    $"SELECT COUNT(DISTINCT policy_id) FROM PolicyDeps WHERE requires_admx_source_id IN ({idParams})";
                for (int i = 0; i < missingAdmxIds.Count; i++)
                    countCmd.Parameters.AddWithValue("@x" + i, missingAdmxIds[i]);
                try
                {
                    var o = await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    impacted =
                        o is null || o is DBNull
                            ? 0
                            : Convert.ToInt64(o, CultureInfo.InvariantCulture);
                }
                catch
                {
                    impacted = 0;
                }
            }
            sourceChanged += (int)Math.Min(int.MaxValue, impacted * 2); // rough weight

            using (var delIdx = conn.CreateCommand())
            {
                delIdx.CommandText =
                    $"DELETE FROM PolicyIndex WHERE rowid IN (SELECT rowid FROM PolicyIndexMap WHERE policy_id IN (SELECT policy_id FROM PolicyDeps WHERE requires_admx_source_id IN ({idParams})));";
                for (int i = 0; i < missingAdmxIds.Count; i++)
                    delIdx.Parameters.AddWithValue("@x" + i, missingAdmxIds[i]);
                await delIdx.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delMap = conn.CreateCommand())
            {
                delMap.CommandText =
                    $"DELETE FROM PolicyIndexMap WHERE policy_id IN (SELECT policy_id FROM PolicyDeps WHERE requires_admx_source_id IN ({idParams}))";
                for (int i = 0; i < missingAdmxIds.Count; i++)
                    delMap.Parameters.AddWithValue("@x" + i, missingAdmxIds[i]);
                await delMap.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delI18n = conn.CreateCommand())
            {
                delI18n.CommandText =
                    $"DELETE FROM PolicyI18n WHERE policy_id IN (SELECT policy_id FROM PolicyDeps WHERE requires_admx_source_id IN ({idParams}))";
                for (int i = 0; i < missingAdmxIds.Count; i++)
                    delI18n.Parameters.AddWithValue("@x" + i, missingAdmxIds[i]);
                await delI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delStr = conn.CreateCommand())
            {
                delStr.CommandText =
                    $"DELETE FROM PolicyStringsDeps WHERE policy_id IN (SELECT policy_id FROM PolicyDeps WHERE requires_admx_source_id IN ({idParams}))";
                for (int i = 0; i < missingAdmxIds.Count; i++)
                    delStr.Parameters.AddWithValue("@x" + i, missingAdmxIds[i]);
                await delStr.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delDeps = conn.CreateCommand())
            {
                delDeps.CommandText =
                    $"DELETE FROM PolicyDeps WHERE requires_admx_source_id IN ({idParams})";
                for (int i = 0; i < missingAdmxIds.Count; i++)
                    delDeps.Parameters.AddWithValue("@x" + i, missingAdmxIds[i]);
                await delDeps.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delPolicies = conn.CreateCommand())
            {
                // Remove orphaned policy rows
                delPolicies.CommandText =
                    "DELETE FROM Policies WHERE id NOT IN (SELECT DISTINCT policy_id FROM PolicyDeps)";
                await delPolicies.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
        if (missingAdmlIdsForCulture.Count > 0)
        {
            var idParams = string.Join(",", missingAdmlIdsForCulture.Select((_, i) => "@y" + i));
            using (var delI18n = conn.CreateCommand())
            {
                delI18n.CommandText =
                    $"DELETE FROM PolicyI18n WHERE culture=@c AND policy_id IN (SELECT policy_id FROM PolicyStringsDeps WHERE culture=@c AND adml_source_id IN ({idParams}))";
                delI18n.Parameters.AddWithValue("@c", culture);
                for (int i = 0; i < missingAdmlIdsForCulture.Count; i++)
                    delI18n.Parameters.AddWithValue("@y" + i, missingAdmlIdsForCulture[i]);
                await delI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delIdx = conn.CreateCommand())
            {
                delIdx.CommandText =
                    $"DELETE FROM PolicyIndex WHERE rowid IN (SELECT rowid FROM PolicyIndexMap WHERE culture=@c AND policy_id IN (SELECT policy_id FROM PolicyStringsDeps WHERE culture=@c AND adml_source_id IN ({idParams})));";
                delIdx.Parameters.AddWithValue("@c", culture);
                for (int i = 0; i < missingAdmlIdsForCulture.Count; i++)
                    delIdx.Parameters.AddWithValue("@y" + i, missingAdmlIdsForCulture[i]);
                await delIdx.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delMap = conn.CreateCommand())
            {
                delMap.CommandText =
                    $"DELETE FROM PolicyIndexMap WHERE culture=@c AND policy_id IN (SELECT policy_id FROM PolicyStringsDeps WHERE culture=@c AND adml_source_id IN ({idParams}))";
                delMap.Parameters.AddWithValue("@c", culture);
                for (int i = 0; i < missingAdmlIdsForCulture.Count; i++)
                    delMap.Parameters.AddWithValue("@y" + i, missingAdmlIdsForCulture[i]);
                await delMap.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var delStr = conn.CreateCommand())
            {
                delStr.CommandText =
                    $"DELETE FROM PolicyStringsDeps WHERE culture=@c AND adml_source_id IN ({idParams})";
                delStr.Parameters.AddWithValue("@c", culture);
                for (int i = 0; i < missingAdmlIdsForCulture.Count; i++)
                    delStr.Parameters.AddWithValue("@y" + i, missingAdmlIdsForCulture[i]);
                await delStr.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            sourceChanged += missingAdmlIdsForCulture.Count;
        }

        bool fullRebuild = false;
        if (sourceTotal == 0)
            fullRebuild = true;
        else if ((double)sourceChanged / Math.Max(1, sourceTotal) > 0.25)
            fullRebuild = true;

        if (fullRebuild)
        {
            using var purge = conn.CreateCommand();
            purge.CommandText =
                "DELETE FROM PolicyIndex; DELETE FROM PolicyIndexMap; DELETE FROM PolicyI18n; DELETE FROM PolicyDeps; DELETE FROM PolicyStringsDeps; DELETE FROM Policies;";
            await purge.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            foreach (var kv in bundle.Policies)
                await UpsertOnePolicyAsync(
                        conn,
                        kv.Value,
                        culture,
                        sourceMap[kv.Value.RawPolicy.DefinedIn],
                        ct
                    )
                    .ConfigureAwait(false);
            return;
        }

        // Partial: determine impacted policies by changed sources
        var changedAdmx = sourceMap
            .Where(kv => kv.Value.AdmxChanged)
            .Select(kv => kv.Key)
            .ToHashSet();
        var changedAdmlIds = sourceMap
            .Where(kv => kv.Value.AdmlChanged)
            .Select(kv => kv.Value.AdmlId)
            .ToHashSet();

        foreach (var kv in bundle.Policies)
        {
            var pol = kv.Value;
            var src = sourceMap[pol.RawPolicy.DefinedIn];
            if (changedAdmx.Contains(pol.RawPolicy.DefinedIn))
            {
                // Core + i18n + fts
                await UpsertOnePolicyAsync(conn, pol, culture, src, ct).ConfigureAwait(false);
            }
            else if (changedAdmlIds.Contains(src.AdmlId))
            {
                // Only i18n + fts for this culture
                var pid = await GetPolicyIdAsync(
                        conn,
                        pol.RawPolicy.DefinedIn.AdmxNamespace,
                        pol.RawPolicy.ID,
                        ct
                    )
                    .ConfigureAwait(false);
                if (pid == 0)
                {
                    await UpsertOnePolicyAsync(conn, pol, culture, src, ct).ConfigureAwait(false);
                }
                else
                {
                    await RebuildI18nAndIndexAsync(conn, pid, pol, culture, src, ct)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static string? GetCultureFromAdmlPath(string admlPath)
    {
        try
        {
            return Path.GetFileName(Path.GetDirectoryName(admlPath));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<long> GetPolicyIdAsync(
        SqliteConnection conn,
        string ns,
        string name,
        CancellationToken ct
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM Policies WHERE ns=@ns AND policy_name=@name";
        cmd.Parameters.AddWithValue("@ns", ns);
        cmd.Parameters.AddWithValue("@name", name);
        var obj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return obj is null || obj is DBNull
            ? 0
            : Convert.ToInt64(obj, CultureInfo.InvariantCulture);
    }

    private async Task UpsertOnePolicyAsync(
        SqliteConnection conn,
        PolicyPlusPolicy pol,
        string culture,
        (long AdmxId, long AdmlId, bool AdmxChanged, bool AdmlChanged) src,
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
        var supportedMin = pol.SupportedOn?.DisplayName ?? string.Empty;
        var supportedMax = string.Empty;
        var deprecated = 0;
        var productHint = pol.SupportedOn?.DisplayName ?? string.Empty;

        long policyId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                @"INSERT INTO Policies(ns, policy_name, category_key, hive, reg_key, reg_value, value_type, supported_min, supported_max, deprecated, product_hint)
VALUES(@ns,@name,@cat,@hive,@rkey,@rval,@vtype,@smin,@smax,@dep,@ph)
ON CONFLICT(ns,policy_name) DO UPDATE SET category_key=excluded.category_key, hive=excluded.hive, reg_key=excluded.reg_key, reg_value=excluded.reg_value, value_type=excluded.value_type, supported_min=excluded.supported_min, supported_max=excluded.supported_max, deprecated=excluded.deprecated, product_hint=excluded.product_hint;
SELECT id FROM Policies WHERE ns=@ns AND policy_name=@name;";
            cmd.Parameters.AddWithValue("@ns", ns);
            cmd.Parameters.AddWithValue("@name", policyName);
            cmd.Parameters.AddWithValue("@cat", catKey);
            cmd.Parameters.AddWithValue("@hive", hive);
            cmd.Parameters.AddWithValue("@rkey", regKey);
            cmd.Parameters.AddWithValue("@rval", regVal);
            cmd.Parameters.AddWithValue("@vtype", vtype);
            cmd.Parameters.AddWithValue("@smin", supportedMin);
            cmd.Parameters.AddWithValue("@smax", supportedMax);
            cmd.Parameters.AddWithValue("@dep", deprecated);
            cmd.Parameters.AddWithValue("@ph", productHint);
            var obj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            policyId = Convert.ToInt64(obj, CultureInfo.InvariantCulture);
        }

        // Track dependencies
        using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM PolicyDeps WHERE policy_id=@pid";
            del.Parameters.AddWithValue("@pid", policyId);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        using (var ins = conn.CreateCommand())
        {
            ins.CommandText =
                "INSERT INTO PolicyDeps(policy_id, requires_admx_source_id) VALUES(@pid,@sid)";
            ins.Parameters.AddWithValue("@pid", policyId);
            ins.Parameters.AddWithValue("@sid", src.AdmxId);
            await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await RebuildI18nAndIndexAsync(conn, policyId, pol, culture, src, ct).ConfigureAwait(false);
    }

    private async Task RebuildI18nAndIndexAsync(
        SqliteConnection conn,
        long policyId,
        PolicyPlusPolicy pol,
        string culture,
        (long AdmxId, long AdmlId, bool AdmxChanged, bool AdmlChanged) src,
        CancellationToken ct
    )
    {
        using (var del1 = conn.CreateCommand())
        {
            del1.CommandText = "DELETE FROM PolicyI18n WHERE policy_id=@pid AND culture=@c";
            del1.Parameters.AddWithValue("@pid", policyId);
            del1.Parameters.AddWithValue("@c", culture);
            await del1.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        using (var del2 = conn.CreateCommand())
        {
            del2.CommandText =
                "DELETE FROM PolicyIndex WHERE rowid IN (SELECT rowid FROM PolicyIndexMap WHERE policy_id=@pid AND culture=@c); DELETE FROM PolicyIndexMap WHERE policy_id=@pid AND culture=@c";
            del2.Parameters.AddWithValue("@pid", policyId);
            del2.Parameters.AddWithValue("@c", culture);
            await del2.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // i18n insert
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

        // Strings deps
        using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM PolicyStringsDeps WHERE policy_id=@pid AND culture=@c";
            del.Parameters.AddWithValue("@pid", policyId);
            del.Parameters.AddWithValue("@c", culture);
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        using (var ins = conn.CreateCommand())
        {
            ins.CommandText =
                "INSERT INTO PolicyStringsDeps(policy_id,culture,adml_source_id) VALUES(@pid,@c,@sid)";
            ins.Parameters.AddWithValue("@pid", policyId);
            ins.Parameters.AddWithValue("@c", culture);
            ins.Parameters.AddWithValue("@sid", src.AdmlId);
            await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // FTS insert using last_insert_rowid
        var hive =
            pol.RawPolicy.Section == AdmxPolicySection.Machine ? "HKLM"
            : pol.RawPolicy.Section == AdmxPolicySection.User ? "HKCU"
            : string.Empty;
        var regPathRaw = GetRegistryPath(
            hive,
            pol.RawPolicy.RegistryKey,
            pol.RawPolicy.RegistryValue
        );
        var regPath = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeStrict(regPathRaw)
        );
        var vtype = InferValueType(pol.RawPolicy);
        var productHint = pol.SupportedOn?.DisplayName ?? string.Empty;
        var tags = string.Join(
            ' ',
            new[]
            {
                pol.RawPolicy.DefinedIn.AdmxNamespace,
                vtype,
                productHint,
                pol.RawPolicy.ID,
            }.Where(s => !string.IsNullOrWhiteSpace(s))
        );

        var titleNorm = TextNormalization.ToNGramTokens(TextNormalization.NormalizeStrict(dname));
        var descNorm = TextNormalization.ToNGramTokens(TextNormalization.NormalizeStrict(desc));
        var titleLoose = TextNormalization.ToNGramTokens(TextNormalization.NormalizeLoose(dname));
        var descLoose = TextNormalization.ToNGramTokens(TextNormalization.NormalizeLoose(desc));

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

    private static async Task<(long Id, bool Changed)> UpsertSourceAsync(
        SqliteConnection conn,
        string path,
        string culture,
        CancellationToken ct
    )
    {
        var fi = new FileInfo(path);
        long mtime = fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0L;
        string sha = fi.Exists
            ? await ComputeSha256HexAsync(path, ct).ConfigureAwait(false)
            : string.Empty;

        string select = "SELECT id, sha256, mtime_utc FROM Sources WHERE path=@p";
        long id = 0;
        string? prevSha = null;
        long prevMtime = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = select;
            cmd.Parameters.AddWithValue("@p", path);
            using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await rdr.ReadAsync(ct).ConfigureAwait(false))
            {
                id = rdr.GetInt64(0);
                prevSha = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                prevMtime = rdr.IsDBNull(2) ? 0 : rdr.GetInt64(2);
            }
        }

        bool changed = prevSha is null || !string.Equals(prevSha, sha, StringComparison.Ordinal);
        if (id == 0)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText =
                "INSERT INTO Sources(path,sha256,mtime_utc,culture) VALUES(@p,@s,@m,@c); SELECT last_insert_rowid();";
            ins.Parameters.AddWithValue("@p", path);
            ins.Parameters.AddWithValue("@s", sha);
            ins.Parameters.AddWithValue("@m", mtime);
            ins.Parameters.AddWithValue("@c", culture);
            var obj = await ins.ExecuteScalarAsync(ct).ConfigureAwait(false);
            id = Convert.ToInt64(obj, CultureInfo.InvariantCulture);
        }
        else if (changed)
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE Sources SET sha256=@s, mtime_utc=@m, culture=@c WHERE id=@id";
            upd.Parameters.AddWithValue("@id", id);
            upd.Parameters.AddWithValue("@s", sha);
            upd.Parameters.AddWithValue("@m", mtime);
            upd.Parameters.AddWithValue("@c", culture);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        return (id, changed);
    }

    private static async Task<string> ComputeSha256HexAsync(string path, CancellationToken ct)
    {
        using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            81920,
            useAsync: true
        );
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct).ConfigureAwait(false);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static async Task IncrementCacheVersionAsync(
        SqliteConnection conn,
        CancellationToken ct
    )
    {
        int cur = 0;
        using (var sel = conn.CreateCommand())
        {
            sel.CommandText = "SELECT value FROM Meta WHERE key='cache_version'";
            var obj = await sel.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (
                obj is string s
                && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            )
                cur = v;
        }
        using (var up = conn.CreateCommand())
        {
            up.CommandText =
                "INSERT INTO Meta(key,value) VALUES('cache_version', @v) ON CONFLICT(key) DO UPDATE SET value=@v";
            up.Parameters.AddWithValue("@v", (cur + 1).ToString(CultureInfo.InvariantCulture));
            await up.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
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

    private static string InferValueType(AdmxPolicy raw)
    {
        // Heuristic based on elements/affected values
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
        if (parts.Count > 0)
            return string.Join('+', parts);
        return "Flag"; // default on/off
    }

    public async Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        string culture,
        int limit = 50,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<PolicyHit>();
        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        var qExact = query;
        var qPrefix = query + "%";
        var norm = TextNormalization.ToNGramTokens(TextNormalization.NormalizeStrict(query));
        var loose = TextNormalization.ToNGramTokens(TextNormalization.NormalizeLoose(query));

        const string sqlNorm =
            @"WITH K1 AS (
  SELECT p.id, @culture AS culture, s.display_name, s.explain_text, 1200 AS score
  FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
  WHERE p.policy_name = @q_exact
  UNION ALL
  SELECT p.id, @culture, s.display_name, s.explain_text, 1000
  FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
  WHERE (p.hive||'\\'||p.reg_key||'\\'||p.reg_value) = @q_exact
  UNION ALL
  SELECT p.id, @culture, s.display_name, s.explain_text, 300
  FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
  WHERE p.policy_name LIKE @q_prefix
),
F1 AS (
  SELECT m.policy_id AS id, m.culture, s.display_name, s.explain_text,
                 (100 - bm25(PolicyIndex)) AS score
  FROM PolicyIndex pi
  JOIN PolicyIndexMap m ON m.rowid = pi.rowid
  JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=@culture
  WHERE m.culture=@culture
        AND (PolicyIndex MATCH @mNorm)
),
F2 AS (
  SELECT m.policy_id AS id, m.culture, s.display_name, s.explain_text,
                 (60 - bm25(PolicyIndex)) AS score
  FROM PolicyIndex pi
  JOIN PolicyIndexMap m ON m.rowid = pi.rowid
  JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=@culture
  WHERE m.culture=@culture
        AND (PolicyIndex MATCH @mLoose)
)
SELECT id, culture,
             (SELECT ns||':'||policy_name FROM Policies WHERE id = id) AS unique_id,
             MAX(score) AS score,
       display_name,
       (SELECT hive||'\\'||reg_key||'\\'||reg_value FROM Policies WHERE id = id) AS registry_path,
       (SELECT product_hint FROM Policies WHERE id = id) AS product_hint,
       (SELECT value_type FROM Policies WHERE id = id) AS value_type
FROM (
  SELECT id, culture, display_name, score FROM K1
  UNION ALL SELECT id, culture, display_name, score FROM F1
  UNION ALL SELECT id, culture, display_name, score FROM F2
)
GROUP BY id, culture
ORDER BY score DESC
LIMIT @limit;";

        const string sqlLoose =
            @"WITH K1 AS (
    SELECT p.id, @culture AS culture, s.display_name, s.explain_text, 1200 AS score
    FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
    WHERE p.policy_name = @q_exact
    UNION ALL
    SELECT p.id, @culture, s.display_name, s.explain_text, 1000
    FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
    WHERE (p.hive||'\\'||p.reg_key||'\\'||p.reg_value) = @q_exact
    UNION ALL
    SELECT p.id, @culture, s.display_name, s.explain_text, 300
    FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id AND s.culture=@culture
    WHERE p.policy_name LIKE @q_prefix
),
F2 AS (
    SELECT m.policy_id AS id, m.culture, s.display_name, s.explain_text,
                                 (60 - bm25(PolicyIndex)) AS score
    FROM PolicyIndex pi
    JOIN PolicyIndexMap m ON m.rowid = pi.rowid
    JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=@culture
    WHERE m.culture=@culture
    AND (PolicyIndex MATCH @mLoose)
)
SELECT id, culture,
             (SELECT ns||':'||policy_name FROM Policies WHERE id = id) AS unique_id,
             MAX(score) AS score,
             display_name,
             (SELECT hive||'\\'||reg_key||'\\'||reg_value FROM Policies WHERE id = id) AS registry_path,
             (SELECT product_hint FROM Policies WHERE id = id) AS product_hint,
             (SELECT value_type FROM Policies WHERE id = id) AS value_type
FROM (
    SELECT id, culture, display_name, score FROM K1
    UNION ALL SELECT id, culture, display_name, score FROM F2
)
GROUP BY id, culture
ORDER BY score DESC
LIMIT @limit;";

        var list = new List<PolicyHit>(Math.Min(limit, 256));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sqlNorm;
        cmd.Parameters.AddWithValue("@culture", culture);
        cmd.Parameters.AddWithValue("@q_exact", qExact);
        cmd.Parameters.AddWithValue("@q_prefix", qPrefix);
        cmd.Parameters.AddWithValue("@mNorm", norm);
        cmd.Parameters.AddWithValue("@mLoose", loose);
        cmd.Parameters.AddWithValue("@limit", limit);

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
        // Phase 2: Loose fallback
        if (list.Count == 0)
        {
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = sqlLoose;
            cmd2.Parameters.AddWithValue("@culture", culture);
            cmd2.Parameters.AddWithValue("@q_exact", qExact);
            cmd2.Parameters.AddWithValue("@q_prefix", qPrefix);
            cmd2.Parameters.AddWithValue("@mLoose", loose);
            cmd2.Parameters.AddWithValue("@limit", limit);

            using var rdr2 = await cmd2.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rdr2.ReadAsync(ct).ConfigureAwait(false))
            {
                var pid = rdr2.GetInt64(0);
                var cul = rdr2.GetString(1);
                var uid = rdr2.IsDBNull(2) ? string.Empty : rdr2.GetString(2);
                var score = rdr2.GetDouble(3);
                var dname = rdr2.IsDBNull(4) ? string.Empty : rdr2.GetString(4);
                var reg = rdr2.IsDBNull(5) ? string.Empty : rdr2.GetString(5);
                var prod = rdr2.IsDBNull(6) ? string.Empty : rdr2.GetString(6);
                var vtype = rdr2.IsDBNull(7) ? string.Empty : rdr2.GetString(7);
                list.Add(new PolicyHit(pid, cul, uid, dname, reg, prod, vtype, score));
            }
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
