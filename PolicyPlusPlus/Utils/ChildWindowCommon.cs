using System;
using Microsoft.UI.Xaml;
using PolicyPlusPlus.Logging; // logging

namespace PolicyPlusPlus.Utils
{
    /// <summary>
    /// Provides a single place for common child window startup wiring: theme reapply, size scaling,
    /// registration/unregistration, front activation, and DPI scale attachment.
    /// </summary>
    public static class ChildWindowCommon
    {
        public static void Initialize(Window window, int width, int height, Action? applyTheme)
        {
            if (window == null)
                return;
            try
            {
                applyTheme?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "applyTheme invoke failed", ex);
            }
            try
            {
                App.ThemeChanged += (s, e) =>
                {
                    try
                    {
                        applyTheme?.Invoke();
                    }
                    catch (Exception ex2)
                    {
                        Log.Warn("ChildWindow", "applyTheme invoke failed (ThemeChanged)", ex2);
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "ThemeChanged subscription failed", ex);
            }
            try
            {
                WindowHelpers.ResizeForDisplayScale(window, width, height);
            }
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "ResizeForDisplayScale failed", ex);
            }
            try
            {
                window.Activated += (s, e) =>
                {
                    try
                    {
                        WindowHelpers.BringToFront(window);
                    }
                    catch (Exception ex2)
                    {
                        Log.Warn("ChildWindow", "BringToFront failed (Activated)", ex2);
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "Activated subscription failed", ex);
            }
            try
            {
                window.Closed += (s, e) =>
                {
                    try
                    {
                        App.UnregisterWindow(window);
                    }
                    catch (Exception ex2)
                    {
                        Log.Warn("ChildWindow", "UnregisterWindow failed (Closed)", ex2);
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "Closed subscription failed", ex);
            }
            try
            {
                App.RegisterWindow(window);
            }
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "RegisterWindow failed", ex);
            }

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
                            try
                            {
                                ScaleHelper.Attach(window, scaleHost, rootShell);
                            }
                            catch (Exception ex)
                            {
                                Log.Warn("ChildWindow", "ScaleHelper.Attach failed", ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("ChildWindow", "TryAttachScale root failure", ex);
                }
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
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "Loaded subscription failed", ex);
            }
        }
    }
}
