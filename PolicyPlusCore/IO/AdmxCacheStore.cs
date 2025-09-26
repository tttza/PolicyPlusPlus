using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PolicyPlusCore.IO;

internal sealed class AdmxCacheStore
{
    private readonly string _dbPath;
    private static bool _batteriesInited;

    public AdmxCacheStore(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        if (!_batteriesInited)
        {
            try
            {
                SQLitePCL.Batteries_V2.Init();
            }
            catch { }
            _batteriesInited = true;
        }
    }

    public string ConnectionString =>
        new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // Avoid shared cache mode; default private cache is safer across processes.
            // Fast mode (tests) shortens default timeout.
            DefaultTimeout = IsFastMode ? 3 : 30,
        }.ToString();

    private static bool IsFastMode =>
        string.Equals(
            Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_FAST"),
            "1",
            StringComparison.Ordinal
        );

    // Exposed for lightweight heuristics (e.g., skipping heavy maintenance on tiny fresh DBs).
    internal string DatabasePath => _dbPath;

    public async Task InitializeAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // Phase 1: create schema in DELETE journal mode so base DB file reflects changes immediately.
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                var busy = IsFastMode ? 5000 : 30000; // Shorter wait in test fast mode
                cmd.CommandText =
                    $"PRAGMA journal_mode=DELETE; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout={busy}; PRAGMA page_size=4096; PRAGMA encoding='UTF-8';";
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
        catch { }
        await CreateSchemaAsync(conn, ct).ConfigureAwait(false);

        // Phase 2: switch to WAL for runtime performance.
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                var busy = IsFastMode ? 5000 : 30000;
                cmd.CommandText =
                    $"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout={busy}; PRAGMA page_size=4096;";
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
        catch { }

        await OptimizeAsync(conn, ct).ConfigureAwait(false);
    }

    private static async Task ApplyPragmasAsync(SqliteConnection conn, CancellationToken ct)
    {
        // Apply requested PRAGMAs. Ignore unsupported pragmas.
        string[] pragmas = new[]
        {
            "PRAGMA journal_mode=WAL;",
            "PRAGMA synchronous=NORMAL;",
            "PRAGMA busy_timeout=30000;",
            "PRAGMA page_size=4096;",
            // mmap_size may not be supported; ignore failure
            "PRAGMA mmap_size=3000000000;",
            "PRAGMA encoding='UTF-8';",
        };
        foreach (var p in pragmas)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = p;
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch { }
        }
    }

    private static async Task CreateSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        // Ensure FTS table supports column queries; migrate from detail=none if present.
        try
        {
            using var chk = conn.CreateCommand();
            chk.CommandText =
                "SELECT sql FROM sqlite_master WHERE type='table' AND name='PolicyIndex'";
            var existingSql = (string?)await chk.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (
                !string.IsNullOrEmpty(existingSql)
                && (
                    existingSql.IndexOf("detail=none", StringComparison.OrdinalIgnoreCase) >= 0
                    || existingSql.IndexOf("detail=column", StringComparison.OrdinalIgnoreCase) >= 0
                )
            )
            {
                using var drop = conn.CreateCommand();
                drop.CommandText =
                    "DROP TABLE IF EXISTS PolicyIndex; DROP TABLE IF EXISTS PolicyIndexMap;";
                await drop.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
        catch { }

        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE IF NOT EXISTS Meta( key TEXT PRIMARY KEY, value TEXT );");
        sb.AppendLine(
            "CREATE TABLE IF NOT EXISTS Policies( id INTEGER PRIMARY KEY, ns TEXT, policy_name TEXT, category_key TEXT, hive TEXT, reg_key TEXT, reg_value TEXT, value_type TEXT, supported_min TEXT, supported_max TEXT, deprecated INTEGER, product_hint TEXT, UNIQUE(ns, policy_name) );"
        );
        sb.AppendLine(
            "CREATE INDEX IF NOT EXISTS idx_policies_reg ON Policies(hive, reg_key, reg_value);"
        );
        sb.AppendLine("CREATE INDEX IF NOT EXISTS idx_policies_name ON Policies(policy_name);");
        sb.AppendLine(
            "CREATE TABLE IF NOT EXISTS PolicyI18n( policy_id INTEGER REFERENCES Policies(id), culture TEXT, display_name TEXT, explain_text TEXT, category_path TEXT, reading_kana TEXT, presentation_json BLOB, UNIQUE(policy_id, culture) );"
        );
        sb.AppendLine(
            "CREATE VIRTUAL TABLE IF NOT EXISTS PolicyIndex USING fts5( title_norm, desc_norm, title_loose, desc_loose, registry_path, tags, tokenize='unicode61', detail=full, content='' );"
        );
        sb.AppendLine(
            "CREATE TABLE IF NOT EXISTS PolicyIndexMap( rowid INTEGER PRIMARY KEY, policy_id INTEGER, culture TEXT );"
        );

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task OptimizeAsync(SqliteConnection? conn = null, CancellationToken ct = default)
    {
        if (conn is null)
        {
            using var c = new SqliteConnection(ConnectionString);
            await c.OpenAsync(ct).ConfigureAwait(false);
            using var cmd0 = c.CreateCommand();
            cmd0.CommandText = "PRAGMA optimize;";
            await cmd0.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            using var cmd1 = c.CreateCommand();
            cmd1.CommandText = "VACUUM;";
            await cmd1.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA optimize;";
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public SqliteConnection OpenConnection() => new SqliteConnection(ConnectionString);

    public async Task FtsOptimizeAsync(CancellationToken ct = default)
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO PolicyIndex(PolicyIndex) VALUES('optimize');";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch { }
    }

    // Attempts to reclaim free pages. When forceFullVacuum=true always runs VACUUM; otherwise only if freelist ratio exceeds threshold.
    public async Task CompactAsync(
        bool forceFullVacuum = false,
        double freelistThresholdRatio = 0.25,
        CancellationToken ct = default
    )
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            long pageCount = 0;
            long freeList = 0;
            try
            {
                using (var q = conn.CreateCommand())
                {
                    q.CommandText = "PRAGMA page_count;";
                    var o = await q.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    if (o != null && o != DBNull.Value)
                        pageCount = Convert.ToInt64(
                            o,
                            System.Globalization.CultureInfo.InvariantCulture
                        );
                }
                using (var q = conn.CreateCommand())
                {
                    q.CommandText = "PRAGMA freelist_count;";
                    var o = await q.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    if (o != null && o != DBNull.Value)
                        freeList = Convert.ToInt64(
                            o,
                            System.Globalization.CultureInfo.InvariantCulture
                        );
                }
            }
            catch { }

            bool shouldVacuum = forceFullVacuum;
            if (!shouldVacuum && pageCount > 0 && freeList > 0)
            {
                var ratio = (double)freeList / pageCount;
                if (ratio >= freelistThresholdRatio)
                    shouldVacuum = true;
            }

            if (shouldVacuum)
            {
                try
                {
                    using var ck = conn.CreateCommand();
                    ck.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    await ck.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                catch { }
                try
                {
                    using var vac = conn.CreateCommand();
                    vac.CommandText = "VACUUM;";
                    await vac.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                catch { }
            }
            else
            {
                try
                {
                    using var opt = conn.CreateCommand();
                    opt.CommandText = "PRAGMA optimize;";
                    await opt.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                catch { }
            }
        }
        catch { }
    }
}
