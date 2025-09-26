using System;
using System.Diagnostics;

namespace PolicyPlusCore.Core;

// Lightweight conditional tracing for AdmxCache performance diagnostics.
internal static class AdmxCacheTrace
{
    private sealed class DummyScope : IDisposable
    {
        public static readonly DummyScope Instance = new DummyScope();

        public void Dispose() { }
    }

    private sealed class RealScope : IDisposable
    {
        private readonly string _name;
        private readonly long _start;

        public RealScope(string name)
        {
            _name = name;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var end = Stopwatch.GetTimestamp();
            var elapsedMs = (end - _start) * 1000.0 / Stopwatch.Frequency;
            var msg =
                $"[AdmxCacheTrace] {_name} took {elapsedMs:F2} ms (T{Environment.CurrentManagedThreadId})";
            try
            {
                Debug.WriteLine(msg);
            }
            catch { }
            try
            {
                Console.WriteLine(msg);
            }
            catch { }
        }
    }

    private static bool IsEnabled()
    {
        try
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_TRACE"),
                "1",
                StringComparison.Ordinal
            );
        }
        catch
        {
            return false;
        }
    }

    public static IDisposable Scope(string name)
    {
        return IsEnabled() ? new RealScope(name) : DummyScope.Instance;
    }
}
