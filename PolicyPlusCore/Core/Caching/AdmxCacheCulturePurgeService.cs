using System.Data;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheCulturePurgeService
{
    public static async Task PurgeCultureAsync(
        AdmxCacheStore store,
        string culture,
        CancellationToken ct
    )
    {
        var writerLock = await AdmxCacheWriterGate
            .TryAcquireWriterLockAsync(
                perAttemptTimeout: TimeSpan.FromSeconds(5),
                maxAttempts: 3,
                retryDelay: TimeSpan.FromMilliseconds(150),
                ct
            )
            .ConfigureAwait(false);
        using var _wl = writerLock; // null allowed

        try
        {
            using var conn = store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            using var tx = (SqliteTransaction)
                await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted)
                    .ConfigureAwait(false);

            try
            {
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
                    var pRid = delFts.Parameters.Add("@rid", SqliteType.Integer);
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
}
