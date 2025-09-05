using Microsoft.UI.Xaml;
using PolicyPlus.WinUI3.Utils;
using System;

namespace PolicyPlus.WinUI3.Utils
{
    /// <summary>
    /// Provides a single place for common child window startup wiring: theme reapply, size scaling,
    /// registration/unregistration, front activation, and DPI scale attachment.
    /// </summary>
    public static class ChildWindowCommon
    {
        public static void Initialize(Window window, int width, int height, Action? applyTheme)
        {
            if (window == null) return;
            try { applyTheme?.Invoke(); } catch { }
            try { App.ThemeChanged += (s, e) => { try { applyTheme?.Invoke(); } catch { } }; } catch { }
            try { WindowHelpers.ResizeForDisplayScale(window, width, height); } catch { }
            try { window.Activated += (s, e) => { try { WindowHelpers.BringToFront(window); } catch { } }; } catch { }
            try { window.Closed += (s, e) => { try { App.UnregisterWindow(window); } catch { } }; } catch { }
            try { App.RegisterWindow(window); } catch { }

            void TryAttachScale()
            {
                try
                {
                    if (window.Content is FrameworkElement fe)
                    {
                        var scaleHost = fe.FindName("ScaleHost") as FrameworkElement;
                        var rootShell = fe.FindName("RootShell") as FrameworkElement;
                        // Fallbacks for windows that only have a _root grid
                        if (rootShell == null)
                        {
                            rootShell = fe.FindName("_root") as FrameworkElement;
                        }
                        if (scaleHost == null)
                        {
                            scaleHost = rootShell; // use same element if dedicated host missing
                        }
                        if (scaleHost != null && rootShell != null)
                        {
                            try { ScaleHelper.Attach(window, scaleHost, rootShell); } catch { }
                        }
                    }
                }
                catch { }
            }
            // Immediate attempt then again on Loaded (content tree ready)
            TryAttachScale();
            try
            {
                if (window.Content is FrameworkElement fe2)
                {
                    fe2.Loaded += (s, e) => TryAttachScale();
                }
            }
            catch { }
        }
    }
}
