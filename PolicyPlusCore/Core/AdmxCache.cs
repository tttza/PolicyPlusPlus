using System;
using System.Collections.Generic;
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
        // Acquire writer lock during initialization to avoid races with cache deletion.
        IDisposable? writerLock = null;
        try
        {
            for (int attempt = 0; attempt < 3 && writerLock is null; attempt++)
            {
                writerLock = AdmxCacheRuntime.TryAcquireWriterLock(TimeSpan.FromSeconds(10));
                if (writerLock is null)
                {
                    // Best-effort short backoff before retrying
                    try
                    {
                        await Task.Delay(250, ct).ConfigureAwait(false);
                    }
                    catch { }
                }
            }

            await _store.InitializeAsync(ct).ConfigureAwait(false);
            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await _store.OptimizeAsync(conn, ct).ConfigureAwait(false);
            // Ensure schema writes are checkpointed so the DB file is not left at 0 bytes.
            try
            {
                using var ck = conn.CreateCommand();
                ck.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await ck.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch { }
        }
        finally
        {
            try
            {
                writerLock?.Dispose();
            }
            catch { }
        }
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
            bool allowGlobalRebuild = needGlobalRebuild && (i == 0);

            // Fast path: if not global rebuild and source signature unchanged, skip this culture.
            bool skipCulture = false;
            if (!allowGlobalRebuild)
            {
                try
                {
                    string sigKey = "sig_" + cul;
                    var prior = await GetMetaAsync(sigKey, ct).ConfigureAwait(false);
                    var currentSig = await ComputeSourceSignatureAsync(root, cul, ct)
                        .ConfigureAwait(false);
                    if (
                        !string.IsNullOrEmpty(prior)
                        && string.Equals(prior, currentSig, StringComparison.Ordinal)
                    )
                    {
                        skipCulture = true;
                    }
                }
                catch { }
            }

            AdmxBundle? bundle = null;
            if (!skipCulture)
            {
                bundle = new AdmxBundle();
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
                catch { }
            }

            if (!skipCulture)
            {
                if (allowGlobalRebuild)
                    didGlobalRebuild = true;
                await DiffAndApplyAsync(bundle!, cul, allowGlobalRebuild, ct).ConfigureAwait(false);
                // Persist signature after successful apply.
                try
                {
                    var sig = await ComputeSourceSignatureAsync(root, cul, ct)
                        .ConfigureAwait(false);
                    await SetMetaAsync("sig_" + cul, sig, ct).ConfigureAwait(false);
                }
                catch { }
            }
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

        // Run a single optimize after processing all cultures to reduce contention and I/O.
        try
        {
            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await _store.OptimizeAsync(conn, ct).ConfigureAwait(false);
            // Ensure WAL contents are checkpointed so the base DB reflects writes promptly.
            try
            {
                using var ck = conn.CreateCommand();
                ck.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await ck.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch { }
            // Lightweight FTS optimize; ignore errors if table absent or busy.
            try
            {
                await _store.FtsOptimizeAsync(ct).ConfigureAwait(false);
            }
            catch { }
        }
        catch { }

        // Conditional compaction: run occasionally to reclaim space if fragmentation high.
        try
        {
            try
            {
                var dbPathField = typeof(AdmxCache)
                    .Assembly.GetType("PolicyPlusCore.IO.AdmxCacheStore")
                    ?.GetField(
                        "_dbPath",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                // Reflection fallback avoided if not found; compaction still attempts heuristic based only on freelist ratio inside CompactAsync.
            }
            catch { }
            await _store
                .CompactAsync(forceFullVacuum: false, freelistThresholdRatio: 0.30, ct)
                .ConfigureAwait(false);
        }
        catch { }
    }

    private async Task DiffAndApplyAsync(
        AdmxBundle bundle,
        string culture,
        bool allowGlobalRebuild,
        CancellationToken ct
    )
    {
        // Serialize writers across processes to avoid WAL write conflicts and cache deletion races.
        // Try a few short retries to reduce chances of missing a rebuild entirely during transient contention.
        IDisposable? writerLock = null;
        for (int attempt = 0; attempt < 3 && writerLock is null; attempt++)
        {
            writerLock = AdmxCacheRuntime.TryAcquireWriterLock(TimeSpan.FromSeconds(5));
            if (writerLock is null)
            {
                try
                {
                    await Task.Delay(250, ct).ConfigureAwait(false);
                }
                catch { }
            }
        }
        if (writerLock is null)
        {
            // Could not acquire within budget; skip this pass to avoid blocking UI. A coalesced rerun should follow.
            return;
        }
        using var _writerLockScope = writerLock;

        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // Phase 1: perform required deletes in a single transaction.
        using (
            var txDel = (Microsoft.Data.Sqlite.SqliteTransaction)
                await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted)
                    .ConfigureAwait(false)
        )
        {
            try
            {
                if (allowGlobalRebuild)
                {
                    // For contentless FTS5, regular DELETE is not allowed; use special commands.
                    using (var purgeFts = conn.CreateCommand())
                    {
                        purgeFts.Transaction = txDel;
                        purgeFts.CommandText =
                            "INSERT INTO PolicyIndex(PolicyIndex) VALUES('delete-all');";
                        await purgeFts.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    using (var purgeRest = conn.CreateCommand())
                    {
                        purgeRest.Transaction = txDel;
                        purgeRest.CommandText =
                            "DELETE FROM PolicyIndexMap; DELETE FROM PolicyI18n; DELETE FROM PolicyDeps; DELETE FROM PolicyStringsDeps; DELETE FROM Policies;";
                        await purgeRest.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    using (var delI18n = conn.CreateCommand())
                    {
                        delI18n.Transaction = txDel;
                        delI18n.CommandText = "DELETE FROM PolicyI18n WHERE culture=@c";
                        delI18n.Parameters.AddWithValue("@c", culture);
                        await delI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    // Delete FTS5 rows for the culture using special 'delete' per rowid, then clean up the map.
                    var rowIds = new List<long>(256);
                    using (var getRows = conn.CreateCommand())
                    {
                        getRows.Transaction = txDel;
                        getRows.CommandText = "SELECT rowid FROM PolicyIndexMap WHERE culture=@c;";
                        getRows.Parameters.AddWithValue("@c", culture);
                        using var r = await getRows.ExecuteReaderAsync(ct).ConfigureAwait(false);
                        while (await r.ReadAsync(ct).ConfigureAwait(false))
                        {
                            if (!r.IsDBNull(0))
                                rowIds.Add(r.GetInt64(0));
                        }
                    }
                    if (rowIds.Count > 0)
                    {
                        using var delFts = conn.CreateCommand();
                        delFts.Transaction = txDel;
                        delFts.CommandText =
                            "INSERT INTO PolicyIndex(PolicyIndex, rowid) VALUES('delete', @rid);";
                        var pRid = delFts.Parameters.Add(
                            "@rid",
                            Microsoft.Data.Sqlite.SqliteType.Integer
                        );
                        foreach (var rid in rowIds)
                        {
                            pRid.Value = rid;
                            await delFts.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                        }
                    }
                    using (var delMap = conn.CreateCommand())
                    {
                        delMap.Transaction = txDel;
                        delMap.CommandText = "DELETE FROM PolicyIndexMap WHERE culture=@c;";
                        delMap.Parameters.AddWithValue("@c", culture);
                        await delMap.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    using (var delStr = conn.CreateCommand())
                    {
                        delStr.Transaction = txDel;
                        delStr.CommandText = "DELETE FROM PolicyStringsDeps WHERE culture=@c";
                        delStr.Parameters.AddWithValue("@c", culture);
                        await delStr.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                }
                await txDel.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await txDel.RollbackAsync(ct).ConfigureAwait(false);
                }
                catch { }
                // After rollback, continue without inserts to avoid partial state.
                return;
            }
        }

        // Prepare reusable commands to reduce per-policy command construction overhead.
        using var cmdPolicy = conn.CreateCommand();
        cmdPolicy.CommandText =
            @"INSERT INTO Policies(ns, policy_name, category_key, hive, reg_key, reg_value, value_type, supported_min, supported_max, deprecated, product_hint)
VALUES(@ns,@name,@cat,@hive,@rkey,@rval,@vtype,'','',0,@ph)
ON CONFLICT(ns,policy_name) DO UPDATE SET category_key=excluded.category_key, hive=excluded.hive, reg_key=excluded.reg_key, reg_value=excluded.reg_value, value_type=excluded.value_type, product_hint=excluded.product_hint;
SELECT id FROM Policies WHERE ns=@ns AND policy_name=@name;";
        var p_ns = cmdPolicy.Parameters.Add("@ns", Microsoft.Data.Sqlite.SqliteType.Text);
        var p_name = cmdPolicy.Parameters.Add("@name", Microsoft.Data.Sqlite.SqliteType.Text);
        var p_cat = cmdPolicy.Parameters.Add("@cat", Microsoft.Data.Sqlite.SqliteType.Text);
        var p_hive = cmdPolicy.Parameters.Add("@hive", Microsoft.Data.Sqlite.SqliteType.Text);
        var p_rkey = cmdPolicy.Parameters.Add("@rkey", Microsoft.Data.Sqlite.SqliteType.Text);
        var p_rval = cmdPolicy.Parameters.Add("@rval", Microsoft.Data.Sqlite.SqliteType.Text);
        var p_vtype = cmdPolicy.Parameters.Add("@vtype", Microsoft.Data.Sqlite.SqliteType.Text);
        var p_ph = cmdPolicy.Parameters.Add("@ph", Microsoft.Data.Sqlite.SqliteType.Text);
        try
        {
            cmdPolicy.Prepare();
        }
        catch { }

        using var cmdI18n = conn.CreateCommand();
        cmdI18n.CommandText =
            @"INSERT OR REPLACE INTO PolicyI18n(policy_id, culture, display_name, explain_text, category_path, reading_kana, presentation_json)
VALUES(@pid,@culture,@dname,@desc,@cat,@kana,@pres)";
        var s_pid = cmdI18n.Parameters.Add("@pid", Microsoft.Data.Sqlite.SqliteType.Integer);
        var s_culture = cmdI18n.Parameters.Add("@culture", Microsoft.Data.Sqlite.SqliteType.Text);
        var s_dname = cmdI18n.Parameters.Add("@dname", Microsoft.Data.Sqlite.SqliteType.Text);
        var s_desc = cmdI18n.Parameters.Add("@desc", Microsoft.Data.Sqlite.SqliteType.Text);
        var s_cat = cmdI18n.Parameters.Add("@cat", Microsoft.Data.Sqlite.SqliteType.Text);
        var s_kana = cmdI18n.Parameters.Add("@kana", Microsoft.Data.Sqlite.SqliteType.Text);
        var s_pres = cmdI18n.Parameters.Add("@pres", Microsoft.Data.Sqlite.SqliteType.Blob);
        try
        {
            cmdI18n.Prepare();
        }
        catch { }

        using var cmdIdx = conn.CreateCommand();
        cmdIdx.CommandText =
            @"INSERT INTO PolicyIndex(title_norm,desc_norm,title_loose,desc_loose,registry_path,tags) VALUES(@tn,@dn,@tl,@dl,@rp,@tags); SELECT last_insert_rowid();";
        var i_tn = cmdIdx.Parameters.Add("@tn", Microsoft.Data.Sqlite.SqliteType.Text);
        var i_dn = cmdIdx.Parameters.Add("@dn", Microsoft.Data.Sqlite.SqliteType.Text);
        var i_tl = cmdIdx.Parameters.Add("@tl", Microsoft.Data.Sqlite.SqliteType.Text);
        var i_dl = cmdIdx.Parameters.Add("@dl", Microsoft.Data.Sqlite.SqliteType.Text);
        var i_rp = cmdIdx.Parameters.Add("@rp", Microsoft.Data.Sqlite.SqliteType.Text);
        var i_tags = cmdIdx.Parameters.Add("@tags", Microsoft.Data.Sqlite.SqliteType.Text);
        try
        {
            cmdIdx.Prepare();
        }
        catch { }

        using var cmdIdxMap = conn.CreateCommand();
        cmdIdxMap.CommandText =
            "INSERT INTO PolicyIndexMap(rowid,policy_id,culture) VALUES(@rowid,@pid,@culture)";
        var m_rowid = cmdIdxMap.Parameters.Add("@rowid", Microsoft.Data.Sqlite.SqliteType.Integer);
        var m_pid = cmdIdxMap.Parameters.Add("@pid", Microsoft.Data.Sqlite.SqliteType.Integer);
        var m_culture = cmdIdxMap.Parameters.Add("@culture", Microsoft.Data.Sqlite.SqliteType.Text);
        try
        {
            cmdIdxMap.Prepare();
        }
        catch { }

        // Phase 2: insert/update in batches to yield and keep UI responsive.
        const int BatchSize = 500;
        var policies = bundle.Policies.Values.ToList();
        int total = policies.Count;
        int index = 0;
        while (index < total)
        {
            int take = Math.Min(BatchSize, total - index);
            using var txIns = (Microsoft.Data.Sqlite.SqliteTransaction)
                await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted)
                    .ConfigureAwait(false);
            try
            {
                // Enlist prepared commands in this transaction.
                cmdPolicy.Transaction = txIns;
                cmdI18n.Transaction = txIns;
                cmdIdx.Transaction = txIns;
                cmdIdxMap.Transaction = txIns;

                for (int i0 = 0; i0 < take; i0++)
                {
                    ct.ThrowIfCancellationRequested();
                    var pol = policies[index + i0];
                    await UpsertOnePolicyAsync(
                            conn,
                            pol,
                            culture,
                            ct,
                            cmdPolicy,
                            p_ns,
                            p_name,
                            p_cat,
                            p_hive,
                            p_rkey,
                            p_rval,
                            p_vtype,
                            p_ph,
                            cmdI18n,
                            s_pid,
                            s_culture,
                            s_dname,
                            s_desc,
                            s_cat,
                            s_kana,
                            s_pres,
                            cmdIdx,
                            i_tn,
                            i_dn,
                            i_tl,
                            i_dl,
                            i_rp,
                            i_tags,
                            cmdIdxMap,
                            m_rowid,
                            m_pid,
                            m_culture
                        )
                        .ConfigureAwait(false);
                }

                await txIns.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    await txIns.RollbackAsync(ct).ConfigureAwait(false);
                }
                catch { }
                // Abort remaining batches on failure to avoid inconsistent partial state.
                break;
            }

            index += take;
            // Yield to allow UI thread and other tasks to run between batches.
            await Task.Yield();
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

    private async Task<string?> GetMetaAsync(string key, CancellationToken ct)
    {
        try
        {
            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM Meta WHERE key=@k LIMIT 1";
            cmd.Parameters.AddWithValue("@k", key);
            var obj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return obj as string;
        }
        catch
        {
            return null;
        }
    }

    // Builds a stable signature of relevant source inputs for a culture (ADMX + culture-specific ADML).
    private static Task<string> ComputeSourceSignatureAsync(
        string root,
        string culture,
        CancellationToken ct
    )
    {
        // Accumulate: file relative path + length + lastWriteUtcTicks
        var sb = new StringBuilder(4096);
        try
        {
            foreach (
                var f in Directory.EnumerateFiles(root, "*.admx", SearchOption.TopDirectoryOnly)
            )
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(f);
                    sb.Append(Path.GetFileName(f))
                        .Append('|')
                        .Append(fi.Length)
                        .Append('|')
                        .Append(fi.LastWriteTimeUtc.Ticks)
                        .Append('\n');
                }
                catch { }
            }
            var admlDir = Path.Combine(root, culture);
            if (Directory.Exists(admlDir))
            {
                foreach (
                    var f in Directory.EnumerateFiles(
                        admlDir,
                        "*.adml",
                        SearchOption.TopDirectoryOnly
                    )
                )
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(f);
                        sb.Append(culture)
                            .Append('/')
                            .Append(Path.GetFileName(f))
                            .Append('|')
                            .Append(fi.Length)
                            .Append('|')
                            .Append(fi.LastWriteTimeUtc.Ticks)
                            .Append('\n');
                    }
                    catch { }
                }
            }
        }
        catch { }
        var data = Encoding.UTF8.GetBytes(sb.ToString());
        string sig;
        try
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            sig = Convert.ToHexString(hash);
        }
        catch
        {
            sig = Convert.ToBase64String(data); // Fallback (larger but rare)
        }
        return Task.FromResult(sig);
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

        var dnameStrict = TextNormalization.NormalizeStrict(dname);
        var descStrict = TextNormalization.NormalizeStrict(desc);
        var titleNorm = TextNormalization.ToNGramTokens(dnameStrict);
        var descNorm = TextNormalization.ToNGramTokens(descStrict);
        var titleLoose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(dnameStrict)
        );
        var descLoose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(descStrict)
        );
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

    // Optimized variant using prepared commands created by the caller for batch processing.
    private static async Task UpsertOnePolicyAsync(
        SqliteConnection conn,
        PolicyPlusPolicy pol,
        string culture,
        CancellationToken ct,
        Microsoft.Data.Sqlite.SqliteCommand cmdPolicy,
        Microsoft.Data.Sqlite.SqliteParameter p_ns,
        Microsoft.Data.Sqlite.SqliteParameter p_name,
        Microsoft.Data.Sqlite.SqliteParameter p_cat,
        Microsoft.Data.Sqlite.SqliteParameter p_hive,
        Microsoft.Data.Sqlite.SqliteParameter p_rkey,
        Microsoft.Data.Sqlite.SqliteParameter p_rval,
        Microsoft.Data.Sqlite.SqliteParameter p_vtype,
        Microsoft.Data.Sqlite.SqliteParameter p_ph,
        Microsoft.Data.Sqlite.SqliteCommand cmdI18n,
        Microsoft.Data.Sqlite.SqliteParameter s_pid,
        Microsoft.Data.Sqlite.SqliteParameter s_culture,
        Microsoft.Data.Sqlite.SqliteParameter s_dname,
        Microsoft.Data.Sqlite.SqliteParameter s_desc,
        Microsoft.Data.Sqlite.SqliteParameter s_cat,
        Microsoft.Data.Sqlite.SqliteParameter s_kana,
        Microsoft.Data.Sqlite.SqliteParameter s_pres,
        Microsoft.Data.Sqlite.SqliteCommand cmdIdx,
        Microsoft.Data.Sqlite.SqliteParameter i_tn,
        Microsoft.Data.Sqlite.SqliteParameter i_dn,
        Microsoft.Data.Sqlite.SqliteParameter i_tl,
        Microsoft.Data.Sqlite.SqliteParameter i_dl,
        Microsoft.Data.Sqlite.SqliteParameter i_rp,
        Microsoft.Data.Sqlite.SqliteParameter i_tags,
        Microsoft.Data.Sqlite.SqliteCommand cmdIdxMap,
        Microsoft.Data.Sqlite.SqliteParameter m_rowid,
        Microsoft.Data.Sqlite.SqliteParameter m_pid,
        Microsoft.Data.Sqlite.SqliteParameter m_culture
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

        p_ns.Value = ns;
        p_name.Value = policyName;
        p_cat.Value = catKey;
        p_hive.Value = hive;
        p_rkey.Value = (object?)regKey ?? DBNull.Value;
        p_rval.Value = (object?)regVal ?? DBNull.Value;
        p_vtype.Value = vtype;
        p_ph.Value = productHint;
        var obj = await cmdPolicy.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var policyId = Convert.ToInt64(obj, CultureInfo.InvariantCulture);

        var dname = pol.DisplayName ?? string.Empty;
        var desc = pol.DisplayExplanation ?? string.Empty;
        var catPath = BuildCategoryPath(pol.Category);
        var presJson = pol.Presentation is null ? null : JsonSerializer.Serialize(pol.Presentation);

        s_pid.Value = policyId;
        s_culture.Value = culture;
        s_dname.Value = dname;
        s_desc.Value = desc;
        s_cat.Value = catPath;
        s_kana.Value = DBNull.Value;
        if (presJson is null)
            s_pres.Value = DBNull.Value;
        else
            s_pres.Value = Encoding.UTF8.GetBytes(presJson);
        await cmdI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        var dnameStrict = TextNormalization.NormalizeStrict(dname);
        var descStrict = TextNormalization.NormalizeStrict(desc);
        var titleNorm = TextNormalization.ToNGramTokens(dnameStrict);
        var descNorm = TextNormalization.ToNGramTokens(descStrict);
        var titleLoose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(dnameStrict)
        );
        var descLoose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(descStrict)
        );
        var regPath = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeStrict(GetRegistryPath(hive, regKey, regVal))
        );
        var tags = string.Join(
            ' ',
            new[] { ns, vtype, productHint, policyName }.Where(s0 => !string.IsNullOrWhiteSpace(s0))
        );

        i_tn.Value = titleNorm;
        i_dn.Value = descNorm;
        i_tl.Value = titleLoose;
        i_dl.Value = descLoose;
        i_rp.Value = regPath;
        i_tags.Value = tags;
        var objRow = await cmdIdx.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var rowid = Convert.ToInt64(objRow, CultureInfo.InvariantCulture);

        m_rowid.Value = rowid;
        m_pid.Value = policyId;
        m_culture.Value = culture;
        await cmdIdxMap.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
        var qStrict = TextNormalization.NormalizeStrict(query);
        var norm = TextNormalization.ToNGramTokens(qStrict);
        var loose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(qStrict)
        );

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
            // Sanitize tokens to avoid triggering phrase parsing or column qualifiers in FTS5 MATCH.
            // Remove characters that are special to the MATCH grammar (quotes, colon, brackets, braces, parentheses).
            static string Sanitize(string t)
            {
                if (string.IsNullOrEmpty(t))
                    return string.Empty;
                var sbTok = new StringBuilder(t.Length);
                for (int i = 0; i < t.Length; i++)
                {
                    char ch = t[i];
                    // Keep letters and digits only; drop all punctuation to avoid MATCH grammar conflicts.
                    if (char.IsLetterOrDigit(ch))
                        sbTok.Append(ch);
                    // else drop
                }
                return sbTok.ToString();
            }
            var safeList = tokens
                .Select(Sanitize)
                .Where(s0 => !string.IsNullOrWhiteSpace(s0))
                .ToList();
            if (safeList.Count == 0)
                return string.Empty;
            // With detail=full, space-joining yields a phrase search; tests rely on this behavior for tight matches.
            var inside = string.Join(' ', safeList);
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
        sb.AppendLine("  SELECT m.policy_id AS id, m.culture, s.display_name, 100 AS score");
        sb.AppendLine("  FROM PolicyIndex");
        sb.AppendLine("  JOIN PolicyIndexMap m ON m.rowid = PolicyIndex.rowid");
        sb.AppendLine("  JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=@culture");
        sb.AppendLine("  WHERE m.culture=@culture AND @enableFts = 1");
        if (enableFts && ftsNormCols.Count > 0 && !string.IsNullOrWhiteSpace(matchNorm))
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
        sb.AppendLine("  SELECT m.policy_id AS id, m.culture, s.display_name, 60 AS score");
        sb.AppendLine("  FROM PolicyIndex");
        sb.AppendLine("  JOIN PolicyIndexMap m ON m.rowid = PolicyIndex.rowid");
        sb.AppendLine("  JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=@culture");
        sb.AppendLine("  WHERE m.culture=@culture AND @enableFts = 1");
        if (enableFts && ftsLooseCols.Count > 0 && !string.IsNullOrWhiteSpace(matchLoose))
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
