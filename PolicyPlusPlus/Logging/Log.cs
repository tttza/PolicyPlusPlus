using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PolicyPlusPlus.Logging
{
    // Logging severity levels (ordering important: lower = more verbose)
    public enum DebugLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
    }

    public interface ILogSink
    {
        void Log(DebugLevel level, string area, string message, Exception? ex = null);
    }

    // Default implementation: Debug.WriteLine (non-breaking, no external deps)
    public sealed class DebugLogSink : ILogSink
    {
        public void Log(DebugLevel level, string area, string message, Exception? ex = null)
        {
            try
            {
                Debug.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] {level, -5} {area}: {message}{(ex != null ? " :: " + ex.GetType().Name + " - " + ex.Message : string.Empty)}"
                );
                if (ex != null && ex.StackTrace != null)
                    Debug.WriteLine(ex.StackTrace);
            }
            catch
            {
                // Never throw from logging.
            }
        }
    }

    public static partial class Log
    {
        // Replaceable at runtime (e.g., unit tests or future file logger). Non-null.
        public static ILogSink Sink { get; set; } = new DebugLogSink();

        // Minimum level to emit. Debug build: Debug (richer diagnostics). Release: Info.
#if DEBUG
        public static DebugLevel MinLevel { get; set; } = DebugLevel.Debug;
#else
        public static DebugLevel MinLevel { get; set; } = DebugLevel.Info;
#endif

        public static bool IsEnabled(DebugLevel level) => level >= MinLevel;

        private static void Emit(DebugLevel level, string area, string msg, Exception? ex = null)
        {
            if (!IsEnabled(level))
                return;
            try
            {
                Sink.Log(level, area, msg, ex);
            }
            catch
            { /* swallow */
            }
        }

        public static void Trace(string area, string msg) => Emit(DebugLevel.Trace, area, msg);

        public static void Debug(string area, string msg) => Emit(DebugLevel.Debug, area, msg);

        public static void Info(string area, string msg) => Emit(DebugLevel.Info, area, msg);

        public static void Warn(string area, string msg, Exception? ex = null) =>
            Emit(DebugLevel.Warn, area, msg, ex);

        public static void Error(string area, string msg, Exception? ex = null) =>
            Emit(DebugLevel.Error, area, msg, ex);

        // Utility: create short summary of options dictionary for logging (avoids huge blobs)
        public static string SummarizeOptions(Dictionary<string, object>? opts, int maxLen = 160)
        {
            if (opts == null || opts.Count == 0)
                return "(none)";
            var sb = new StringBuilder();
            int i = 0;
            foreach (var kv in opts)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(kv.Key).Append('=');
                sb.Append(FormatValue(kv.Value));
                if (sb.Length > maxLen)
                {
                    sb.Append(" �c");
                    break;
                }
                i++;
            }
            return sb.ToString();
        }

        private static string FormatValue(object? value)
        {
            if (value == null)
                return "null";
            if (value is string s)
                return Truncate(s.Replace('\n', ' ').Replace('\r', ' '));
            if (value is bool b)
                return b ? "true" : "false";
            if (value is Array arr)
            {
                var first = arr.Length == 0 ? 0 : Math.Min(arr.Length, 3);
                var parts = new string[first];
                for (int i = 0; i < first; i++)
                    parts[i] = Convert.ToString(arr.GetValue(i)) ?? string.Empty;
                return $"[{string.Join('|', parts)}{(arr.Length > first ? "…" : string.Empty)}]";
            }
            if (value is System.Collections.IEnumerable en && value is not string)
            {
                int c = 0;
                var sb = new StringBuilder();
                sb.Append('[');
                foreach (var e in en)
                {
                    if (c > 0)
                        sb.Append('|');
                    sb.Append(Truncate(Convert.ToString(e) ?? string.Empty, 24));
                    c++;
                    if (c == 3)
                        break;
                }
                if (c >= 3)
                    sb.Append('…');
                sb.Append(']');
                return sb.ToString();
            }
            return Truncate(Convert.ToString(value) ?? string.Empty);
        }

        private static string Truncate(string s, int max = 48)
        {
            if (s.Length <= max)
                return s;
            return s.Substring(0, max) + "…";
        }

        // Correlation helpers
        public static string NewCorrelationId() => Guid.NewGuid().ToString("N");

        // Parses command line style tokens to set log level. Accepts:
        //   --log-trace | --log-debug | --log-info | --log-warn | --log-error
        //   --log-level=<trace|debug|info|warn|error>
        // First matching token wins. Ignores errors.
        public static void ConfigureFromArgs(IEnumerable<string> args)
        {
            if (args == null)
                return;
            try
            {
                foreach (var raw in args)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;
                    var a = raw.Trim();
                    if (!a.StartsWith("--", StringComparison.Ordinal))
                        continue;
                    if (TryMapFlag(a))
                        break; // first explicit level wins
                }
            }
            catch { }
        }

        private static bool TryMapFlag(string flag)
        {
            static bool Set(DebugLevel level)
            {
                MinLevel = level;
                return true;
            }
            if (flag.Equals("--log-trace", StringComparison.OrdinalIgnoreCase))
                return Set(DebugLevel.Trace);
            if (flag.Equals("--log-debug", StringComparison.OrdinalIgnoreCase))
                return Set(DebugLevel.Debug);
            if (flag.Equals("--log-info", StringComparison.OrdinalIgnoreCase))
                return Set(DebugLevel.Info);
            if (flag.Equals("--log-warn", StringComparison.OrdinalIgnoreCase))
                return Set(DebugLevel.Warn);
            if (flag.Equals("--log-error", StringComparison.OrdinalIgnoreCase))
                return Set(DebugLevel.Error);
            if (flag.StartsWith("--log-level=", StringComparison.OrdinalIgnoreCase))
            {
                var val = flag.Substring("--log-level=".Length).Trim();
                return val.ToLowerInvariant() switch
                {
                    "trace" => Set(DebugLevel.Trace),
                    "debug" => Set(DebugLevel.Debug),
                    "info" => Set(DebugLevel.Info),
                    "warn" or "warning" => Set(DebugLevel.Warn),
                    "error" => Set(DebugLevel.Error),
                    _ => false,
                };
            }
            return false;
        }
    }
}
