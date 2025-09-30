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
        try
        {
            SqliteConnection.ClearAllPools();
        }
        catch { }
    }

    // Provides a cross-process writer lock to serialize cache rebuilds and mutations.
    public static IDisposable? TryAcquireWriterLock(TimeSpan timeout)
    {
        // Allow test or diagnostic scenarios to disable or rename the writer mutex to avoid
        // unintended cross-process contention (e.g. UI tests running alongside unit tests).
        // POLICYPLUS_CACHE_DISABLE_WRITER_LOCK=1 -> skip acquiring (acts as if acquired).
        // POLICYPLUS_CACHE_WRITER_MUTEX_NAME=<custom> -> use an alternate name (still global).
        // These overrides intentionally keep default production behavior when unset.
        try
        {
            var disable = Environment.GetEnvironmentVariable(
                "POLICYPLUS_CACHE_DISABLE_WRITER_LOCK"
            );
            if (string.Equals(disable, "1", StringComparison.Ordinal))
            {
                return new Noop();
            }
        }
        catch { }
        string name = @"Local\PolicyPlusPlus.AdmxCache.Writer";
        try
        {
            var overrideName = Environment.GetEnvironmentVariable(
                "POLICYPLUS_CACHE_WRITER_MUTEX_NAME"
            );
            if (!string.IsNullOrWhiteSpace(overrideName))
            {
                name = overrideName!;
            }
        }
        catch { }
        Mutex? m = null;
        try
        {
            m = new Mutex(initiallyOwned: false, name);
            bool acquired;
            try
            {
                acquired = m.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            if (!acquired)
            {
                m.Dispose();
                return null;
            }
            return new Releaser(m);
        }
        catch
        {
            try
            {
                m?.Dispose();
            }
            catch { }
            return null;
        }
    }

    private sealed class Releaser : IDisposable
    {
        private Mutex? _m;

        public Releaser(Mutex m)
        {
            _m = m;
        }

        public void Dispose()
        {
            var m = Interlocked.Exchange(ref _m, null);
            if (m == null)
                return;
            try
            {
                m.ReleaseMutex();
            }
            catch { }
            try
            {
                m.Dispose();
            }
            catch { }
        }
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose() { }
    }
}
