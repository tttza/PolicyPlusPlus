using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PolicyPlus.WinUI3.Logging;
using PolicyPlus.WinUI3.Utils;
using System;
using System.Collections.Concurrent;
using System.Text;
using Windows.ApplicationModel.DataTransfer;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class LogViewerWindow : Window, ILogSink
    {
        private readonly ConcurrentQueue<string> _pending = new();
        private bool _flushScheduled;
        private bool _paused;
        private DebugLevel _uiLevel = DebugLevel.Info;
        private ILogSink _previous = Log.Sink; // capture at creation

        public LogViewerWindow()
        {
            InitializeComponent();
            Title = "Logs";
            ChildWindowCommon.Initialize(this, 760, 540, ApplyTheme);
            // Chain sinks so original still receives messages
            Log.Sink = new ChainedSink(this, _previous);
            Closed += (_, __) => { try { Log.Sink = _previous; } catch { } };
            try { RootShell.Loaded += (_, __) => { try { UpdateStatus(); if (LevelCombo != null && LevelCombo.SelectedIndex < 0) LevelCombo.SelectedIndex = 2; } catch { } }; } catch { }
        }

        private void ApplyTheme() { try { if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme; } catch { } }

        private sealed class ChainedSink : ILogSink
        { private readonly ILogSink _a; private readonly ILogSink _b; public ChainedSink(ILogSink a, ILogSink b) { _a = a; _b = b; } public void Log(DebugLevel l, string ar, string msg, Exception? ex = null) { try { _a.Log(l, ar, msg, ex); } catch { } try { _b.Log(l, ar, msg, ex); } catch { } } }

        // ILogSink implementation
        void ILogSink.Log(DebugLevel level, string area, string message, Exception? ex) => Receive(level, area, message, ex);

        private void Receive(DebugLevel level, string area, string message, Exception? ex = null)
        {
            if (level < _uiLevel || _paused) return;
            _pending.Enqueue(FormatLine(level, area, message, ex));
            ScheduleFlush();
        }

        private static string FormatLine(DebugLevel l, string a, string m, Exception? ex)
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"))
              .Append(' ').Append(l.ToString().PadRight(5))
              .Append(' ').Append(a).Append(" | ")
              .Append(m);
            if (ex != null) sb.Append(" :: ").Append(ex.GetType().Name).Append(':').Append(ex.Message);
            return sb.ToString();
        }

        private void ScheduleFlush()
        {
            if (_flushScheduled) return; _flushScheduled = true;
            DispatcherQueue.TryEnqueue(() =>
            {
                _flushScheduled = false;
                if (_paused || _pending.IsEmpty) return;
                var sb = new StringBuilder();
                while (_pending.TryDequeue(out var line)) sb.AppendLine(line);
                try { if (LogBox != null) { LogBox.Text += sb.ToString(); LogBox.SelectionStart = LogBox.Text.Length; LogBox.SelectionLength = 0; } } catch { } });
        }

        private void LevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { var txt = (LevelCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Info"; _uiLevel = txt switch { "Trace" => DebugLevel.Trace, "Debug" => DebugLevel.Debug, "Info" => DebugLevel.Info, "Warn" => DebugLevel.Warn, "Error" => DebugLevel.Error, _ => DebugLevel.Info }; UpdateStatus(); }
        private void ClearBtn_Click(object sender, RoutedEventArgs e) { try { if (LogBox != null) LogBox.Text = string.Empty; } catch { } UpdateStatus(); }
        private void CopyBtn_Click(object sender, RoutedEventArgs e) { try { var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy }; dp.SetText(LogBox?.Text ?? string.Empty); Clipboard.SetContent(dp); } catch { } }
        private void PauseBtn_Click(object sender, RoutedEventArgs e) { _paused = !_paused; try { if (PauseBtn != null) PauseBtn.Content = _paused ? "Resume" : "Pause"; if (LiveIndicator != null) LiveIndicator.Visibility = _paused ? Visibility.Collapsed : Visibility.Visible; } catch { } UpdateStatus(); }
        private void UpdateStatus() { try { if (StatusText != null) StatusText.Text = _paused ? "Paused" : $"Level >= {_uiLevel}"; } catch { } }
    }
}
