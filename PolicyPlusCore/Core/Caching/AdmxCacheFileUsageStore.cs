using Microsoft.Data.Sqlite;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal sealed class AdmxCacheFileUsageStore
{
    private const string EnsureSchemaSql =
        @"CREATE TABLE IF NOT EXISTS FileUsage( file_path TEXT PRIMARY KEY, last_access_utc INTEGER NOT NULL, kind INTEGER NOT NULL ); CREATE INDEX IF NOT EXISTS IX_FileUsage_LastAccess ON FileUsage(last_access_utc);";

    private readonly AdmxCacheStore _store;
    private volatile bool _schemaEnsured;

    public AdmxCacheFileUsageStore(AdmxCacheStore store)
    {
        _store = store;
    }

    public async Task TryEnsureSchemaOnInitializeAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (_schemaEnsured)
            return;

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = EnsureSchemaSql;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _schemaEnsured = true;
        }
        catch { }
    }

    private async Task EnsureSchemaIfNeededAsync(
        SqliteConnection conn,
        bool markEnsuredRegardless,
        CancellationToken ct
    )
    {
        if (_schemaEnsured)
            return;

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = EnsureSchemaSql;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _schemaEnsured = true;
        }
        catch
        {
            if (markEnsuredRegardless)
                _schemaEnsured = true;
        }
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = _store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await EnsureSchemaIfNeededAsync((SqliteConnection)conn, markEnsuredRegardless: true, ct)
            .ConfigureAwait(false);
        return (SqliteConnection)conn;
    }

    public async Task UpsertAsync(
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
        try
        {
            // Serialize with other writers. Retry briefly to reduce false negative (0 removals) when a rebuild just finished.
            writerLock = await AdmxCacheWriterGate
                .TryAcquireWriterLockAsync(
                    perAttemptTimeout: TimeSpan.FromSeconds(5),
                    maxAttempts: 3,
                    retryDelay: TimeSpan.FromMilliseconds(150),
                    ct
                )
                .ConfigureAwait(false);
            if (writerLock is null)
                return 0; // Could not acquire within budget.

            using var conn = _store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            // Ensure table exists (no-op if previously created)
            await EnsureSchemaIfNeededAsync((SqliteConnection)conn, markEnsuredRegardless: true, ct)
                .ConfigureAwait(false);

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
}
