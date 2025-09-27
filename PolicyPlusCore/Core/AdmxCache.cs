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

    // Tracks whether FileUsage schema ensured to avoid repeating DDL each scan.
    private volatile bool _fileUsageSchemaEnsured;

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
        using var _traceInit = AdmxCacheTrace.Scope("InitializeAsync");
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
            // Heavy optimize (VACUUM) is skipped here; a brand new or tiny DB gains nothing and added ~30s in tests.
            // A later opportunistic maintenance phase will run after substantial data is inserted.
            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            // Proactively ensure FileUsage schema so tests can query it immediately after Initialize without waiting for first scan.
            try
            {
                using var cmdFU = conn.CreateCommand();
                cmdFU.CommandText =
                    @"CREATE TABLE IF NOT EXISTS FileUsage( file_path TEXT PRIMARY KEY, last_access_utc INTEGER NOT NULL, kind INTEGER NOT NULL ); CREATE INDEX IF NOT EXISTS IX_FileUsage_LastAccess ON FileUsage(last_access_utc);";
                await cmdFU.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                _fileUsageSchemaEnsured = true;
            }
            catch { }
            try
            {
                using var ck = conn.CreateCommand();
                ck.CommandText = "PRAGMA wal_checkpoint(PASSIVE);"; // Passive to avoid forcing truncation on empty DB.
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
        using var _traceScan = AdmxCacheTrace.Scope("ScanAndUpdateAsync.Total");
        bool maintenanceDisabled = string.Equals(
            Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_DISABLE_MAINT"),
            "1",
            StringComparison.Ordinal
        );
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
            using var _traceCulture = AdmxCacheTrace.Scope("ScanAndUpdateAsync.Culture:" + cul);
            bool allowGlobalRebuild = needGlobalRebuild && (i == 0);

            // Strategy A: Do not persist fallback. If the exact culture directory (root\culture) does not exist OR contains no *.adml,
            // treat this culture as absent: remove any prior persisted rows for this culture (they may have been fallback-injected by earlier versions) and skip insertion.
            try
            {
                var cultureDir = Path.Combine(root, cul);
                bool cultureHasAdml =
                    Directory.Exists(cultureDir)
                    && Directory
                        .EnumerateFiles(cultureDir, "*.adml", SearchOption.TopDirectoryOnly)
                        .Any();
                if (!cultureHasAdml)
                {
                    await PurgeCultureAsync(cul, ct).ConfigureAwait(false);
                    i++;
                    continue; // Skip parse/apply for this culture (fallback handled at query time)
                }
            }
            catch { }

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
                bundle.EnableLanguageFallback = false; // prevent cross-culture fallback persistence
                SqliteConnection? usageConn = null;
                try
                {
                    foreach (var admx in EnumerateAdmxFilesFiltered(root))
                    {
                        try
                        {
                            _ = bundle.LoadFile(admx, cul);
                            usageConn ??= await OpenFileUsageConnectionAsync(ct)
                                .ConfigureAwait(false);
                            await UpsertFileUsageAsync(usageConn, admx, 0, ct)
                                .ConfigureAwait(false);
                            try
                            {
                                var admlPath = DeriveAdmlPath(admx, cul);
                                if (!string.IsNullOrEmpty(admlPath) && File.Exists(admlPath))
                                {
                                    await UpsertFileUsageAsync(usageConn, admlPath, 1, ct)
                                        .ConfigureAwait(false);
                                }
                            }
                            catch { }
                        }
                        catch { }
                    }
                }
                catch { }
                finally
                {
                    try
                    {
                        usageConn?.Dispose();
                    }
                    catch { }
                }
            }
            else
            {
                // Even if we skip a full parse due to unchanged signature, still refresh last access timestamps.
                try
                {
                    SqliteConnection? usageConn = null;
                    try
                    {
                        foreach (var admx in EnumerateAdmxFilesFiltered(root))
                        {
                            try
                            {
                                usageConn ??= await OpenFileUsageConnectionAsync(ct)
                                    .ConfigureAwait(false);
                                await UpsertFileUsageAsync(usageConn, admx, 0, ct)
                                    .ConfigureAwait(false);
                                var admlPath = DeriveAdmlPath(admx, cul);
                                if (!string.IsNullOrEmpty(admlPath) && File.Exists(admlPath))
                                {
                                    await UpsertFileUsageAsync(usageConn, admlPath, 1, ct)
                                        .ConfigureAwait(false);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                    finally
                    {
                        try
                        {
                            usageConn?.Dispose();
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (!skipCulture)
            {
                if (allowGlobalRebuild)
                    didGlobalRebuild = true;
                using var _traceDiffApply = AdmxCacheTrace.Scope(
                    "DiffAndApply:" + cul + (allowGlobalRebuild ? "(global)" : "")
                );
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

        if (!maintenanceDisabled)
        {
            // Opportunistic maintenance (skipped for tiny DBs to keep tests fast).
            try
            {
                using var mconn = _store.OpenConnection();
                await mconn.OpenAsync(ct).ConfigureAwait(false);
                long pageCount = 0;
                try
                {
                    using var pc = mconn.CreateCommand();
                    pc.CommandText = "PRAGMA page_count;";
                    var o = await pc.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    if (o != null && o != DBNull.Value)
                        pageCount = Convert.ToInt64(
                            o,
                            System.Globalization.CultureInfo.InvariantCulture
                        );
                }
                catch { }

                const int MinPagesForMaintenance = 128; // Skip optimize/compact when extremely small.
                if (pageCount >= MinPagesForMaintenance)
                {
                    try
                    {
                        await _store.OptimizeAsync(mconn, ct).ConfigureAwait(false);
                    }
                    catch { }
                    try
                    {
                        using var ck = mconn.CreateCommand();
                        ck.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        await ck.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    catch { }
                    try
                    {
                        await _store.FtsOptimizeAsync(ct).ConfigureAwait(false);
                    }
                    catch { }
                    try
                    {
                        await _store
                            .CompactAsync(forceFullVacuum: false, freelistThresholdRatio: 0.30, ct)
                            .ConfigureAwait(false);
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        using var ck2 = mconn.CreateCommand();
                        ck2.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
                        await ck2.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    // Allows test environment to restrict which ADMX files are parsed to accelerate indexing.
    // If POLICYPLUS_CACHE_ONLY_FILES is set (semicolon separated list of file names), only those are yielded.
    private static IEnumerable<string> EnumerateAdmxFilesFiltered(string root)
    {
        var filterEnv = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES");
        if (!string.IsNullOrWhiteSpace(filterEnv))
        {
            var allowedSet = new HashSet<string>(
                filterEnv.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var name in allowedSet)
            {
                string p;
                try
                {
                    p = Path.Combine(root, name);
                }
                catch
                {
                    continue;
                }
                if (File.Exists(p))
                    yield return p;
            }
            yield break;
        }
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.admx", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }
        foreach (var f in files)
            yield return f;
    }

    // Deletes localized rows (PolicyI18n + FTS index rows) for a culture if present. No-op on failure.
    private async Task PurgeCultureAsync(string culture, CancellationToken ct)
    {
        bool fastMode = string.Equals(
            Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_FAST"),
            "1",
            StringComparison.Ordinal
        );
        IDisposable? writerLock = null;
        if (!fastMode)
        {
            for (int attempt = 0; attempt < 3 && writerLock is null; attempt++)
            {
                writerLock = AdmxCacheRuntime.TryAcquireWriterLock(TimeSpan.FromSeconds(5));
                if (writerLock is null)
                {
                    try
                    {
                        await Task.Delay(150, ct).ConfigureAwait(false);
                    }
                    catch { }
                }
            }
        }
        using var _wl = writerLock; // null allowed
        try
        {
            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            using var tx = (Microsoft.Data.Sqlite.SqliteTransaction)
                await conn.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted)
                    .ConfigureAwait(false);
            try
            {
                // Collect rowids for FTS delete
                var rowIds = new List<long>(64);
                using (var getRows = conn.CreateCommand())
                {
                    getRows.Transaction = tx;
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
                    delFts.Transaction = tx;
                    delFts.CommandText =
                        "INSERT INTO PolicyIndex(PolicyIndex, rowid) VALUES('delete', @rid);";
                    var pRid = delFts.Parameters.Add(
                        "@rid",
                        Microsoft.Data.Sqlite.SqliteType.Integer
                    );
                    foreach (var rid in rowIds)
                    {
                        pRid.Value = rid;
                        try
                        {
                            await delFts.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                        }
                        catch { }
                    }
                }
                using (var delMap = conn.CreateCommand())
                {
                    delMap.Transaction = tx;
                    delMap.CommandText = "DELETE FROM PolicyIndexMap WHERE culture=@c;";
                    delMap.Parameters.AddWithValue("@c", culture);
                    try
                    {
                        await delMap.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    catch { }
                }
                using (var delI18n = conn.CreateCommand())
                {
                    delI18n.Transaction = tx;
                    delI18n.CommandText = "DELETE FROM PolicyI18n WHERE culture=@c;";
                    delI18n.Parameters.AddWithValue("@c", culture);
                    try
                    {
                        await delI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    catch { }
                }
                try
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                }
                catch { }
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
        catch { }
    }

    // Attempts to derive the ADML path for an ADMX given a culture.
    private static string? DeriveAdmlPath(string admxPath, string culture)
    {
        try
        {
            var dir = Path.GetDirectoryName(admxPath);
            if (string.IsNullOrEmpty(dir))
                return null;
            var cultureDir = Path.Combine(dir, culture);
            var fileName = Path.GetFileNameWithoutExtension(admxPath);
            var candidate = Path.Combine(cultureDir, fileName + ".adml");
            return candidate;
        }
        catch
        {
            return null;
        }
    }

    private async Task<SqliteConnection> OpenFileUsageConnectionAsync(CancellationToken ct)
    {
        var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        if (!_fileUsageSchemaEnsured)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    @"CREATE TABLE IF NOT EXISTS FileUsage( file_path TEXT PRIMARY KEY, last_access_utc INTEGER NOT NULL, kind INTEGER NOT NULL ); CREATE INDEX IF NOT EXISTS IX_FileUsage_LastAccess ON FileUsage(last_access_utc);";
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch { }
            _fileUsageSchemaEnsured = true;
        }
        return (SqliteConnection)conn;
    }

    private static async Task UpsertFileUsageAsync(
        SqliteConnection conn,
        string path,
        int kind,
        CancellationToken ct
    )
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                @"INSERT INTO FileUsage(file_path,last_access_utc,kind) VALUES(@p,@now,@k) ON CONFLICT(file_path) DO UPDATE SET last_access_utc=excluded.last_access_utc;";
            cmd.Parameters.AddWithValue("@p", path);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@k", kind);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch { }
    }

    public async Task<int> PurgeStaleCacheEntriesAsync(
        TimeSpan olderThan,
        CancellationToken ct = default
    )
    {
        int removed = 0;
        IDisposable? writerLock = null;
        bool fastMode = string.Equals(
            Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_FAST"),
            "1",
            StringComparison.Ordinal
        );
        try
        {
            if (!fastMode)
            {
                // Serialize with other writers. Retry briefly to reduce false negative (0 removals) when a rebuild just finished.
                for (int attempt = 0; attempt < 3 && writerLock is null; attempt++)
                {
                    writerLock = AdmxCacheRuntime.TryAcquireWriterLock(TimeSpan.FromSeconds(5));
                    if (writerLock is null)
                    {
                        try
                        {
                            await Task.Delay(150, ct).ConfigureAwait(false);
                        }
                        catch { }
                    }
                }
                if (writerLock is null)
                    return 0; // Could not acquire within budget.
            }
            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            // Ensure table exists (no-op if previously created)
            if (!_fileUsageSchemaEnsured)
            {
                try
                {
                    using var cmdEnsure = conn.CreateCommand();
                    cmdEnsure.CommandText =
                        @"CREATE TABLE IF NOT EXISTS FileUsage( file_path TEXT PRIMARY KEY, last_access_utc INTEGER NOT NULL, kind INTEGER NOT NULL ); CREATE INDEX IF NOT EXISTS IX_FileUsage_LastAccess ON FileUsage(last_access_utc);";
                    await cmdEnsure.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                catch { }
                _fileUsageSchemaEnsured = true;
            }
            var threshold = DateTimeOffset.UtcNow.Subtract(olderThan).ToUnixTimeSeconds();
            var stale = new List<string>(64);
            try
            {
                using var sel = conn.CreateCommand();
                sel.CommandText = "SELECT file_path FROM FileUsage WHERE last_access_utc < @t";
                sel.Parameters.AddWithValue("@t", threshold);
                using var r = await sel.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await r.ReadAsync(ct).ConfigureAwait(false))
                {
                    if (!r.IsDBNull(0))
                        stale.Add(r.GetString(0));
                }
            }
            catch { }
            if (stale.Count == 0)
                return 0;
            try
            {
                using var tx = conn.BeginTransaction();
                using var del = conn.CreateCommand();
                del.Transaction = tx;
                del.CommandText = "DELETE FROM FileUsage WHERE file_path=@p";
                var p = del.Parameters.Add("@p", SqliteType.Text);
                foreach (var s in stale)
                {
                    p.Value = s;
                    try
                    {
                        await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                        removed++;
                    }
                    catch { }
                }
                try
                {
                    tx.Commit();
                }
                catch { }
            }
            catch { }
        }
        catch { }
        finally
        {
            try
            {
                writerLock?.Dispose();
            }
            catch { }
        }
        return removed;
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
        bool fastMode = string.Equals(
            Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_FAST"),
            "1",
            StringComparison.Ordinal
        );
        IDisposable? writerLock = null;
        if (!fastMode)
        {
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
                            "DELETE FROM PolicyIndexMap; DELETE FROM PolicyI18n; DELETE FROM Policies;";
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
        try
        {
            var capEnv = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_MAX_POLICIES");
            if (
                !string.IsNullOrWhiteSpace(capEnv)
                && int.TryParse(capEnv, out var maxPol)
                && maxPol > 0
                && policies.Count > maxPol
            )
            {
                policies = policies.Take(maxPol).ToList();
            }
        }
        catch { }
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
        var sb = new StringBuilder(1024);
        HashSet<string>? allowed = null;
        try
        {
            var filter = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES");
            if (!string.IsNullOrWhiteSpace(filter))
                allowed = new HashSet<string>(
                    filter.Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    ),
                    StringComparer.OrdinalIgnoreCase
                );
        }
        catch { }
        try
        {
            if (allowed != null)
            {
                foreach (var fn in allowed)
                {
                    ct.ThrowIfCancellationRequested();
                    string path;
                    try
                    {
                        path = Path.Combine(root, fn);
                    }
                    catch
                    {
                        continue;
                    }
                    if (!File.Exists(path))
                        continue;
                    try
                    {
                        var fi = new FileInfo(path);
                        sb.Append(fn)
                            .Append('|')
                            .Append(fi.Length)
                            .Append('|')
                            .Append(fi.LastWriteTimeUtc.Ticks)
                            .Append('\n');
                    }
                    catch { }
                }
            }
            else
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
            }
            var admlDir = Path.Combine(root, culture);
            if (Directory.Exists(admlDir))
            {
                if (allowed != null)
                {
                    foreach (var fn in allowed)
                    {
                        ct.ThrowIfCancellationRequested();
                        var admlName = Path.GetFileNameWithoutExtension(fn) + ".adml";
                        string path;
                        try
                        {
                            path = Path.Combine(admlDir, admlName);
                        }
                        catch
                        {
                            continue;
                        }
                        if (!File.Exists(path))
                            continue;
                        try
                        {
                            var fi = new FileInfo(path);
                            sb.Append(culture)
                                .Append('/')
                                .Append(admlName)
                                .Append('|')
                                .Append(fi.Length)
                                .Append('|')
                                .Append(fi.LastWriteTimeUtc.Ticks)
                                .Append('\n');
                        }
                        catch { }
                    }
                }
                else
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
        }
        catch { }
        var data = Encoding.UTF8.GetBytes(sb.ToString());
        string sig;
        try
        {
            using var sha = SHA256.Create();
            sig = Convert.ToHexString(sha.ComputeHash(data));
        }
        catch
        {
            sig = Convert.ToBase64String(data);
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

        if (string.IsNullOrWhiteSpace(dname))
        {
            // Do not persist fallback-derived or empty localization rows for this culture.
            return;
        }

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

        if (string.IsNullOrWhiteSpace(dname))
        {
            return; // skip fallback/empty localization persistence
        }

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

    public async Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        IReadOnlyList<string> cultures,
        SearchFields fields,
        int limit = 50,
        CancellationToken ct = default
    ) =>
        await SearchAsync(query, cultures, fields, andMode: false, limit, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<PolicyHit>> SearchAsync(
        string query,
        IReadOnlyList<string> cultures,
        SearchFields fields,
        bool andMode,
        int limit = 50,
        CancellationToken ct = default
    )
    {
        if (cultures == null || cultures.Count == 0)
            cultures = new[] { CultureInfo.CurrentUICulture.Name };
        if (string.IsNullOrWhiteSpace(query) || fields == SearchFields.None)
            return Array.Empty<PolicyHit>();

        var qExact = query;
        var qPrefix = query + "%";
        var qStrict = TextNormalization.NormalizeStrict(query);
        var norm = TextNormalization.ToNGramTokens(qStrict);
        var loose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(qStrict)
        );

        bool useId = (fields & SearchFields.Id) != 0;
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

        static string EscapeSqlSingle(string s) => s.Replace("'", "''");
        static IEnumerable<string> SplitTokens(string grams) =>
            grams.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
        static string SanitizeTok(string t)
        {
            if (string.IsNullOrEmpty(t))
                return string.Empty;
            var sb = new StringBuilder(t.Length);
            foreach (var ch in t)
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
            return sb.ToString();
        }
        static string BuildFtsOr(List<string> cols, IEnumerable<string> rawTokens)
        {
            var toks = rawTokens
                .Select(SanitizeTok)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (cols.Count == 0 || toks.Count == 0)
                return string.Empty;
            // For each token build (col1:(tok) OR col2:(tok) ...) and join all with OR
            var perTok = new List<string>(toks.Count);
            foreach (var t in toks)
                perTok.Add("(" + string.Join(" OR ", cols.Select(c => c + ":(" + t + ")")) + ")");
            return string.Join(" OR ", perTok);
        }
        static string BuildFtsAnd(List<string> cols, IEnumerable<string> rawTokens)
        {
            var toks = rawTokens
                .Select(SanitizeTok)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (cols.Count == 0 || toks.Count == 0)
                return string.Empty;
            var per = new List<string>(toks.Count);
            foreach (var t in toks)
                per.Add("(" + string.Join(" OR ", cols.Select(c => c + ":(" + t + ")")) + ")");
            return string.Join(" AND ", per);
        }
        var normTokens = SplitTokens(norm);
        var looseTokens = SplitTokens(loose);

        // Build expression requiring ALL tokens for a segment: ( (c1:(g1) OR c2:(g1)) AND (c1:(g2) OR c2:(g2)) ... )
        static string BuildPerSegmentAllGrams(
            List<string> cols,
            IEnumerable<string> rawTokens,
            int maxGrams = 8
        )
        {
            if (cols.Count == 0)
                return string.Empty;
            var toks = rawTokens
                .Select(SanitizeTok)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(maxGrams) // cap to avoid very large FTS expressions on long segments
                .ToList();
            if (toks.Count == 0)
                return string.Empty;
            if (toks.Count == 1)
                return "(" + string.Join(" OR ", cols.Select(c => c + ":(" + toks[0] + ")")) + ")";
            var per = new List<string>(toks.Count);
            foreach (var t in toks)
                per.Add("(" + string.Join(" OR ", cols.Select(c => c + ":(" + t + ")")) + ")");
            return "(" + string.Join(" AND ", per) + ")";
        }
        static string BuildFtsSegmentedAnd(List<string> cols, IEnumerable<string> segments)
        {
            if (cols.Count == 0)
                return string.Empty;
            var segExprs = new List<string>();
            foreach (var seg in segments)
            {
                if (string.IsNullOrWhiteSpace(seg))
                    continue;
                var grams = TextNormalization.ToNGramTokens(seg);
                if (string.IsNullOrWhiteSpace(grams))
                {
                    // Fallback: if the segment is 1 char (cannot produce 2-gram), use the raw char directly.
                    if (seg.Length == 1)
                    {
                        var tok = SanitizeTok(seg);
                        if (!string.IsNullOrWhiteSpace(tok))
                            grams = tok;
                    }
                }
                var toks = string.IsNullOrEmpty(grams)
                    ? Array.Empty<string>()
                    : grams.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    );
                if (toks.Length > 0)
                {
                    // Heuristic: if any 3-grams exist, drop 2-grams to improve precision (common 2-grams cause broad matches in Japanese).
                    bool hasTri = toks.Any(t => t.Length == 3);
                    if (hasTri)
                    {
                        var filtered = toks.Where(t => t.Length == 3).Take(10).ToArray(); // cap per segment
                        if (filtered.Length > 0)
                            toks = filtered;
                    }
                }
                if (toks.Length == 0)
                    continue;
                var expr = BuildPerSegmentAllGrams(cols, toks);
                if (!string.IsNullOrWhiteSpace(expr))
                    segExprs.Add(expr); // already wrapped
            }
            if (segExprs.Count == 0)
                return string.Empty;
            return string.Join(" AND ", segExprs);
        }

        string matchNorm;
        string matchLoose;
        if (andMode && qStrict.IndexOf(' ') >= 0)
        {
            var segmentsStrict = qStrict.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            matchNorm = BuildFtsSegmentedAnd(ftsNormCols, segmentsStrict);
            // For loose, re-normalize each strict segment into loose form then build.
            var segmentsLoose = segmentsStrict.Select(s =>
                TextNormalization.NormalizeLooseFromStrict(s)
            );
            matchLoose = BuildFtsSegmentedAnd(ftsLooseCols, segmentsLoose);
            // If segmentation produced nothing (e.g., all 1-char segments), fall back to original behavior.
            if (string.IsNullOrWhiteSpace(matchNorm))
                matchNorm = BuildFtsAnd(ftsNormCols, normTokens);
            if (string.IsNullOrWhiteSpace(matchLoose))
                matchLoose = BuildFtsAnd(ftsLooseCols, looseTokens);
        }
        else
        {
            // Precision tweak: if searching only by Name (no Id/Registry/Description) and not explicitly in AND mode,
            // require all grams (AND) instead of any (OR). The previous OR behavior caused false positives where a token
            // chosen from description (not present in display name) shared a single common 2-gram with the title, making
            // the Name-only search incorrectly match. This aligns with integration test expectation that a description-only
            // token will not hit a Name-only search.
            bool nameOnly = useName && !useDesc && !useReg && !useId;
            if (!andMode && nameOnly)
            {
                matchNorm = BuildFtsAnd(ftsNormCols, normTokens);
                matchLoose = BuildFtsAnd(ftsLooseCols, looseTokens);
            }
            else
            {
                matchNorm = andMode
                    ? BuildFtsAnd(ftsNormCols, normTokens)
                    : BuildFtsOr(ftsNormCols, normTokens);
                matchLoose = andMode
                    ? BuildFtsAnd(ftsLooseCols, looseTokens)
                    : BuildFtsOr(ftsLooseCols, looseTokens);
            }
        }
        var matchNormEsc = EscapeSqlSingle(matchNorm);
        var matchLooseEsc = EscapeSqlSingle(matchLoose);

        using var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        // Build prioritized culture list (order preserved from caller)
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
        sb.AppendLine(") , ExactPrefix AS (");
        sb.AppendLine(
            "  SELECT NULL AS id, NULL AS culture, '' AS display_name, -1 AS score WHERE 1=0"
        );
        if (useId)
        {
            sb.AppendLine("  UNION ALL");
            sb.AppendLine(
                "  SELECT p.id, s.culture, s.display_name, 1200 FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id WHERE p.policy_name=@q_exact AND s.culture IN (SELECT culture FROM CulturePref)"
            );
            sb.AppendLine("  UNION ALL");
            sb.AppendLine(
                "  SELECT p.id, s.culture, s.display_name, 300 FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id WHERE p.policy_name LIKE @q_prefix AND s.culture IN (SELECT culture FROM CulturePref)"
            );
        }
        if (useReg)
        {
            sb.AppendLine("  UNION ALL");
            sb.AppendLine(
                "  SELECT p.id, s.culture, s.display_name, 1000 FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id WHERE (p.hive||'\\\\'||p.reg_key||'\\\\'||p.reg_value)=@q_exact AND s.culture IN (SELECT culture FROM CulturePref)"
            );
        }
        sb.AppendLine(") , F1 AS (");
        sb.AppendLine(
            "  SELECT m.policy_id AS id, m.culture, s.display_name, 100 AS score FROM PolicyIndex JOIN PolicyIndexMap m ON m.rowid=PolicyIndex.rowid JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=m.culture WHERE @enableFts=1 AND m.culture IN (SELECT culture FROM CulturePref)"
        );
        if (enableFts && ftsNormCols.Count > 0 && !string.IsNullOrWhiteSpace(matchNorm))
            sb.Append("    AND PolicyIndex MATCH '").Append(matchNormEsc).AppendLine("'");
        else
            sb.AppendLine("    AND (0)");
        sb.AppendLine(") , F2 AS (");
        sb.AppendLine(
            "  SELECT m.policy_id AS id, m.culture, s.display_name, 60 AS score FROM PolicyIndex JOIN PolicyIndexMap m ON m.rowid=PolicyIndex.rowid JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=m.culture WHERE @enableFts=1 AND m.culture IN (SELECT culture FROM CulturePref)"
        );
        if (enableFts && ftsLooseCols.Count > 0 && !string.IsNullOrWhiteSpace(matchLoose))
            sb.Append("    AND PolicyIndex MATCH '").Append(matchLooseEsc).AppendLine("'");
        else
            sb.AppendLine("    AND (0)");
        sb.AppendLine(") , Candidates AS (");
        sb.AppendLine("  SELECT id,culture,display_name,score FROM ExactPrefix");
        sb.AppendLine("  UNION ALL SELECT id,culture,display_name,score FROM F1");
        sb.AppendLine("  UNION ALL SELECT id,culture,display_name,score FROM F2");
        // Filtered: allow primary always; allow second always (if provided); allow fallback cultures only when primary row absent for that policy.
        sb.AppendLine(") , Filtered AS (");
        sb.AppendLine("  SELECT c.* FROM Candidates c");
        sb.AppendLine("  WHERE c.culture = @primaryCulture");
        sb.AppendLine("     OR (@secondCulture IS NOT NULL AND c.culture = @secondCulture)");
        sb.AppendLine("     OR ( c.culture <> @primaryCulture");
        sb.AppendLine("          AND (@secondCulture IS NULL OR c.culture <> @secondCulture)");
        sb.AppendLine(
            "          AND NOT EXISTS (SELECT 1 FROM PolicyI18n px WHERE px.policy_id = c.id AND px.culture = @primaryCulture)"
        );
        sb.AppendLine(
            "          AND ( @secondCulture IS NULL OR NOT EXISTS (SELECT 1 FROM PolicyI18n sx WHERE sx.policy_id = c.id AND sx.culture = @secondCulture) )"
        );
        sb.AppendLine("        )");
        sb.AppendLine(") , Ranked AS (");
        sb.AppendLine(
            "  SELECT c.id,c.culture,c.display_name,c.score,cp.prio, ROW_NUMBER() OVER (PARTITION BY c.id ORDER BY cp.prio ASC) AS rnk FROM Filtered c JOIN CulturePref cp ON cp.culture=c.culture"
        );
        sb.AppendLine(
            ") SELECT R.id,R.culture, (SELECT ns||':'||policy_name FROM Policies P WHERE P.id=R.id) AS unique_id,"
        );
        sb.AppendLine("       R.score, R.display_name,");
        sb.AppendLine(
            "       (SELECT hive||'\\\\'||reg_key||'\\\\'||reg_value FROM Policies P2 WHERE P2.id=R.id) AS registry_path,"
        );
        sb.AppendLine(
            "       (SELECT product_hint FROM Policies P3 WHERE P3.id=R.id) AS product_hint,"
        );
        sb.AppendLine("       (SELECT value_type FROM Policies P4 WHERE P4.id=R.id) AS value_type");
        sb.AppendLine("  FROM Ranked R WHERE R.rnk = 1 ORDER BY R.score DESC LIMIT @limit; ");

        var list = new List<PolicyHit>(Math.Min(limit, 256));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@q_exact", qExact);
        cmd.Parameters.AddWithValue("@q_prefix", qPrefix);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@enableFts", enableFts ? 1 : 0);
        for (int i = 0; i < cultures.Count; i++)
            cmd.Parameters.AddWithValue("@c" + i, NormalizeCultureName(cultures[i]));
        // Primary culture = first in list; second = second element if exists; others treated as fallback candidates.
        var primaryCulture =
            cultures.Count > 0
                ? NormalizeCultureName(cultures[0])
                : CultureInfo.CurrentUICulture.Name;
        string? secondCulture = cultures.Count > 1 ? NormalizeCultureName(cultures[1]) : null;
        if (
            secondCulture != null
            && string.Equals(secondCulture, primaryCulture, StringComparison.OrdinalIgnoreCase)
        )
        {
            // Explicit duplicate primary acts as placeholder second – treat as absent so fallback suppression logic engages correctly.
            secondCulture = null;
        }
        cmd.Parameters.AddWithValue("@primaryCulture", primaryCulture);
        var pSecond = cmd.CreateParameter();
        pSecond.ParameterName = "@secondCulture";
        if (secondCulture == null)
            pSecond.Value = DBNull.Value;
        else
            pSecond.Value = secondCulture;
        cmd.Parameters.Add(pSecond);
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

    public async Task<PolicyDetail?> GetByPolicyNameAsync(
        string ns,
        string policyName,
        IReadOnlyList<string> cultures,
        CancellationToken ct = default
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
            cmd.Parameters.AddWithValue("@c" + i, NormalizeCultureName(cultures[i]));
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

    public async Task<PolicyDetail?> GetByRegistryPathAsync(
        string registryPath,
        IReadOnlyList<string> cultures,
        CancellationToken ct = default
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
            cmd.Parameters.AddWithValue("@c" + i, NormalizeCultureName(cultures[i]));
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
