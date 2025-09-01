using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using PolicyPlus.WinUI3.Windows;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Utils;
using Microsoft.UI.Windowing;

namespace PolicyPlus.WinUI3
{
    public partial class App : Application
    {
        public static Window? Window { get; private set; }
        private static readonly HashSet<Window> _secondaryWindows = new();
        private static readonly Dictionary<string, EditSettingWindow> _openEditWindows = new();

        public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;
        public static event EventHandler? ThemeChanged;

        public static double CurrentScale { get; private set; } = 1.0;
        public static event EventHandler? ScaleChanged;

        private static string? _iconPathCache;

        public App()
        {
            InitializeComponent();
            this.UnhandledException += (s, e) => { try { e.Handled = true; } catch { } };
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { ElevationService.Instance.ShutdownAsync().GetAwaiter().GetResult(); } catch { } };
            }
            catch { }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                var cmd = Environment.GetCommandLineArgs();
                if (cmd != null && cmd.Length >= 3 && string.Equals(cmd[1], "--elevation-host", StringComparison.OrdinalIgnoreCase))
                {
                    string pipe = cmd[2];
                    _ = ElevationHost.Run(pipe);
                    try { Environment.Exit(0); } catch { }
                    return;
                }
            }
            catch { }

            Window = new MainWindow();
            ApplyThemeTo(Window);

            TryApplyIconTo(Window);

            Window.Closed += async (s, e) => { try { await ElevationService.Instance.ShutdownAsync(); } catch { } CloseAllSecondaryWindows(); };
            Window.Activate();
        }

        private static void TryApplyIconTo(Window w)
        {
            // Prefer cached path
            try
            {
                if (!string.IsNullOrEmpty(_iconPathCache))
                {
                    w.AppWindow?.SetIcon(_iconPathCache);
                    return;
                }
            }
            catch { }

            // Try embedded resource first (embedded via <EmbeddedResource Include="Assets\\*.ico" />)
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var names = asm.GetManifestResourceNames();
                var icoName = names.FirstOrDefault(n => n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(icoName))
                {
                    using var s = asm.GetManifestResourceStream(icoName);
                    if (s != null)
                    {
                        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PolicyPlusAppIcon.ico");
                        using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            s.CopyTo(fs);
                        }
                        _iconPathCache = tempPath;
                        w.AppWindow?.SetIcon(tempPath);
                        return;
                    }
                }
            }
            catch { }

            // Fallback: look for a .ico file next to the app (useful during development)
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string assets = System.IO.Path.Combine(baseDir, "Assets");
                if (Directory.Exists(assets))
                {
                    var path = Directory.EnumerateFiles(assets, "*.ico").FirstOrDefault();
                    if (!string.IsNullOrEmpty(path))
                    {
                        _iconPathCache = path;
                        w.AppWindow?.SetIcon(path);
                    }
                }
            }
            catch { }
        }

        public static void SetGlobalTheme(ElementTheme theme)
        {
            CurrentTheme = theme;
            if (Window != null) ApplyThemeTo(Window);
            foreach (var w in _secondaryWindows) ApplyThemeTo(w);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void SetGlobalScale(double scale)
        {
            if (scale <= 0) scale = 1.0;
            CurrentScale = scale;
            ScaleChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void ApplyThemeTo(Window w)
        {
            if (w.Content is FrameworkElement fe)
                fe.RequestedTheme = CurrentTheme;
        }

        public static void RegisterWindow(Window w)
        {
            if (w == Window) return;
            _secondaryWindows.Add(w);
            ApplyThemeTo(w);
            TryApplyIconTo(w);
        }
        public static void UnregisterWindow(Window w)
        {
            _secondaryWindows.Remove(w);
        }
        public static void CloseAllSecondaryWindows()
        {
            foreach (var w in _secondaryWindows.ToArray())
            {
                try { w.Close(); } catch { }
            }
            _secondaryWindows.Clear();
            _openEditWindows.Clear();
        }

        public static bool TryActivateExistingEdit(string policyId)
        {
            if (_openEditWindows.TryGetValue(policyId, out var win))
            {
                try
                {
                    WindowHelpers.BringToFront(win);
                    win.Activate();
                    var timer = win.DispatcherQueue.CreateTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(180);
                    timer.IsRepeating = false;
                    timer.Tick += (s, e) => { try { WindowHelpers.BringToFront(win); win.Activate(); } catch { } };
                    timer.Start();
                }
                catch { }
                return true;
            }
            return false;
        }

        public static void RegisterEditWindow(string policyId, EditSettingWindow win)
        {
            _openEditWindows[policyId] = win;
            win.Closed += (s, e) => { _openEditWindows.Remove(policyId); };
        }
    }
}
