namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheWriterGate
{
    public static async Task<IDisposable?> TryAcquireWriterLockAsync(
        TimeSpan perAttemptTimeout,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken ct
    )
    {
        if (IsFastMode)
            return NoopLock.Instance;

        IDisposable? writerLock = null;
        for (int attempt = 0; attempt < maxAttempts && writerLock is null; attempt++)
        {
            writerLock = AdmxCacheRuntime.TryAcquireWriterLock(perAttemptTimeout);
            if (writerLock is null)
            {
                try
                {
                    await Task.Delay(retryDelay, ct).ConfigureAwait(false);
                }
                catch { }
            }
        }

        return writerLock;
    }

    private static bool IsFastMode =>
        string.Equals(
            Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_FAST"),
            "1",
            StringComparison.Ordinal
        );

    private sealed class NoopLock : IDisposable
    {
        public static readonly NoopLock Instance = new();

        private NoopLock() { }

        public void Dispose() { }
    }
}
