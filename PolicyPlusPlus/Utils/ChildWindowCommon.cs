using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
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
            SystemBackdrop? deferredBackdrop = null;
            bool restoreBackdrop = false;
            try
            {
                if (window.SystemBackdrop is MicaBackdrop mica)
                {
                    deferredBackdrop = mica;
                    restoreBackdrop = true;
                    window.SystemBackdrop = null;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("ChildWindow", "Mica deferral failed: " + ex.Message);
            }
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
                if (window.Content is FrameworkElement root)
                {
                    var effectiveTheme = App.GetEffectiveTheme(window);
                    if (effectiveTheme == ElementTheme.Dark)
                    {
                        var initialOpacity = root.Opacity;
                        if (initialOpacity > 0)
                        {
                            root.Opacity = 0;
                            void RestoreOpacity(object? sender, RoutedEventArgs e)
                            {
                                root.Loaded -= RestoreOpacity;
                                try
                                {
                                    root.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        try
                                        {
                                            if (restoreBackdrop && deferredBackdrop != null)
                                                window.SystemBackdrop = deferredBackdrop;
                                        }
                                        catch (Exception backdropEx)
                                        {
                                            Log.Debug(
                                                "ChildWindow",
                                                "Mica restore failed: " + backdropEx.Message
                                            );
                                        }
                                        root.Opacity = initialOpacity;
                                    });
                                }
                                catch
                                {
                                    root.Opacity = initialOpacity;
                                }
                            }
                            root.Loaded += RestoreOpacity;
                        }
                        else if (restoreBackdrop && deferredBackdrop != null)
                        {
                            // Light theme still needs backdrop reapply but no fade
                            void RestoreBackdrop(object? sender, RoutedEventArgs e)
                            {
                                root.Loaded -= RestoreBackdrop;
                                try
                                {
                                    root.DispatcherQueue.TryEnqueue(() =>
                                    {
                                        try
                                        {
                                            window.SystemBackdrop = deferredBackdrop;
                                        }
                                        catch (Exception backdropEx)
                                        {
                                            Log.Debug(
                                                "ChildWindow",
                                                "Mica restore (light) failed: " + backdropEx.Message
                                            );
                                        }
                                    });
                                }
                                catch (Exception dsEx)
                                {
                                    Log.Debug(
                                        "ChildWindow",
                                        "Dispatcher restore failed: " + dsEx.Message
                                    );
                                }
                            }
                            root.Loaded += RestoreBackdrop;
                        }
                    }
                    else if (restoreBackdrop && deferredBackdrop != null)
                    {
                        // Non-dark theme but still temporarily removed backdrop
                        try
                        {
                            root.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    window.SystemBackdrop = deferredBackdrop;
                                }
                                catch (Exception backdropEx)
                                {
                                    Log.Debug(
                                        "ChildWindow",
                                        "Mica restore default failed: " + backdropEx.Message
                                    );
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(
                                "ChildWindow",
                                "Immediate backdrop restore failed: " + ex.Message
                            );
                        }
                    }
                }
                else if (restoreBackdrop && deferredBackdrop != null)
                {
                    try
                    {
                        window.SystemBackdrop = deferredBackdrop;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("ChildWindow", "Mica restore (no content) failed: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("ChildWindow", "initial opacity guard failed", ex);
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
