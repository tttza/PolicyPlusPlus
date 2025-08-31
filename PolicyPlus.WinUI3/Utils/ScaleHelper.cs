using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

namespace PolicyPlus.WinUI3.Utils
{
    internal static class ScaleHelper
    {
        public static void Attach(Window window, FrameworkElement host, FrameworkElement innerRoot)
        {
            if (host == null || innerRoot == null) return;

            innerRoot.HorizontalAlignment = HorizontalAlignment.Left;
            innerRoot.VerticalAlignment = VerticalAlignment.Top;
            innerRoot.Margin = new Thickness(0);

            var transform = innerRoot.RenderTransform as ScaleTransform;
            if (transform == null)
            {
                transform = new ScaleTransform();
                innerRoot.RenderTransform = transform;
                innerRoot.RenderTransformOrigin = new Point(0, 0);
            }

            var clip = host.Clip as RectangleGeometry;
            if (clip == null)
            {
                clip = new RectangleGeometry();
                host.Clip = clip;
            }

            void Apply()
            {
                try
                {
                    double scale = Math.Max(0.1, App.CurrentScale);
                    transform.ScaleX = scale;
                    transform.ScaleY = scale;

                    double w = host.ActualWidth;
                    double h = host.ActualHeight;
                    if (w > 0 && h > 0)
                    {
                        innerRoot.Width = Math.Ceiling(w / scale);
                        innerRoot.Height = Math.Ceiling(h / scale);
                        clip.Rect = new Rect(0, 0, w, h);
                    }

                    innerRoot.UpdateLayout();
                }
                catch { }
            }

            SizeChangedEventHandler sizeHandler = (s, e) => Apply();
            host.SizeChanged += sizeHandler;
            EventHandler scaleHandler = (s, e) => Apply();
            App.ScaleChanged += scaleHandler;
            window.Activated += (s, e) => Apply();

            if (host.IsLoaded)
                Apply();
            else
                host.Loaded += (s, e) => Apply();

            window.Closed += (s, e) =>
            {
                try { host.SizeChanged -= sizeHandler; } catch { }
                try { App.ScaleChanged -= scaleHandler; } catch { }
            };
        }
    }
}
