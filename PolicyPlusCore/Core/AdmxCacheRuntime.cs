using System;
using System.Threading;
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

    // Provides a cross-process writer lock to serialize cache rebuilds and mutations.
    public static IDisposable? TryAcquireWriterLock(TimeSpan timeout)
    {
        const string name = @"Local\PolicyPlusPlus.AdmxCache.Writer";
        Mutex? m = null;
        try
        {
            m = new Mutex(initiallyOwned: false, name);
            bool acquired;
            try { acquired = m.WaitOne(timeout); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
            {
                m.Dispose();
                return null;
            }
            return new Releaser(m);
        }
        catch
        {
            try { m?.Dispose(); } catch { }
            return null;
        }
    }

    private sealed class Releaser : IDisposable
    {
        private Mutex? _m;
        public Releaser(Mutex m) { _m = m; }
        public void Dispose()
        {
            var m = Interlocked.Exchange(ref _m, null);
            if (m == null) return;
            try { m.ReleaseMutex(); } catch { }
            try { m.Dispose(); } catch { }
        }
    }
}
