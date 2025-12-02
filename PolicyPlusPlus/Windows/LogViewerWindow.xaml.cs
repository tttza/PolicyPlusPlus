using System;
#nullable enable
using System.Collections.Concurrent;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PolicyPlusPlus.Logging;
using PolicyPlusPlus.Utils;
using Windows.ApplicationModel.DataTransfer;

namespace PolicyPlusPlus.Windows
{
    public sealed partial class LogViewerWindow : Window, ILogSink
    {
        private readonly ConcurrentQueue<string> _pending = new();
        private bool _flushScheduled;
        private bool _paused;
        private bool _initializedSelection; // suppress early SelectionChanged until we set it explicitly
        private DebugLevel _uiLevel = DebugLevel.Info;
        private ILogSink _previous = Log.Sink; // capture at creation
        private long _lastSeq; // last sequence id from buffer we have displayed
        private string[]? _preloadedLines; // snapshot lines captured at construction

        // Throttling parameters: avoid UI saturation during bursts
        private const int MaxLinesPerFlush = 400; // cap per UI update
        private static readonly TimeSpan MinFlushInterval = TimeSpan.FromMilliseconds(120); // min spacing
        private DateTime _lastFlushTime = DateTime.MinValue;

        public LogViewerWindow()
        {
            InitializeComponent();
            Title = "Logs";
            ChildWindowCommon.Initialize(this, 760, 540, ApplyTheme);

            // Initialize viewer filter level from current global emission MinLevel so dropdown matches runtime state.
            _uiLevel = Log.MinLevel;

            // Capture snapshot BEFORE chaining sink so we don't duplicate early messages
            try
            {
                var snap = Log.BufferSnapshotAll();
                _lastSeq = snap.lastSeq;
                _preloadedLines = snap.lines;
            }
            catch (Exception ex)
            {
                Log.Warn("LogView", "snapshot capture failed", ex);
            }

            // Chain sinks so original still receives messages (only new lines after snapshot)
            Log.Sink = new ChainedSink(this, _previous);
            Closed += (_, __) =>
            {
                try
                {
                    Log.Sink = _previous;
                }
                catch (Exception ex)
                {
                    Log.Debug("LogView", $"restore sink failed: {ex.Message}");
                }
            };
            try
            {
                RootShell.Loaded += (_, __) =>
                {
                    try
                    {
                        if (_preloadedLines != null && _preloadedLines.Length > 0 && LogBox != null)
                        {
                            // Apply current filter to snapshot so initial display is consistent with level selection.
                            var sbInit = new StringBuilder();
                            foreach (var line in _preloadedLines)
                            {
                                if (IsLineVisible(line))
                                    sbInit.AppendLine(line);
                            }
                            LogBox.Text = sbInit.ToString();
                            LogBox.SelectionStart = LogBox.Text.Length;
                            LogBox.SelectionLength = 0;
                            _preloadedLines = null;
                        }
                        UpdateStatus();
                        if (LevelCombo != null)
                        {
                            LevelCombo.SelectedIndex = _uiLevel switch
                            {
                                DebugLevel.Trace => 0,
                                DebugLevel.Debug => 1,
                                DebugLevel.Info => 2,
                                DebugLevel.Warn => 3,
                                DebugLevel.Error => 4,
                                _ => 2,
                            };
                            _initializedSelection = true; // now future user changes should be processed
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("LogView", "initial load apply failed", ex);
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Warn("LogView", "attach RootShell.Loaded failed", ex);
            }
        }

        private void ApplyTheme()
        {
            try
            {
                var theme = App.GetEffectiveTheme(this);
                if (Content is FrameworkElement fe)
                    fe.RequestedTheme = theme;
                WindowHelpers.ApplyImmersiveDarkMode(this, theme == ElementTheme.Dark);
            }
            catch (Exception ex)
            {
                Log.Debug("LogView", $"ApplyTheme failed: {ex.Message}");
            }
        }

        private sealed class ChainedSink : ILogSink
        {
            private readonly ILogSink _sinkA;
            private readonly ILogSink _sinkB;

            public ChainedSink(ILogSink a, ILogSink b)
            {
                _sinkA = a;
                _sinkB = b;
            }

            void ILogSink.Log(DebugLevel level, string area, string message, Exception? ex)
            {
                try
                {
                    _sinkA.Log(level, area, message, ex);
                }
                catch (Exception ex2)
                {
                    Log.Debug("LogView", $"sinkA failed: {ex2.Message}");
                }
                try
                {
                    _sinkB.Log(level, area, message, ex);
                }
                catch (Exception ex3)
                {
                    Log.Debug("LogView", $"sinkB failed: {ex3.Message}");
                }
            }
        }

        // ILogSink implementation for LogViewerWindow itself
        void ILogSink.Log(DebugLevel level, string area, string message, Exception? ex) =>
            Receive(level, area, message, ex);

        private void Receive(DebugLevel level, string area, string message, Exception? ex = null)
        {
            // Always advance buffer position even if current UI level would hide the triggering entry.
            // Previous logic skipped advancing _lastSeq for suppressed levels; later higher-level events
            // would then retroactively enqueue hidden lines (unexpected for user) or miss newly allowed ones
            // after raising verbosity. We now read the delta unconditionally and filter per line.
            var delta = Log.BufferReadSince(_lastSeq);
            _lastSeq = delta.lastSeq;
            if (_paused || delta.lines.Length == 0)
                return;
            foreach (var line in delta.lines)
            {
                if (IsLineVisible(line))
                    _pending.Enqueue(line);
            }
            if (!_pending.IsEmpty)
                ScheduleFlush();
        }

        private bool IsLineVisible(string formatted)
        {
            try
            {
                // Format: HH:mm:ss.fff <Level>[padding] <Area> | message
                // We parse the second token as level.
                int firstSpace = formatted.IndexOf(' ');
                if (firstSpace < 0)
                    return true; // fail open (show)
                int secondSpace = formatted.IndexOf(' ', firstSpace + 1);
                if (secondSpace < 0)
                    return true;
                var levelToken = formatted
                    .Substring(firstSpace + 1, secondSpace - firstSpace - 1)
                    .Trim();
                if (Enum.TryParse<DebugLevel>(levelToken, true, out var lvl))
                    return lvl >= _uiLevel;
                return true; // unknown token -> show
            }
            catch
            {
                return true; // never break display on parse errors
            }
        }

        private void ScheduleFlush()
        {
            if (_flushScheduled)
                return;
            _flushScheduled = true;
            DispatcherQueue.TryEnqueue(() =>
            {
                _flushScheduled = false;
                if (_paused || _pending.IsEmpty)
                    return;
                // Enforce minimum interval
                var now = DateTime.UtcNow;
                if (now - _lastFlushTime < MinFlushInterval)
                {
                    // Requeue a later flush (single)
                    if (!_flushScheduled)
                    {
                        _flushScheduled = true;
                        var delay = MinFlushInterval - (now - _lastFlushTime);
                        _ = DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                if (delay > TimeSpan.Zero)
                                    await System.Threading.Tasks.Task.Delay(delay);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug("LogView", $"delayed flush wait failed: {ex.Message}");
                            }
                            _flushScheduled = false;
                            ScheduleFlush();
                        });
                    }
                    return;
                }
                var sb = new StringBuilder();
                int taken = 0;
                while (taken < MaxLinesPerFlush && _pending.TryDequeue(out var line))
                {
                    sb.AppendLine(line);
                    taken++;
                }
                try
                {
                    if (LogBox != null)
                    {
                        LogBox.Text += sb.ToString();
                        LogBox.SelectionStart = LogBox.Text.Length;
                        LogBox.SelectionLength = 0;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("LogView", "flush append failed", ex);
                }
                _lastFlushTime = DateTime.UtcNow;
                // If backlog remains, schedule another (respect interval)
                if (!_pending.IsEmpty)
                {
                    ScheduleFlush();
                }
            });
        }

        private void LevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initializedSelection)
                return; // ignore early automatic event fired by XAML materialization
            var txt = (LevelCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Info";
            _uiLevel = txt switch
            {
                "Trace" => DebugLevel.Trace,
                "Debug" => DebugLevel.Debug,
                "Info" => DebugLevel.Info,
                "Warn" => DebugLevel.Warn,
                "Error" => DebugLevel.Error,
                _ => DebugLevel.Info,
            };
            // Always apply selected level to global emission threshold.
            try
            {
                if (Log.MinLevel != _uiLevel)
                    Log.MinLevel = _uiLevel;
            }
            catch (Exception ex)
            {
                Log.Warn("LogView", "set global level failed", ex);
            }
            UpdateStatus();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LogBox != null)
                    LogBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                Log.Debug("LogView", $"ClearBtn failed: {ex.Message}");
            }
            UpdateStatus();
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(LogBox?.Text ?? string.Empty);
                Clipboard.SetContent(dp);
            }
            catch (Exception ex)
            {
                Log.Warn("LogView", "Copy failed", ex);
            }
        }

        private void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            _paused = !_paused;
            try
            {
                if (PauseBtn != null)
                    PauseBtn.Content = _paused ? "Resume" : "Pause";
                if (LiveIndicator != null)
                    LiveIndicator.Visibility = _paused ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Debug("LogView", $"PauseBtn toggle failed: {ex.Message}");
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            try
            {
                if (StatusText != null)
                    StatusText.Text = _paused ? "Paused" : $"Level >= {_uiLevel} (global)";
            }
            catch (Exception ex)
            {
                Log.Debug("LogView", $"UpdateStatus failed: {ex.Message}");
            }
        }
    }
}
