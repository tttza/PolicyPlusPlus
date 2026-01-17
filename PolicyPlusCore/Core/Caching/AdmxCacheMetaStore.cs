using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheMetaStore
{
    public static async Task<bool> NeedsGlobalRebuildAsync(
        AdmxCacheStore store,
        string currentRoot,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = store.OpenConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM Meta WHERE key='source_root' LIMIT 1";
            var obj = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            var stored = obj as string;
            if (string.IsNullOrWhiteSpace(stored))
                return true; // first run

            // Compare case-insensitively and with normalized full paths where possible.
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
            // On any error, fall back to global rebuild to be safe.
            return true;
        }
    }

    public static async Task SetMetaAsync(
        AdmxCacheStore store,
        string key,
        string value,
        CancellationToken ct
    )
    {
        using var conn = store.OpenConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Meta(key, value) VALUES(@k,@v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static async Task<string?> GetMetaAsync(
        AdmxCacheStore store,
        string key,
        CancellationToken ct
    )
    {
        try
        {
            using var conn = store.OpenConnection();
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
}
