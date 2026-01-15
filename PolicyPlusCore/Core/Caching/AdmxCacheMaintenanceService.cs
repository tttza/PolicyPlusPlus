using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheMaintenanceService
{
    public static async Task RunOpportunisticMaintenanceAsync(
        AdmxCacheStore store,
        CancellationToken ct
    )
    {
        // Opportunistic maintenance (skipped for very small DBs).
        try
        {
            using var mconn = store.OpenConnection();
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
                    await store.OptimizeAsync(mconn, ct).ConfigureAwait(false);
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
                    await store.FtsOptimizeAsync(ct).ConfigureAwait(false);
                }
                catch { }
                try
                {
                    await store
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
