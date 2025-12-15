using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using PolicyPlusPlus.Logging;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.Windows;
using Windows.UI.ViewManagement;

namespace PolicyPlusPlus
{
    internal static partial class BuildInfo
    {
        private static string? _cached;
        public static string Version
        {
            get
            {
                if (_cached != null)
                    return _cached;
                try
                {
                    var asm = typeof(App).Assembly;
                    var info = asm.GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                        .FirstOrDefault()
                        ?.InformationalVersion;
                    if (!string.IsNullOrWhiteSpace(info))
                    {
                        var v = info.Trim();
                        _cached = v;
                        return _cached;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "App",
                        $"informational version read failed: {ex.GetType().Name} {ex.Message}"
                    );
                }
                _cached = "dev"; // Fallback when attribute missing
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
        private static UISettings? _uiSettings;

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
            try
            {
                Log.ConfigureFromArgs(Environment.GetCommandLineArgs());
            }
            catch { }
            Log.Info("App", "Application constructing");
            this.UnhandledException += (s, e) =>
            {
                try
                {
                    e.Handled = true;
                }
                catch { }
            };
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    try
                    {
                        ElevationService.Instance.ShutdownAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(
                            "App",
                            $"elevation shutdown exit hook failed: {ex.GetType().Name} {ex.Message}"
                        );
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Debug("App", $"attach ProcessExit failed: {ex.GetType().Name} {ex.Message}");
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Log.Info("App", "OnLaunched start");
            try
            {
                SettingsService.Instance.Initialize();
                var appSettings = SettingsService.Instance.LoadSettings();
                // Opportunistic cache maintenance: purge entries older than 30 days.
                try
                {
                    SettingsService.Instance.PurgeOldCacheEntries(TimeSpan.FromDays(30));
                }
                catch { }
                if (!string.IsNullOrEmpty(appSettings.Theme))
                    SetGlobalTheme(
                        appSettings.Theme switch
                        {
                            "Light" => ElementTheme.Light,
                            "Dark" => ElementTheme.Dark,
                            _ => ElementTheme.Default,
                        }
                    );
                if (!string.IsNullOrEmpty(appSettings.UIScale))
                {
                    if (double.TryParse(appSettings.UIScale.TrimEnd('%'), out var pct))
                        SetGlobalScale(Math.Max(0.5, pct / 100.0));
                }
                var (counts, lastUsed) = SettingsService.Instance.LoadSearchStats();
                SearchRankingService.Initialize(counts, lastUsed);
                // Observe cache enable/disable flips to start/stop host.
                try
                {
                    // Simple observer: reload settings on language event too heavy; add lightweight polling via event ties if needed later.
                    // For now, changes require relaunch or explicit Clear/Rebuild flows.
                }
                catch { }
            }
            catch (Exception ex)
            {
                Log.Warn("App", $"settings init failed", ex);
            }

            // Initialize ADMX cache + watcher in background without blocking UI launch
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                bool useCache = s.AdmxCacheEnabled ?? true;
                if (useCache)
                {
                    _ = AdmxCacheHostService.Instance.StartAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Debug("App", $"Admx cache host start failed: {ex.Message}");
            }

            var customPolVm = new ViewModels.CustomPolViewModel(
                SettingsService.Instance,
                SettingsService.Instance.LoadSettings().CustomPol
            );

            Window = new MainWindow();
            try
            {
                Window.Title = "Policy++";
            }
            catch { }
            if (Window.Content is FrameworkElement feRoot)
            {
                feRoot.DataContext = customPolVm;
            }
            ApplyThemeTo(Window);
            TryApplyIconTo(Window);

            // Show the window first, then run initialization in the background so startup feels instant.
            // Busy overlay inside MainWindow will indicate progress while ADMX loads.
            Window.Closed += async (s, e) =>
            {
                try
                {
                    var snap = SearchRankingService.GetSnapshot();
                    SettingsService.Instance.SaveSearchStats(snap.counts, snap.lastUsed);
                }
                catch (Exception ex)
                {
                    Log.Debug("App", $"save search stats failed: {ex.Message}");
                }
                try
                {
                    await AdmxCacheHostService.Instance.StopAsync();
                }
                catch (Exception ex)
                {
                    Log.Debug("App", $"Admx cache host stop failed: {ex.Message}");
                }
                try
                {
                    await ElevationService.Instance.ShutdownAsync();
                }
                catch (Exception ex)
                {
                    Log.Debug("App", $"elevation shutdown (Closed) failed: {ex.Message}");
                }
                CloseAllSecondaryWindows();
            };

            // Activate the window before triggering heavy initialization
            Window.Activate();

            // Kick off initialization without awaiting to avoid blocking first paint
            try
            {
                if (Window is MainWindow mw)
                {
                    _ = mw.EnsureInitializedAsync();
                }
                try
                {
                    CacheMaintenanceScheduler.Instance.Start();
                }
                catch { }
            }
            catch (Exception ex)
            {
                Log.Debug("App", $"async init failed: {ex.Message}");
            }
            Log.Info("App", "MainWindow activated");
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
            catch (Exception ex)
            {
                Log.Debug("App", $"set icon cached failed: {ex.Message}");
            }

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var names = asm.GetManifestResourceNames();
                var icoName = names.FirstOrDefault(n =>
                    n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                );
                if (!string.IsNullOrEmpty(icoName))
                {
                    using var s = asm.GetManifestResourceStream(icoName);
                    if (s != null)
                    {
                        var tempPath = System.IO.Path.Combine(
                            System.IO.Path.GetTempPath(),
                            "PolicyPlusAppIcon.ico"
                        );
                        using (
                            var fs = new System.IO.FileStream(
                                tempPath,
                                System.IO.FileMode.Create,
                                System.IO.FileAccess.Write,
                                System.IO.FileShare.Read
                            )
                        )
                        {
                            s.CopyTo(fs);
                        }
                        _iconPathCache = tempPath;
                        w.AppWindow?.SetIcon(tempPath);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("App", $"embedded icon apply failed: {ex.Message}");
            }

            try
            {
                string baseDir = AppContext.BaseDirectory;
                string assets = System.IO.Path.Combine(baseDir, "Assets");
                if (System.IO.Directory.Exists(assets))
                {
                    var path = System.IO.Directory.EnumerateFiles(assets, "*.ico").FirstOrDefault();
                    if (!string.IsNullOrEmpty(path))
                    {
                        _iconPathCache = path;
                        w.AppWindow?.SetIcon(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("App", $"fallback icon apply failed: {ex.Message}");
            }
        }

        public static void SetGlobalTheme(ElementTheme theme)
        {
            CurrentTheme = theme;
            if (Window != null)
                ApplyThemeTo(Window);
            foreach (var w in _secondaryWindows)
                ApplyThemeTo(w);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void SetGlobalScale(double scale)
        {
            if (scale <= 0)
                scale = 1.0;
            CurrentScale = scale;
            ScaleChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void ApplyThemeTo(Window w)
        {
            var desiredTheme =
                CurrentTheme == ElementTheme.Default ? ElementTheme.Default : CurrentTheme;
            if (w.Content is FrameworkElement fe)
                fe.RequestedTheme = desiredTheme;
            var immersiveTheme =
                desiredTheme == ElementTheme.Default ? GetEffectiveTheme(w) : desiredTheme;
            WindowHelpers.ApplyImmersiveDarkMode(w, immersiveTheme == ElementTheme.Dark);
        }

        internal static ElementTheme GetEffectiveTheme(Window? context = null)
        {
            if (CurrentTheme == ElementTheme.Dark || CurrentTheme == ElementTheme.Light)
                return CurrentTheme;

            try
            {
                var probe = context ?? Window;
                if (probe?.Content is FrameworkElement root)
                {
                    var actual = root.ActualTheme;
                    if (actual == ElementTheme.Dark || actual == ElementTheme.Light)
                        return actual;
                    var requested = root.RequestedTheme;
                    if (requested == ElementTheme.Dark || requested == ElementTheme.Light)
                        return requested;
                }
            }
            catch { }

            try
            {
                var requested = Current?.RequestedTheme;
                if (requested == ApplicationTheme.Dark)
                    return ElementTheme.Dark;
                if (requested == ApplicationTheme.Light)
                    return ElementTheme.Light;
            }
            catch { }

            try
            {
                _uiSettings ??= new UISettings();
                var color = _uiSettings.GetColorValue(UIColorType.Background);
                double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
                return luminance < 128 ? ElementTheme.Dark : ElementTheme.Light;
            }
            catch { }

            return ElementTheme.Dark;
        }

        public static void RegisterWindow(Window w)
        {
            if (w == Window)
                return;
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
                try
                {
                    w.Close();
                }
                catch { }
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
                    WindowHelpers.ActivateAndBringToFront(win);
                }
                catch { }
                return true;
            }
            return false;
        }

        public static void RegisterEditWindow(string policyId, EditSettingWindow win)
        {
            _openEditWindows[policyId] = win;
            win.Closed += (s, e) =>
            {
                _openEditWindows.Remove(policyId);
            };
        }
    }
}
