using System;
using System.Diagnostics;

namespace PolicyPlusPlus.Logging
{
    /// <summary>
    /// Lightweight scope helper to log start/end (with duration ms) and capture exceptions.
    /// Usage: using var _ = LogScope.Debug("Area", "operation details");
    /// End log only emitted if level enabled. Never throws.
    /// </summary>
    public sealed class LogScope : IDisposable
    {
        private readonly string _area;
        private readonly string _operation;
        private readonly DebugLevel _level;
        private readonly Stopwatch _sw;
        private bool _completed;
        private Exception? _exception;

        private LogScope(string area, string operation, DebugLevel level)
        {
            _area = area;
            _operation = operation;
            _level = level;
            if (Log.IsEnabled(level))
            {
                // Start timing only when level is enabled.
                _sw = Stopwatch.StartNew();
                try
                {
                    Log.EmitInternal(level, area, $"BEGIN {_operation}");
                }
                catch { }
            }
            else
            {
                _sw = new Stopwatch();
            }
        }

        public static LogScope Trace(string area, string operation) =>
            new(area, operation, DebugLevel.Trace);

        public static LogScope Debug(string area, string operation) =>
            new(area, operation, DebugLevel.Debug);

        public static LogScope Info(string area, string operation) =>
            new(area, operation, DebugLevel.Info);

        // Mark scope as logically successful (optional; success assumed if no exception recorded).
        public void Complete()
        {
            _completed = true;
        }

        // Record first exception so end log can include failure note.
        public void Capture(Exception ex)
        {
            if (_exception == null)
                _exception = ex;
        }

        public void Dispose()
        {
            try
            {
                if (!Log.IsEnabled(_level))
                    return;
                _sw.Stop();
                string status = _exception != null ? "FAIL" : (_completed ? "OK" : "END");
                var msg = $"{status} {_operation} ({_sw.ElapsedMilliseconds} ms)";
                if (_exception != null)
                    Log.EmitInternal(
                        _level >= DebugLevel.Info ? DebugLevel.Warn : _level,
                        _area,
                        msg,
                        _exception
                    );
                else
                    Log.EmitInternal(_level, _area, msg);
            }
            catch { }
        }
    }

    public static partial class Log
    {
        // Internal helper for LogScope to avoid exposing Emit.
        internal static void EmitInternal(
            DebugLevel level,
            string area,
            string msg,
            Exception? ex = null
        )
        {
            try
            {
                Sink.Log(level, area, msg, ex);
            }
            catch { }
        }
    }
}
