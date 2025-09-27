using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        // Centralized ephemeral status notification.
        // Auto close timing policy:
        //  Informational: 2500 ms
        //  Success:       3000 ms
        //  Warning:       6000 ms (user may need a little longer to read)
        //  Error:         manual (no auto close) – stays until replaced or user closes
        // Call sites keep using existing two‑parameter form; optional args allow future refinement.
        internal void ShowInfo(
            string message,
            InfoBarSeverity severity = InfoBarSeverity.Informational,
            int? overrideDurationMs = null,
            string? title = null
        )
        {
            if (StatusBar == null)
                return;

            _infoBarCloseCts?.Cancel();
            StatusBar.Opacity = 1;

            StatusBar.Title = title; // null -> default (hidden)
            StatusBar.Severity = severity;
            StatusBar.Message = message;

            // Error / Warning remain closable; transient statuses auto‑close.
            StatusBar.IsClosable =
                severity == InfoBarSeverity.Error || severity == InfoBarSeverity.Warning;
            StatusBar.IsOpen = true;

            // Decide duration (ms). 0 or negative => no auto close.
            int duration =
                overrideDurationMs
                ?? severity switch
                {
                    InfoBarSeverity.Informational => 2500,
                    InfoBarSeverity.Success => 3000,
                    InfoBarSeverity.Warning => 6000,
                    InfoBarSeverity.Error => 0,
                    _ => 3000,
                };

            if (duration > 0)
                StartInfoBarAutoClose(duration);
        }

        private void StartInfoBarAutoClose(int durationMs)
        {
            if (durationMs <= 0)
                return;
            _infoBarCloseCts?.Cancel();
            _infoBarCloseCts = new CancellationTokenSource();
            var token = _infoBarCloseCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(durationMs, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                if (token.IsCancellationRequested)
                    return;

                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        await FadeOutAndCloseInfoBarAsync();
                    }
                });
            });
        }

        private async Task FadeOutAndCloseInfoBarAsync()
        {
            if (StatusBar == null || !StatusBar.IsOpen)
                return;

            var anim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new ExponentialEase
                {
                    Exponent = 4,
                    EasingMode = EasingMode.EaseOut,
                },
            };
            var sb = new Storyboard();
            Storyboard.SetTarget(anim, StatusBar);
            Storyboard.SetTargetProperty(anim, "Opacity");
            sb.Children.Add(anim);

            var tcs = new TaskCompletionSource<object?>();
            sb.Completed += (s, e) => tcs.TrySetResult(null);
            sb.Begin();
            await tcs.Task;

            StatusBar.IsOpen = false;
            StatusBar.Opacity = 1;
        }

        private void HideInfo()
        {
            if (StatusBar == null)
                return;
            _infoBarCloseCts?.Cancel();
            StatusBar.IsOpen = false;
            StatusBar.Opacity = 1;
        }
    }
}
