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
using PolicyPlusPlus.Windows;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using Microsoft.UI.Windowing;
using PolicyPlusPlus.Logging; // logging

namespace PolicyPlusPlus
{
    internal static partial class BuildInfo
    {
        private static string? _cached;
        public static string Version
        {
            get
            {
                if (_cached != null) return _cached;
                try
                {
                    string baseDir = AppContext.BaseDirectory;
                    string path = System.IO.Path.Combine(baseDir, "gitversion.txt");
                    if (File.Exists(path))
                    {
                        var txt = File.ReadAllText(path).Trim();
                        if (!string.IsNullOrEmpty(txt)) { _cached = txt; return _cached; }
                    }
                }
                catch (Exception ex) { Log.Debug("App", $"version file read failed: {ex.GetType().Name} {ex.Message}"); }
                _cached = "dev";
                return _cached;
            }
        }
        public static string CreditsHeader => $"Policy++ {Version}";
    }
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
#if USE_VELOPACK
            try
            {
                Velopack.VelopackApp.Build().Run();
            }
            catch (Exception ex)
            {
                Log.Debug("App", $"Velopack init failed: {ex.GetType().Name} {ex.Message}");
            }
#endif
#if USE_STORE_UPDATE
            // Store update checks are not performed at startup (manual trigger only).
#endif
            InitializeComponent();
            this.UnhandledException += (s, e) => { try { e.Handled = true; } catch { } };
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { ElevationService.Instance.ShutdownAsync().GetAwaiter().GetResult(); } catch (Exception ex) { Log.Debug("App", $"elevation shutdown exit hook failed: {ex.GetType().Name} {ex.Message}"); } };
            }
            catch (Exception ex) { Log.Debug("App", $"attach ProcessExit failed: {ex.GetType().Name} {ex.Message}"); }
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                SettingsService.Instance.Initialize();
                var appSettings = SettingsService.Instance.LoadSettings();
                if (!string.IsNullOrEmpty(appSettings.Theme))
                    SetGlobalTheme(appSettings.Theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default });
                if (!string.IsNullOrEmpty(appSettings.UIScale))
                {
                    if (double.TryParse(appSettings.UIScale.TrimEnd('%'), out var pct))
                        SetGlobalScale(Math.Max(0.5, pct / 100.0));
                }
                var (counts, lastUsed) = SettingsService.Instance.LoadSearchStats();
                SearchRankingService.Initialize(counts, lastUsed);
            }
            catch (Exception ex) { Log.Warn("App", $"settings init failed", ex); }

            var customPolVm = new ViewModels.CustomPolViewModel(SettingsService.Instance, SettingsService.Instance.LoadSettings().CustomPol);

            Window = new MainWindow();
            try { Window.Title = "Policy++"; } catch { }
            if (Window.Content is FrameworkElement feRoot)
            {
                feRoot.DataContext = customPolVm;
            }
            ApplyThemeTo(Window);
            TryApplyIconTo(Window);

            // Await async UI initialization (ADMX load etc.) before first Activate where possible.
            try
            {
                if (Window is MainWindow mw)
                {
                    await mw.EnsureInitializedAsync();
                }
            }
            catch (Exception ex) { Log.Debug("App", $"async init failed: {ex.Message}"); }

            Window.Closed += async (s, e) =>
            {
                try
                {
                    var snap = SearchRankingService.GetSnapshot();
                    SettingsService.Instance.SaveSearchStats(snap.counts, snap.lastUsed);
                }
                catch (Exception ex) { Log.Debug("App", $"save search stats failed: {ex.Message}"); }
                try { await ElevationService.Instance.ShutdownAsync(); } catch (Exception ex) { Log.Debug("App", $"elevation shutdown (Closed) failed: {ex.Message}"); }
                CloseAllSecondaryWindows();
            };
            Window.Activate();
        }

        private static void TryApplyIconTo(Window w)
        {
            try
            {
                if (!string.IsNullOrEmpty(_iconPathCache))
                {
                    w.AppWindow?.SetIcon(_iconPathCache);
                    return;
                }
            }
            catch (Exception ex) { Log.Debug("App", $"set icon cached failed: {ex.Message}"); }

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
            catch (Exception ex) { Log.Debug("App", $"embedded icon apply failed: {ex.Message}"); }

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
            catch (Exception ex) { Log.Debug("App", $"fallback icon apply failed: {ex.Message}"); }
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
        { _secondaryWindows.Remove(w); }
        public static void CloseAllSecondaryWindows()
        {
            foreach (var w in _secondaryWindows.ToArray())
            { try { w.Close(); } catch { } }
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
