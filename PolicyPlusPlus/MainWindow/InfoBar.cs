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
        private void ShowInfo(
            string message,
            InfoBarSeverity severity = InfoBarSeverity.Informational
        )
        {
            if (StatusBar == null)
                return;

            _infoBarCloseCts?.Cancel();
            StatusBar.Opacity = 1;

            StatusBar.Title = null;
            StatusBar.Severity = severity;
            StatusBar.Message = message;
            StatusBar.IsOpen = true;

            StartInfoBarAutoClose();
        }

        private void StartInfoBarAutoClose()
        {
            _infoBarCloseCts?.Cancel();
            _infoBarCloseCts = new CancellationTokenSource();
            var token = _infoBarCloseCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
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
