using System;
using Microsoft.Data.Sqlite;

namespace PolicyPlusCore.Core;

// Runtime helpers for ADMX cache operations that need process-wide effects.
public static class AdmxCacheRuntime
{
    // Clears all pooled SQLite connections so the DB file can be deleted safely.
    public static void ReleaseSqliteHandles()
    {
        try { SqliteConnection.ClearAllPools(); } catch { }
    }
}
