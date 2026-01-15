using System.Data;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Admx;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheScanBundleAndUsageService
{
    public static async Task<AdmxBundle> LoadBundleAndUpdateUsageAsync(
        IEnumerable<string> admxFiles,
        string culture,
        Func<CancellationToken, Task<SqliteConnection>> openFileUsageConnectionAsync,
        Func<SqliteConnection, string, int, CancellationToken, Task> upsertFileUsageAsync,
        Func<string, string, string?> deriveAdmlPath,
        Action<string, Exception> traceFileUsageWarning,
        CancellationToken ct
    )
    {
        var bundle = new AdmxBundle { EnableLanguageFallback = false };

        SqliteConnection? usageConn = null;
        try
        {
            foreach (var admx in admxFiles)
            {
                try
                {
                    _ = bundle.LoadFile(admx, culture);

                    usageConn ??= await openFileUsageConnectionAsync(ct).ConfigureAwait(false);

                    // Keep current behavior: begin/commit a transaction per file when possible.
                    System.Data.Common.DbTransaction? usageTx = null;
                    if (usageConn is SqliteConnection uc && uc.State == ConnectionState.Open)
                    {
                        try
                        {
                            usageTx ??= uc.BeginTransaction();
                        }
                        catch
                        {
                            usageTx = null;
                        }
                    }

                    await upsertFileUsageAsync(usageConn, admx, 0, ct).ConfigureAwait(false);
                    try
                    {
                        var admlPath = deriveAdmlPath(admx, culture);
                        if (!string.IsNullOrEmpty(admlPath) && File.Exists(admlPath))
                        {
                            await upsertFileUsageAsync(usageConn, admlPath, 1, ct)
                                .ConfigureAwait(false);
                        }
                    }
                    catch { }

                    try
                    {
                        usageTx?.Commit();
                    }
                    catch (Exception ex)
                    {
                        traceFileUsageWarning("FileUsage transaction commit failed", ex);
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

        return bundle;
    }

    public static async Task RefreshUsageOnlyAsync(
        IEnumerable<string> admxFiles,
        string culture,
        Func<CancellationToken, Task<SqliteConnection>> openFileUsageConnectionAsync,
        Func<SqliteConnection, string, int, CancellationToken, Task> upsertFileUsageAsync,
        Func<string, string, string?> deriveAdmlPath,
        CancellationToken ct
    )
    {
        // Skipped parse: still refresh last-access timestamps.
        try
        {
            SqliteConnection? usageConn = null;
            try
            {
                foreach (var admx in admxFiles)
                {
                    try
                    {
                        usageConn ??= await openFileUsageConnectionAsync(ct).ConfigureAwait(false);
                        await upsertFileUsageAsync(usageConn, admx, 0, ct).ConfigureAwait(false);

                        var admlPath = deriveAdmlPath(admx, culture);
                        if (!string.IsNullOrEmpty(admlPath) && File.Exists(admlPath))
                        {
                            await upsertFileUsageAsync(usageConn, admlPath, 1, ct)
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
}
