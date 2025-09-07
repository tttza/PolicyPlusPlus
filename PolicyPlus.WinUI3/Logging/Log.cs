using System;
using System.Diagnostics;

namespace PolicyPlus.WinUI3.Logging
{
    // Logging severity levels (kept minimal for now)
    public enum DebugLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level,-5} {area}: {message}{(ex != null ? " :: " + ex.GetType().Name + " - " + ex.Message : string.Empty)}");
                if (ex != null && ex.StackTrace != null)
                    Debug.WriteLine(ex.StackTrace);
            }
            catch
            {
                // Never throw from logging.
            }
        }
    }

    public static class Log
    {
        // Replaceable at runtime (e.g., unit tests or future file logger). Non-null.
        public static ILogSink Sink { get; set; } = new DebugLogSink();

        public static void Trace(string area, string msg) => Sink.Log(DebugLevel.Trace, area, msg);
        public static void Debug(string area, string msg) => Sink.Log(DebugLevel.Debug, area, msg);
        public static void Info(string area, string msg)  => Sink.Log(DebugLevel.Info,  area, msg);
        public static void Warn(string area, string msg, Exception? ex = null) => Sink.Log(DebugLevel.Warn, area, msg, ex);
        public static void Error(string area, string msg, Exception? ex = null) => Sink.Log(DebugLevel.Error, area, msg, ex);
    }
}
