using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheInitializationService
{
    public static async Task InitializeAsync(
        AdmxCacheStore store,
        AdmxCacheFileUsageStore fileUsageStore,
        CancellationToken ct
    )
    {
        using var _traceInit = AdmxCacheTrace.Scope("InitializeAsync");
        IDisposable? writerLock = null;

        try
        {
            writerLock = await AdmxCacheWriterGate
                .TryAcquireWriterLockAsync(
                    perAttemptTimeout: TimeSpan.FromSeconds(10),
                    maxAttempts: 3,
                    retryDelay: TimeSpan.FromMilliseconds(250),
                    ct
                )
                .ConfigureAwait(false);

            await store.InitializeAsync(ct).ConfigureAwait(false);

            using var conn = store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await fileUsageStore.TryEnsureSchemaOnInitializeAsync(conn, ct).ConfigureAwait(false);

            try
            {
                using var ck = conn.CreateCommand();
                ck.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
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
}
