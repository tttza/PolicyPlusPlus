using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusCore.Utilities;
using PolicyPlusPlus.Dialogs;
using PolicyPlusPlus.Logging;
using PolicyPlusPlus.Models;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.ViewModels;
using PolicyPlusPlus.Windows;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow : Window
    {
        public event EventHandler? Saved;

        private bool _hideEmptyCategories = true;
        private bool _showDetails = true;
        private GridLength? _savedDetailRowHeight;
        private GridLength? _savedSplitterRowHeight;

        private AdmxBundle? _bundle;
        private List<PolicyPlusPolicy> _allPolicies = new();
        private List<PolicyPlusPolicy> _visiblePolicies = new();
        private Dictionary<string, List<PolicyPlusPolicy>> _nameGroups = new(
            System.StringComparer.InvariantCultureIgnoreCase
        );
        private int _totalGroupCount = 0;
        private AdmxPolicySection _appliesFilter = AdmxPolicySection.Both;
        private PolicyPlusCategory? _selectedCategory = null;
        private bool _configuredOnly = false;
        private bool _limitUnfilteredTo1000 = true;
        private bool _bookmarksOnly = false; // persisted flag (moved from Filtering.cs usage)
        private bool _suppressBookmarksOnlyChanged; // suppress persistence during load

        // Exposed for internal consumers instead of reflection (read-only to preserve invariants)
        internal AdmxBundle? Bundle => _bundle;

        // Map of visible policy id -> row for fast partial updates
        private readonly Dictionary<string, PolicyListRow> _rowByPolicyId = new(
            StringComparer.OrdinalIgnoreCase
        );

        // Temp .pol mode and save tracking
        private string? _tempPolCompPath = null;
        private string? _tempPolUserPath = null;

        private static readonly System.Text.RegularExpressions.Regex UrlRegex = new(
            @"(https?://[^\s]+)",
            System.Text.RegularExpressions.RegexOptions.Compiled
                | System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Prevents SelectionChanged from re-applying category during programmatic tree updates
        private bool _suppressCategorySelectionChanged;
        private bool _suppressAppliesToSelectionChanged;

        // Typing suppression flag for navigation pushes
        private bool _navTyping;

        // Sorting state
        private string? _sortColumn;
        private DataGridSortDirection? _sortDirection;

        // Track current ADMX path and language for cache keys
        private string? _currentAdmxPath;
        private string? _currentLanguage;

        // Cross-partial fields (were previously lost during refactor)
        private CancellationTokenSource? _infoBarCloseCts;
        private double? _savedVerticalOffset;
        private string? _savedSelectedPolicyId;
        private ScrollViewer? _policyListScroll;
        private double? _lastKnownVerticalOffset;
        private CancellationTokenSource? _refreshDebounceCts;
        private double? _savedAnchorViewportY;
        private double? _savedAnchorRatio;
        private bool _doubleTapHooked; // used by HookDoubleTapHandlers
        private bool _initialized; // guard to run initialization once
        private AppSettings? _settingsCache; // cached settings snapshot
        private bool _forceComputeStatesOnce; // triggers one-time forced policy state computation on next bind

        public MainWindow()
        {
            this.InitializeComponent();
            try
            {
                ObserveSearchOptions();
            }
            catch { }
            try
            {
                TryHookCustomPolVm();
            }
            catch { }
            try
            {
                PolicySourceManager.Instance.SourcesChanged += (_, __) =>
                {
                    RefreshVisibleRows();
                    var li = RootGrid?.FindName("SourceStatusText") as TextBlock;
                    if (li != null)
                        li.Text = SourceStatusFormatter.FormatStatus();
                };
            }
            catch { }
            HookPendingQueue();
            TryInitCustomTitleBar();
            RootGrid.Loaded += (s, e) =>
            {
                try
                {
                    ScaleHelper.Attach(this, ScaleHost, RootGrid);
                }
                catch { }
                InitUpdateMenuVisibility();
                try
                {
                    LoadCustomPolSettings();
                }
                catch { }
                try
                {
                    ObserveSearchOptions();
                }
                catch { }
                try
                {
                    ObserveFilterOptions();
                }
                catch { }
            };
            try
            {
                BookmarkService.Instance.ActiveListChanged += BookmarkService_ActiveListChanged;
            }
            catch { }
            try
            {
                PendingChangesService.Instance.DirtyChanged += (_, __) => UpdateUnsavedIndicator();
            }
            catch { }
            try
            {
                // Notify user when ADMX cache rebuild starts/completes.
                EventHub.AdmxCacheRebuildStarted += (reason, changed) =>
                {
                    string msg = reason switch
                    {
                        "initial" => "Building ADMX cache...",
                        "languages" => "Rebuilding ADMX cache for language change...",
                        "sourcesRoot" => "Rebuilding ADMX cache for folder change...",
                        "cacheCleared" => "Rebuilding ADMX cache after clearing...",
                        "watcher" => changed != null && changed.Count > 0
                            ? $"ADMX change detected ({changed.Count}). Rebuilding cache..."
                            : "ADMX change detected. Rebuilding cache...",
                        _ => "Rebuilding ADMX cache...",
                    };
                    try
                    {
                        DispatcherQueue.TryEnqueue(() =>
                            ShowInfo(msg, InfoBarSeverity.Informational)
                        );
                    }
                    catch { }
                };
                EventHub.AdmxCacheRebuildCompleted += reason =>
                {
                    string done = reason switch
                    {
                        "initial" => "ADMX cache built.",
                        "languages" => "ADMX cache rebuilt for language change.",
                        "sourcesRoot" => "ADMX cache rebuilt for folder change.",
                        "cacheCleared" => "ADMX cache rebuilt after clearing.",
                        "watcher" => "ADMX cache rebuilt.",
                        _ => "ADMX cache rebuilt.",
                    };
                    try
                    {
                        DispatcherQueue.TryEnqueue(() => ShowInfo(done, InfoBarSeverity.Success));
                    }
                    catch { }
                };
            }
            catch { }
            try
            {
                Closed += (s, e) =>
                {
                    try
                    {
                        BookmarkService.Instance.ActiveListChanged -=
                            BookmarkService_ActiveListChanged;
                    }
                    catch { }
                    try
                    {
                        PendingChangesService.Instance.DirtyChanged -= (_, __) =>
                            UpdateUnsavedIndicator();
                    }
                    catch { }
                    try
                    {
                        EventHub.AdmxCacheRebuildStarted -= null; // no-op safeguard
                        EventHub.AdmxCacheRebuildCompleted -= null; // no-op safeguard
                    }
                    catch { }
                };
            }
            catch { }
            AppWindow.Closing += AppWindow_Closing;
        }

        private async void BtnClearCacheRebuild_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowInfo("Clearing cache...", InfoBarSeverity.Informational);
            }
            catch { }
            bool ok = false;
            try
            {
                // Stop ADMX cache host to release SQLite handles so files can be deleted.
                try
                {
                    await Services.AdmxCacheHostService.Instance.StopAsync();
                }
                catch { }
                ok = await SettingsService.Instance.ClearCacheDirectoryAsync();
                // Restart in background only if cache is enabled; rebuild will be kicked off by CacheCleared event.
                try
                {
                    var s = Services.SettingsService.Instance.LoadSettings();
                    if ((s.AdmxCacheEnabled ?? true) == true)
                        _ = Services.AdmxCacheHostService.Instance.StartAsync();
                }
                catch { }
            }
            catch
            {
                ok = false;
            }
            try
            {
                if (ok)
                    ShowInfo("Cache cleared. Rebuilding in background...", InfoBarSeverity.Success);
                else
                    ShowInfo("Failed to clear cache.", InfoBarSeverity.Error);
            }
            catch { }
        }

        private void InitUpdateMenuVisibility()
        {
            try
            {
                if (Content is not FrameworkElement fe)
                    return;
                // Initialize ADMX cache toggle from settings
                try
                {
                    var toggle = fe.FindName("ToggleAdmxCacheMenu") as ToggleMenuFlyoutItem;
                    if (toggle != null)
                    {
                        var s = SettingsService.Instance.LoadSettings();
                        toggle.IsChecked = s.AdmxCacheEnabled ?? true;
                    }
                }
                catch { }
                var checkItem = fe.FindName("MenuCheckForUpdates") as MenuFlyoutItem;
                var storeItem = fe.FindName("MenuOpenStorePage") as MenuFlyoutItem;
                var prereleaseToggle =
                    fe.FindName("MenuIncludePrereleaseUpdates") as ToggleMenuFlyoutItem;
                if (UpdateHelper.IsVelopackAvailable && checkItem != null)
                    checkItem.Visibility = Visibility.Visible;
                if (UpdateHelper.IsStoreBuild && storeItem != null)
                    storeItem.Visibility = Visibility.Visible;
                if (UpdateHelper.IsVelopackAvailable && prereleaseToggle != null)
                {
                    try
                    {
                        var s = SettingsService.Instance.LoadSettings();
                        prereleaseToggle.IsChecked = s.IncludePrereleaseUpdates ?? false;
                        prereleaseToggle.Visibility = Visibility.Visible;
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void MenuIncludePrereleaseUpdates_Click(object sender, RoutedEventArgs e)
        {
#if USE_VELOPACK
            try
            {
                if (sender is ToggleMenuFlyoutItem t)
                {
                    SettingsService.Instance.UpdateIncludePrereleaseUpdates(t.IsChecked);
#if USE_VELOPACK
                    UpdateHelper.ResetVelopackSource();
#endif
                    ShowInfo(
                        t.IsChecked
                            ? "Prerelease updates enabled (will include pre-release versions)."
                            : "Prerelease updates disabled (only stable releases).",
                        InfoBarSeverity.Informational
                    );
                }
            }
            catch { }
#endif
        }

        private void ToggleAdmxCacheMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not ToggleMenuFlyoutItem t)
                    return;
                bool enabled = t.IsChecked;
                SettingsService.Instance.UpdateAdmxCacheEnabled(enabled);
                if (enabled)
                {
                    _ = Services.AdmxCacheHostService.Instance.StartAsync();
                    ShowInfo(
                        "ADMX cache enabled. Building in background...",
                        InfoBarSeverity.Informational
                    );
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Services.AdmxCacheHostService.Instance.StopAsync();
                        }
                        catch { }
                    });
                    ShowInfo(
                        "ADMX cache disabled. Using legacy in-memory search.",
                        InfoBarSeverity.Informational
                    );
                }
            }
            catch { }
        }

        private async void MenuCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (!UpdateHelper.IsVelopackAvailable)
            {
                ShowInfo("Updates not available in this build.");
                return;
            }

            if (UpdateHelper.IsRestartPending)
            {
                if (UpdateHelper.IsDeferredInstall)
                {
                    ContentDialog df = new()
                    {
                        Title = "Update Ready (Deferred)",
                        Content =
                            "An update has been prepared and will install when the app exits. Close and restart the application to complete installation.",
                        PrimaryButtonText = "Exit & Install Now",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                    };
                    if (this.Content is FrameworkElement feDf)
                        df.XamlRoot = feDf.XamlRoot;
                    ContentDialogResult dr;
                    try
                    {
                        dr = await df.ShowAsync();
                    }
                    catch
                    {
                        dr = ContentDialogResult.None;
                    }
                    if (dr == ContentDialogResult.Primary)
                    {
                        App.Current.Exit();
                    }
                    return;
                }
                else
                {
                    ContentDialog rd = new()
                    {
                        Title = "Update Ready",
                        Content =
                            "An update has been downloaded and is ready to install. Restart the application now?",
                        PrimaryButtonText = "Restart Now",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                    };
                    if (this.Content is FrameworkElement feR)
                        rd.XamlRoot = feR.XamlRoot;
                    ContentDialogResult rRes;
                    try
                    {
                        rRes = await rd.ShowAsync();
                    }
                    catch
                    {
                        rRes = ContentDialogResult.None;
                    }
                    if (rRes == ContentDialogResult.Primary)
                    {
                        App.Current.Exit();
                    }
                    else
                    {
                        ShowInfo(
                            "Restart later to finish applying the update.",
                            InfoBarSeverity.Informational
                        );
                    }
                    return;
                }
            }

            ShowInfo("Checking for updates...");
            var (ok, hasUpdate, message) = await UpdateHelper.CheckVelopackUpdatesAsync();
            if (!ok)
            {
                ShowInfo("Update check failed: " + message, InfoBarSeverity.Error);
                return;
            }
            if (!hasUpdate)
            {
                if (message != null)
                    ShowInfo(message, InfoBarSeverity.Informational);
                return;
            }

            string notes = UpdateHelper.GetPendingUpdateNotes() ?? string.Empty;
            string body = string.IsNullOrEmpty(notes)
                ? "An update is available. Choose how to apply it."
                : "An update is available. Choose how to apply it.\n\n" + notes;

            ContentDialog choiceDlg = new()
            {
                Title = "Update Available",
                Content = body,
                PrimaryButtonText = "Install & Restart Now",
                SecondaryButtonText = "Install On Exit",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };
            if (this.Content is FrameworkElement root)
                choiceDlg.XamlRoot = root.XamlRoot;
            ContentDialogResult choiceRes;
            try
            {
                choiceRes = await choiceDlg.ShowAsync();
            }
            catch
            {
                choiceRes = ContentDialogResult.None;
            }

            if (
                choiceRes != ContentDialogResult.Primary
                && choiceRes != ContentDialogResult.Secondary
            )
            {
                _ = UpdateHelper.ApplyVelopackPendingAsync(
                    UpdateHelper.VelopackUpdateApplyChoice.Cancel
                );
                ShowInfo("Update canceled.", InfoBarSeverity.Informational);
                return;
            }

            var applyChoice =
                choiceRes == ContentDialogResult.Primary
                    ? UpdateHelper.VelopackUpdateApplyChoice.RestartNow
                    : UpdateHelper.VelopackUpdateApplyChoice.OnExit;
            ShowInfo(
                applyChoice == UpdateHelper.VelopackUpdateApplyChoice.RestartNow
                    ? "Applying update and restarting..."
                    : "Downloading update for apply-on-exit..."
            );
            var (applyOk, restartInitiated, applyMessage) =
                await UpdateHelper.ApplyVelopackPendingAsync(applyChoice);
            if (!applyOk)
            {
                ShowInfo("Update failed: " + applyMessage, InfoBarSeverity.Error);
                return;
            }

            if (restartInitiated)
            {
                ShowInfo(applyMessage ?? "Restarting...", InfoBarSeverity.Success);
            }
            else
            {
                if (applyChoice == UpdateHelper.VelopackUpdateApplyChoice.OnExit)
                {
                    ShowInfo(
                        applyMessage ?? "Update will be applied on exit.",
                        InfoBarSeverity.Informational
                    );
                }
                else
                {
                    ShowInfo(applyMessage ?? "Update staged.", InfoBarSeverity.Informational);
                }
            }
        }

        private async void MenuOpenStorePage_Click(object sender, RoutedEventArgs e)
        {
            if (!UpdateHelper.IsStoreBuild)
            {
                ShowInfo("Store page not available in this build.");
                return;
            }
            var (ok, message) = await UpdateHelper.OpenStorePageAsync();
            if (!ok)
                ShowInfo("Failed to open Store page: " + message, InfoBarSeverity.Error);
        }

        private void BookmarkService_ActiveListChanged(object? sender, EventArgs e)
        {
            try
            {
                // If bookmark-only filter is enabled, active list switch changes the visible set.
                if (_bookmarksOnly)
                {
                    DispatcherQueue.TryEnqueue(() =>
                        RebindConsideringAsync(SearchBox?.Text ?? string.Empty)
                    );
                }
                else
                {
                    // Otherwise just update bookmark icons/states for visible rows.
                    DispatcherQueue.TryEnqueue(RefreshVisibleRows);
                }
            }
            catch { }
        }

        private void TryInitCustomTitleBar()
        {
            try
            {
                var appWindow = this.AppWindow;
                if (appWindow is not null)
                {
                    appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    var transparent = global::Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    appWindow.TitleBar.ButtonBackgroundColor = transparent;
                    appWindow.TitleBar.ButtonInactiveBackgroundColor = transparent;
                    UpdateTitleBarMargin(appWindow);
                }
                if (Content is FrameworkElement fe)
                {
                    fe.Loaded += (_, __) =>
                        this.SetTitleBar(fe.FindName("AppTitleBar") as UIElement);
                }
            }
            catch { }
        }

        private void UpdateTitleBarMargin(AppWindow appWindow)
        {
            try
            {
                if (RootGrid?.FindName("AppTitleBar") is FrameworkElement bar)
                {
                    var left = appWindow.TitleBar.LeftInset;
                    var right = appWindow.TitleBar.RightInset;
                    bar.Margin = new Thickness(
                        left + 8,
                        bar.Margin.Top,
                        right + 8,
                        bar.Margin.Bottom
                    );
                }
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Retained for legacy path; main startup now calls EnsureInitializedAsync earlier.
            _ = EnsureInitializedAsync();
        }

        private async System.Threading.Tasks.Task InitializeUiFromSettingsAsync()
        {
            if (_initialized)
                return;
            _initialized = true;
            try
            {
                (RootGrid?.FindName("VersionText") as TextBlock)!.Text = BuildInfo.Version;
            }
            catch { }
#if USE_VELOPACK
            try
            {
                // Fire-and-forget background update check shortly after UI init to avoid UI thread jank.
                if (UpdateHelper.IsVelopackAvailable)
                {
                    _ = Task.Run(async () =>
                    {
                        // Small delay to let initial rendering settle.
                        await Task.Delay(10000).ConfigureAwait(false);
                        var (ok, hasUpdate, _) = await UpdateHelper
                            .CheckVelopackUpdatesAsync()
                            .ConfigureAwait(false);
                        if (ok && hasUpdate)
                        {
                            try
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    if (
                                        RootGrid?.FindName("UpdateAvailableBadge")
                                        is TextBlock badge
                                    )
                                    {
                                        badge.Visibility = Visibility.Visible;
                                    }
                                });
                            }
                            catch { }
                        }
                    });
                }
            }
            catch { }
#endif
            AppSettings s;
            try
            {
                s = _settingsCache ?? SettingsService.Instance.LoadSettings();
                _settingsCache = s;
            }
            catch
            {
                s = new AppSettings();
            }
            try
            {
                _hideEmptyCategories = s.HideEmptyCategories ?? true;
                try
                {
                    ToggleHideEmptyMenu.IsChecked = _hideEmptyCategories;
                }
                catch { }

                _configuredOnly = s.ConfiguredOnly ?? false;
                _bookmarksOnly = s.BookmarksOnly ?? false;
                try
                {
                    if (ChkConfiguredOnly != null)
                    {
                        ChkConfiguredOnly.IsChecked = _configuredOnly;
                    }
                    if (ChkBookmarksOnly != null)
                    {
                        ChkBookmarksOnly.IsChecked = _bookmarksOnly;
                    }
                    // Also push into the bound FilterViewModel to guarantee consistency even if bindings
                    // have not propagated yet when UI tests attach immediately after launch.
                    try
                    {
                        var fvm = FilterVM;
                        if (fvm != null)
                        {
                            fvm.ConfiguredOnly = _configuredOnly;
                            fvm.BookmarksOnly = _bookmarksOnly;
                        }
                    }
                    catch { }
                }
                catch { }

                try
                {
                    UpdateSearchPlaceholder();
                }
                catch { }
                // Detail pane ratio now applied lazily on first Show via ShowDetailsPane(); legacy call removed.

                _showDetails = s.ShowDetails ?? true;
                try
                {
                    ViewDetailsToggle.IsChecked = _showDetails;
                }
                catch { }
                // Keep FilterViewModel in sync for menu-driven flags as well.
                try
                {
                    var fvm2 = FilterVM;
                    if (fvm2 != null)
                    {
                        fvm2.HideEmptyCategories = _hideEmptyCategories;
                        fvm2.ShowDetails = _showDetails;
                    }
                }
                catch { }
                ApplyDetailsPaneVisibility();

                _limitUnfilteredTo1000 = s.LimitUnfilteredTo1000 ?? true;
                try
                {
                    ToggleLimitUnfilteredMenu.IsChecked = _limitUnfilteredTo1000;
                }
                catch { }

                var themePref = s.Theme ?? "System";
                ApplyTheme(themePref);
                App.SetGlobalTheme(
                    themePref switch
                    {
                        "Light" => ElementTheme.Light,
                        "Dark" => ElementTheme.Dark,
                        _ => ElementTheme.Default,
                    }
                );

                var scalePref = s.UIScale ?? "100%";
                SetScaleFromString(scalePref, updateSelector: false, save: false);

                LoadColumnPrefs();
                try
                {
                    HookColumnLayoutEvents();
                }
                catch { }
                try
                {
                    ApplySavedColumnLayout();
                }
                catch { }
                try
                {
                    ApplySecondLanguageVisibilityToViewMenu();
                }
                catch { }
                try
                {
                    ApplyPersistedLayout();
                }
                catch { }
                HookDoubleTapHandlers();

                string defaultPath = Environment.ExpandEnvironmentVariables(
                    @"%WINDIR%\\PolicyDefinitions"
                );
                try
                {
                    var testAdmx = Environment.GetEnvironmentVariable("POLICYPLUS_TEST_ADMX_DIR");
                    if (!string.IsNullOrWhiteSpace(testAdmx) && Directory.Exists(testAdmx))
                    {
                        defaultPath = testAdmx; // test override
                    }
                }
                catch { }
                string lastPath = s.AdmxSourcePath ?? defaultPath;
                if (Directory.Exists(lastPath))
                {
                    await LoadAdmxFolderAsync(lastPath);
                    // When launching directly into ConfiguredOnly view, ensure full bundle so state filtering is accurate.
                    if (_configuredOnly)
                    {
                        _forceComputeStatesOnce = true;
                        try
                        {
                            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                        }
                        catch { }
                    }
                }

                try
                {
                    InitNavigation();
                }
                catch { }
                try
                {
                    UpdateSearchClearButtonVisibility();
                }
                catch { }

                try
                {
                    var hist = SettingsService.Instance.LoadHistory();
                    foreach (var h in hist)
                        PendingChangesService.Instance.History.Add(h);
                }
                catch { }

                try
                {
                    ObserveSearchOptions();
                }
                catch { }
                try
                {
                    ObserveFilterOptions();
                }
                catch { }
            }
            catch { }
        }

        public System.Threading.Tasks.Task EnsureInitializedAsync() =>
            InitializeUiFromSettingsAsync();

        private void UpdateAvailableBadge_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
#if USE_VELOPACK
                // Reuse existing manual update flow when Velopack updater is available.
                MenuCheckForUpdates_Click(sender, e);
#else
                // Packaged (Store) build: no in-app manual updater; hide badge if tapped.
                if (sender is FrameworkElement fe)
                    fe.Visibility = Visibility.Collapsed;
#endif
            }
            catch { }
        }

        // Helper to detect double-tap originating from bookmark toggle button to suppress edit dialog.
        private bool IsFromBookmarkButton(DependencyObject? dep)
        {
            while (dep != null)
            {
                if (dep is Button btn && btn.Tag is PolicyPlusPolicy)
                    return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void HookDoubleTapHandlers()
        {
            if (_doubleTapHooked)
                return;
            try
            {
                if (PolicyList != null)
                {
                    PolicyList.AddHandler(
                        UIElement.DoubleTappedEvent,
                        new DoubleTappedEventHandler(PolicyList_DoubleTapped),
                        true
                    );
                }
                if (CategoryTree != null)
                {
                    CategoryTree.AddHandler(
                        UIElement.TappedEvent,
                        new TappedEventHandler(CategoryTree_Tapped),
                        true
                    );
                    CategoryTree.AddHandler(
                        UIElement.DoubleTappedEvent,
                        new DoubleTappedEventHandler(CategoryTree_DoubleTapped),
                        true
                    );
                }
                _doubleTapHooked = true;
            }
            catch { }
        }

        private CheckBox? GetFlag(string name) => (RootGrid?.FindName(name) as CheckBox);

        private void HookPendingQueue()
        {
            try
            {
                PendingChangesWindow.ChangesAppliedOrDiscarded += (_, __) =>
                {
                    UpdateUnsavedIndicator();
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                    PersistHistory();
                };
                PendingChangesService.Instance.Pending.CollectionChanged +=
                    Pending_CollectionChanged;
                PendingChangesService.Instance.History.CollectionChanged += (_, __) =>
                {
                    PersistHistory();
                };
                UpdateUnsavedIndicator();
            }
            catch { }
        }

        private void Pending_CollectionChanged(
            object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e
        )
        {
            try
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (e.NewItems != null)
                {
                    foreach (var obj in e.NewItems)
                    {
                        if (obj is PendingChange pc && !string.IsNullOrEmpty(pc.PolicyId))
                            ids.Add(pc.PolicyId);
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (var obj in e.OldItems)
                    {
                        if (obj is PendingChange pc && !string.IsNullOrEmpty(pc.PolicyId))
                            ids.Add(pc.PolicyId);
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateUnsavedIndicator();

                    // If ConfiguredOnly is active, the visible set may change; rebind
                    if (_configuredOnly)
                    {
                        RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                        return;
                    }

                    if (ids.Count == 0)
                    {
                        // Fallback: refresh visible rows
                        RefreshVisibleRows();
                        return;
                    }

                    UpdateVisibleRowsForPolicyIds(ids);
                });
            }
            catch { }
        }

        private void UpdateVisibleRowsForPolicyIds(IEnumerable<string> ids)
        {
            try
            {
                var ctx = PolicySourceAccessor.Acquire();
                foreach (var id in ids)
                {
                    if (_rowByPolicyId.TryGetValue(id, out var row))
                    {
                        row.RefreshStateFromSourcesAndPending(ctx.Comp, ctx.User);
                    }
                }
            }
            catch { }
        }

        private void RefreshVisibleRows()
        {
            try
            {
                var ctx = PolicySourceAccessor.Acquire();
                if (PolicyList?.ItemsSource is System.Collections.IEnumerable seq)
                {
                    foreach (var it in seq)
                    {
                        if (it is PolicyListRow row)
                        {
                            row.RefreshStateFromSourcesAndPending(ctx.Comp, ctx.User);
                        }
                    }
                }
            }
            catch { }
        }

        private void PersistHistory()
        {
            try
            {
                var list = PendingChangesService.Instance.History.ToList();
                SettingsService.Instance.SaveHistory(list);
            }
            catch { }
        }

        private void ScheduleRefreshList()
        {
            try
            {
                if (_lastKnownVerticalOffset.HasValue)
                    _savedVerticalOffset = _lastKnownVerticalOffset;

                _refreshDebounceCts?.Cancel();
                _refreshDebounceCts = new CancellationTokenSource();
                var token = _refreshDebounceCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(60, token);
                    }
                    catch
                    {
                        return;
                    }
                    if (token.IsCancellationRequested)
                        return;
                    DispatcherQueue.TryEnqueue(() =>
                        RebindConsideringAsync(SearchBox?.Text ?? string.Empty)
                    );
                });
            }
            catch { }
        }

        private void ColumnToggle_Click(object sender, RoutedEventArgs e)
        {
            SaveColumnPrefs();
            ApplyColumnVisibilityFromToggles();
            _navTyping = false;
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            UpdateNavButtons();
        }

        private void HiddenFlag_Checked(object sender, RoutedEventArgs e)
        {
            // keep compatibility but drive from toggles
            SaveColumnPrefs();
            ApplyColumnVisibilityFromToggles();
        }

        private void UpdateUnsavedIndicator()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (UnsavedIndicator != null)
                {
                    UnsavedIndicator.Visibility =
                        (PendingChangesService.Instance.Pending.Count > 0)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
            });
        }

        private void SetBusy(bool busy)
        {
            if (BusyOverlay == null)
                return;
            if (busy)
            {
                try
                {
                    if (BusyText != null && string.IsNullOrWhiteSpace(BusyText.Text))
                        BusyText.Text = "Working...";
                }
                catch { }
            }
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetBusy(bool busy, string? message)
        {
            if (BusyOverlay == null)
                return;
            try
            {
                if (BusyText != null && !string.IsNullOrEmpty(message))
                    BusyText.Text = message;
            }
            catch { }
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task LoadAdmxFolderAsync(string path)
        {
            SetBusy(true, "Loading...");
            try
            {
                var settings = SettingsService.Instance.LoadSettings();
                var langPref =
                    settings.Language ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
                var secondEnabled = settings.SecondLanguageEnabled ?? false;
                var secondLang = secondEnabled
                    ? (settings.SecondLanguage ?? "en-US")
                    : string.Empty;
                bool useSecond =
                    secondEnabled
                    && !string.IsNullOrEmpty(secondLang)
                    && !string.Equals(secondLang, langPref, StringComparison.OrdinalIgnoreCase);

                _currentAdmxPath = path; // for cache keys
                _currentLanguage = langPref; // for cache keys

                AdmxBundle? newBundle = null;
                int failureCount = 0;
                List<PolicyPlusPolicy>? allLocal = null;
                int totalGroupsLocal = 0;
                List<(
                    PolicyPlusPolicy Policy,
                    string NameLower,
                    string SecondLower,
                    string IdLower,
                    string DescLower
                )>? searchIndexLocal = null;
                Dictionary<
                    string,
                    (
                        PolicyPlusPolicy Policy,
                        string NameLower,
                        string SecondLower,
                        string IdLower,
                        string DescLower
                    )
                >? searchIndexByIdLocal = null;
                List<AdmxLoadFailure>? failuresLocal = null; // capture detailed failures

                // Always perform full XML load.
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        var b = new AdmxBundle();
                        try
                        {
                            bool allowPrimaryFallback = true;
                            try
                            {
                                allowPrimaryFallback =
                                    settings.PrimaryLanguageFallbackEnabled ?? true;
                            }
                            catch { }
                            b.EnableLanguageFallback = allowPrimaryFallback;
                        }
                        catch { }
                        var fails = b.LoadFolder(path, langPref);
                        failuresLocal = fails.ToList();
                        newBundle = b;
                        failureCount = failuresLocal.Count;
                        allLocal = b.Policies.Values.ToList();
                        totalGroupsLocal = allLocal
                            .GroupBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase)
                            .Count();
                        try
                        {
                            searchIndexLocal = allLocal
                                .Select(p =>
                                    (
                                        Policy: p,
                                        NameLower: SearchText.Normalize(p.DisplayName),
                                        SecondLower: useSecond
                                            ? SearchText.Normalize(
                                                LocalizedTextService.GetPolicyNameIn(p, secondLang)
                                            )
                                            : string.Empty,
                                        IdLower: SearchText.Normalize(p.UniqueID),
                                        DescLower: SearchText.Normalize(p.DisplayExplanation)
                                    )
                                )
                                .ToList();
                            searchIndexByIdLocal = new Dictionary<
                                string,
                                (
                                    PolicyPlusPolicy Policy,
                                    string NameLower,
                                    string SecondLower,
                                    string IdLower,
                                    string DescLower
                                )
                            >(StringComparer.OrdinalIgnoreCase);
                            foreach (var e in searchIndexLocal)
                                searchIndexByIdLocal[e.Policy.UniqueID] = e;
                        }
                        catch
                        {
                            searchIndexLocal =
                                new List<(PolicyPlusPolicy, string, string, string, string)>();
                            searchIndexByIdLocal = new Dictionary<
                                string,
                                (PolicyPlusPolicy, string, string, string, string)
                            >(StringComparer.OrdinalIgnoreCase);
                        }
                    });
                }

                // Log detailed failures (if any)
                if (failuresLocal != null && failuresLocal.Count > 0)
                {
                    foreach (var f in failuresLocal)
                    {
                        Log.Warn(
                            "ADMXLoad",
                            $"Failure ({f.FailType}) {f.AdmxPath}: {f.Info}".TrimEnd()
                        );
                    }
                }

                _bundle = newBundle;
                _allPolicies = allLocal ?? new List<PolicyPlusPolicy>();
                _totalGroupCount = totalGroupsLocal;

                // Attempt secondary bundle loads (en-US, en) to fill missing strings when primary ADML not present.
                try
                {
                    bool needFill = _allPolicies.Any(p => string.IsNullOrWhiteSpace(p.DisplayName));
                    bool allowPrimaryFallback = true;
                    try
                    {
                        allowPrimaryFallback = settings.PrimaryLanguageFallbackEnabled ?? true;
                    }
                    catch { }
                    if (needFill && allowPrimaryFallback)
                    {
                        var fallbackLangsOrdered = new List<string>();
                        if (!string.Equals(langPref, "en-US", StringComparison.OrdinalIgnoreCase))
                            fallbackLangsOrdered.Add("en-US");
                        if (!string.Equals(langPref, "en", StringComparison.OrdinalIgnoreCase))
                            fallbackLangsOrdered.Add("en");
                        foreach (var fb in fallbackLangsOrdered)
                        {
                            var fbBundle = new AdmxBundle { EnableLanguageFallback = false };
                            try
                            {
                                var fbFails = fbBundle.LoadFolder(path, fb); // we only need strings
                                foreach (var ff in fbFails)
                                    Log.Warn(
                                        "ADMXLoadFallback",
                                        $"Fallback load ({fb}) failure {ff.FailType} {ff.AdmxPath}: {ff.Info}".TrimEnd()
                                    );
                            }
                            catch
                            {
                                continue;
                            }
                            foreach (var p in _allPolicies)
                            {
                                if (!string.IsNullOrWhiteSpace(p.DisplayName))
                                    continue;
                                if (!fbBundle.Policies.TryGetValue(p.UniqueID, out var fbPol))
                                    continue;
                                if (!string.IsNullOrWhiteSpace(fbPol.DisplayName))
                                    p.DisplayName = fbPol.DisplayName;
                                if (
                                    string.IsNullOrWhiteSpace(p.DisplayExplanation)
                                    && !string.IsNullOrWhiteSpace(fbPol.DisplayExplanation)
                                )
                                    p.DisplayExplanation = fbPol.DisplayExplanation;
                                if (
                                    p.SupportedOn != null
                                    && fbPol.SupportedOn != null
                                    && string.IsNullOrWhiteSpace(p.SupportedOn.DisplayName)
                                    && !string.IsNullOrWhiteSpace(fbPol.SupportedOn.DisplayName)
                                )
                                    p.SupportedOn.DisplayName = fbPol.SupportedOn.DisplayName;
                            }
                            if (!_allPolicies.Any(x => string.IsNullOrWhiteSpace(x.DisplayName)))
                                break;
                        }
                    }
                }
                catch { }

                // Fill missing localized strings using fallback languages so policies remain visible.
                try
                {
                    var settingsForFallback = settings; // already loaded at method start
                    bool allowPrimaryFallback = true;
                    try
                    {
                        allowPrimaryFallback =
                            settingsForFallback.PrimaryLanguageFallbackEnabled ?? true;
                    }
                    catch { }
                    if (allowPrimaryFallback)
                    {
                        var fallbackLangs = new[]
                        {
                            langPref,
                            CultureInfo.CurrentUICulture.Name,
                            "en-US",
                            "en",
                        }
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .Select(l => l.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        bool anyChanged = false;
                        foreach (var p in _allPolicies)
                        {
                            bool missingName = string.IsNullOrWhiteSpace(p.DisplayName);
                            bool missingExplain = string.IsNullOrWhiteSpace(p.DisplayExplanation);
                            bool missingSupported =
                                p.SupportedOn != null
                                && string.IsNullOrWhiteSpace(p.SupportedOn.DisplayName);
                            if (!(missingName || missingExplain || missingSupported))
                                continue;
                            foreach (var fl in fallbackLangs)
                            {
                                if (missingName)
                                {
                                    var name = LocalizedTextService.GetPolicyNameIn(
                                        p,
                                        fl,
                                        useFallback: true
                                    );
                                    if (!string.IsNullOrWhiteSpace(name))
                                    {
                                        p.DisplayName = name;
                                        missingName = false;
                                        anyChanged = true;
                                    }
                                }
                                if (missingExplain)
                                {
                                    var exTxt = LocalizedTextService.GetPolicyExplanationIn(
                                        p,
                                        fl,
                                        useFallback: true
                                    );
                                    if (!string.IsNullOrWhiteSpace(exTxt))
                                    {
                                        p.DisplayExplanation = exTxt;
                                        missingExplain = false;
                                        anyChanged = true;
                                    }
                                }
                                if (missingSupported && p.SupportedOn != null)
                                {
                                    var sup = LocalizedTextService.GetSupportedDisplayIn(
                                        p,
                                        fl,
                                        useFallback: true
                                    );
                                    if (!string.IsNullOrWhiteSpace(sup))
                                    {
                                        p.SupportedOn.DisplayName = sup;
                                        missingSupported = false;
                                        anyChanged = true;
                                    }
                                }
                                if (!(missingName || missingExplain || missingSupported))
                                    break;
                            }
                            if (!allowPrimaryFallback && string.IsNullOrWhiteSpace(p.DisplayName))
                                continue;
                            if (string.IsNullOrWhiteSpace(p.DisplayName))
                            {
                                p.DisplayName = p.UniqueID;
                                anyChanged = true;
                            }
                        }
                        if (anyChanged)
                        {
                            try
                            {
                                searchIndexLocal = _allPolicies
                                    .Select(pp =>
                                        (
                                            Policy: pp,
                                            NameLower: SearchText.Normalize(pp.DisplayName),
                                            SecondLower: useSecond
                                                ? SearchText.Normalize(
                                                    LocalizedTextService.GetPolicyNameIn(
                                                        pp,
                                                        secondLang
                                                    )
                                                )
                                                : string.Empty,
                                            IdLower: SearchText.Normalize(pp.UniqueID),
                                            DescLower: SearchText.Normalize(pp.DisplayExplanation)
                                        )
                                    )
                                    .ToList();
                                searchIndexByIdLocal = new Dictionary<
                                    string,
                                    (
                                        PolicyPlusPolicy Policy,
                                        string NameLower,
                                        string SecondLower,
                                        string IdLower,
                                        string DescLower
                                    )
                                >(StringComparer.OrdinalIgnoreCase);
                                foreach (var e in searchIndexLocal)
                                    searchIndexByIdLocal[e.Policy.UniqueID] = e;
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                RegistryReferenceCache.Clear();
                _descIndexBuilt = false; // force rebuild or load from cache for description index

                if (searchIndexLocal != null && searchIndexByIdLocal != null)
                {
                    _searchIndex = searchIndexLocal;
                    _searchIndexById = searchIndexByIdLocal;
                }
                else
                {
                    RebuildSearchIndex();
                }

                // Snapshot persistence eliminated: rely on direct parse.

                // Do not load UI-level N-gram snapshots from disk anymore; indices will build on demand when cache is enabled.
                _secondIndexBuilt = useSecond ? false : true;

                StartPrebuildDescIndex();

                BuildCategoryTreeAsync();

                // Single-phase load only (no staged upgrade).

                if (failureCount > 0)
                {
                    // Summarize failure types for user
                    try
                    {
                        var groups = failuresLocal!
                            .GroupBy(f => f.FailType)
                            .Select(g => $"{g.Key}:{g.Count()}")
                            .ToList();
                        string detail = string.Join(", ", groups);
                        var firstFew = failuresLocal!
                            .Take(4)
                            .Select(f => Path.GetFileName(f.AdmxPath) + "(" + f.FailType + ")")
                            .ToList();
                        string sample = string.Join("; ", firstFew);
                        ShowInfo(
                            $"ADMX load completed with {failureCount} issue(s) [{detail}] e.g. {sample}.",
                            InfoBarSeverity.Warning
                        );
                    }
                    catch
                    {
                        ShowInfo(
                            $"ADMX load completed with {failureCount} issue(s).",
                            InfoBarSeverity.Warning
                        );
                    }
                }
                else
                    ShowInfo($"ADMX loaded ({langPref}).");

                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // Schedules hiding the busy overlay only after the DataGrid has realized at least one policy row (ensuring glyphs appear before flicker).
        // Busy overlay now hides immediately when load method finishes.

        // Previous multi-phase warm/full load removed; full bundle is always present.

        private void AppliesToSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAppliesToSelectionChanged)
                return;
            var sel = (AppliesToSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            _appliesFilter = sel switch
            {
                "Computer" => AdmxPolicySection.Machine,
                "User" => AdmxPolicySection.User,
                _ => AdmxPolicySection.Both,
            };
            _navTyping = false;
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            UpdateNavButtons();
        }

        private void EnsureTempPolPaths() { /* legacy no-op: creation handled inside PolicySourceManager */
        }

        private void PolicyList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                if (PolicyList == null)
                    return;
                DependencyObject? dep = e.OriginalSource as DependencyObject;
                DataGridRow? row = null;
                while (dep != null && row == null)
                {
                    if (dep is DataGridRow dgRow)
                        row = dgRow;
                    else
                        dep = VisualTreeHelper.GetParent(dep);
                }
                if (row != null && row.DataContext != null)
                {
                    // Update selection so context actions operate on the intended item.
                    PolicyList.SelectedItem = row.DataContext;
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task OpenEditDialogForPolicyInternalAsync(
            PolicyPlusPolicy representative,
            bool ensureFront
        )
        {
            try
            {
                SearchRankingService.RecordUsage(representative.UniqueID);
            }
            catch { }
            await this.OpenEditDialogForPolicyAsync(representative, ensureFront);
        }

        private void BtnViewFormatted_Click(object sender, RoutedEventArgs e)
        {
            var row = PolicyList?.SelectedItem as PolicyListRow;
            var p = row?.Policy;
            if (p is null || _bundle is null)
                return;
            try
            {
                SearchRankingService.RecordUsage(p.UniqueID);
            }
            catch { }
            var ctx = PolicySourceAccessor.Acquire();
            var win = new DetailPolicyFormattedWindow();
            win.Initialize(p, _bundle, ctx.Comp, ctx.User, p.RawPolicy.Section);
            win.Activate();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _navTyping = false;
            SearchBox.Text = string.Empty;
            _selectedCategory = null;
            _configuredOnly = false;
            _bookmarksOnly = false;
            if (ChkConfiguredOnly != null)
                ChkConfiguredOnly.IsChecked = false;
            if (ChkBookmarksOnly != null)
                ChkBookmarksOnly.IsChecked = false;
            try
            {
                SettingsService.Instance.UpdateConfiguredOnly(false);
            }
            catch { }
            try
            {
                SettingsService.Instance.UpdateBookmarksOnly(false);
            }
            catch { }
            UpdateSearchPlaceholder();
            RunAsyncFilterAndBind();
            UpdateNavButtons();
        }

        private async void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new PolicyPlusPlus.Dialogs.AboutDialog();
                if (Content is FrameworkElement fe)
                    dlg.XamlRoot = fe.XamlRoot;
                await dlg.ShowAsync();
            }
            catch { }
        }

        private async void ShortcutsMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new PolicyPlusPlus.Dialogs.ShortcutsDialog();
                if (Content is FrameworkElement fe)
                    dlg.XamlRoot = fe.XamlRoot;
                await dlg.ShowAsync();
            }
            catch { }
        }

        private async void BtnLoadAdmxFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new FolderPicker();
                var hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(dlg, hwnd);
                dlg.SuggestedStartLocation = PickerLocationId.Desktop;
                dlg.FileTypeFilter.Add(".xml");
                dlg.FileTypeFilter.Add(".admx");
                dlg.FileTypeFilter.Add(".zip");

                var file = await dlg.PickSingleFolderAsync();
                if (file == null)
                    return;
                SetBusy(true, "Loading ADMX folder...");
                try
                {
                    var path = file.Path;
                    var ok = true;
                    try
                    {
                        ok = Directory.Exists(path);
                    }
                    catch
                    {
                        ok = false;
                    }
                    if (!ok)
                    {
                        ShowInfo("Folder not found. Please re-select.", InfoBarSeverity.Error);
                        return;
                    }

                    string? lastPath = null;
                    try
                    {
                        lastPath = SettingsService.Instance.LoadSettings().AdmxSourcePath;
                    }
                    catch { }
                    bool isSamePath = false;
                    try
                    {
                        isSamePath = string.Equals(
                            lastPath,
                            path,
                            StringComparison.OrdinalIgnoreCase
                        );
                    }
                    catch { }

                    await LoadAdmxFolderAsync(path);

                    if (!isSamePath)
                    {
                        // New path; suggest reloading after save
                        ContentDialog reloadDlg = new()
                        {
                            Title = "ADMX Load Complete",
                            Content =
                                "The selected ADMX folder has been loaded. Save changes to use it as the default source?",
                            PrimaryButtonText = "Save & Reload",
                            SecondaryButtonText = "Discard Changes",
                            CloseButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Primary,
                        };
                        if (this.Content is FrameworkElement fe)
                            reloadDlg.XamlRoot = fe.XamlRoot;
                        ContentDialogResult res;
                        try
                        {
                            res = await reloadDlg.ShowAsync();
                        }
                        catch
                        {
                            res = ContentDialogResult.None;
                        }
                        if (res == ContentDialogResult.Primary)
                        {
                            // Save as new default
                            SettingsService.Instance.UpdateAdmxSourcePath(path);
                        }
                        else if (res == ContentDialogResult.None)
                        {
                            // cancel close
                            return;
                        }
                        else
                        {
                            // discard
                            PendingChangesService.Instance.DiscardAll();
                        }
                    }
                }
                finally
                {
                    SetBusy(false);
                }
            }
            catch (Exception ex)
            {
                SetBusy(false);
                ShowInfo("Failed to load ADMX folder: " + ex.Message, InfoBarSeverity.Error);
            }
        }

        private void ToggleLimitUnfilteredMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (FilterVM != null && sender is ToggleMenuFlyoutItem t)
                {
                    FilterVM.LimitUnfilteredTo1000 = t.IsChecked;
                }
            }
            catch { }
        }

        private void RebindConsideringAsync(string q, bool showBaselineOnEmpty = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    RunAsyncFilterAndBind(showBaselineOnEmpty);
                else
                    RunAsyncSearchAndBind(q);
            }
            catch { }
        }

        private void UnsavedIndicator_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // If no pending changes, act as a refresh
            if (PendingChangesService.Instance.Pending.Count == 0)
            {
                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                return;
            }

            // Otherwise, show the pending changes window
            var win = new PendingChangesWindow();
            win.Activate();
            try
            {
                WindowHelpers.BringToFront(win);
            }
            catch { }
        }

        private void ShowActiveSourceInfo()
        {
            try
            {
                UpdateSourceStatusUnified();
            }
            catch { }
        }

        private void PolicyList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            DependencyObject? dep = e.OriginalSource as DependencyObject;
            if (dep != null && IsFromBookmarkButton(dep))
            {
                e.Handled = true;
                return;
            }
            object? item = null;
            var dgRow = FindAncestorDataGridRow(dep);
            if (dgRow != null)
                item = dgRow.DataContext;
            if (item == null)
                item = (e.OriginalSource as FrameworkElement)?.DataContext;
            if (item == null)
                item = PolicyList?.SelectedItem;
            if (item is PolicyListRow row && row.Policy is PolicyPlusPolicy pol)
            {
                e.Handled = true;
                _ = OpenEditDialogForPolicyInternalAsync(pol, true);
                return;
            }
            if (item is PolicyListRow row2 && row2.Category is PolicyPlusCategory cat)
            {
                e.Handled = true;
                _selectedCategory = cat;
                UpdateSearchPlaceholder();
                _navTyping = false;
                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                SelectCategoryInTree(cat);
                UpdateNavButtons();
            }
        }

        private static DataGridRow? FindAncestorDataGridRow(DependencyObject? start)
        {
            while (start != null)
            {
                if (start is DataGridRow dgr)
                    return dgr;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
        }

        private void UpdateSearchPlaceholder()
        {
            if (SearchBox == null)
                return;
            SearchBox.PlaceholderText =
                _selectedCategory != null
                    ? $"Search policies in {_selectedCategory.DisplayName}"
                    : "Search policies";
            try
            {
                var btn = RootGrid?.FindName("ClearCategoryFilterButton") as Button;
                if (btn != null)
                    btn.IsEnabled = _selectedCategory != null;
            }
            catch { }
        }

        private void ViewDetailsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                _showDetails = t.IsChecked;
                try
                {
                    SettingsService.Instance.UpdateShowDetails(_showDetails);
                }
                catch { }
                ApplyDetailsPaneVisibility();
            }
        }

        private async void BtnExportReg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                var hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeChoices.Add(
                    "Registry scripts",
                    new System.Collections.Generic.List<string> { ".reg" }
                );
                picker.SuggestedFileName = "export";
                var file = await picker.PickSaveFileAsync();
                if (file is null)
                    return;
                var reg = PolicySourceSnapshot.SnapshotAllPolicyToReg();
                reg.Save(file.Path);
                ShowInfo(".reg exported.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfo("Failed to export .reg: " + ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void BtnImportPol_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new ImportPolDialog();
                if (this.Content is FrameworkElement root)
                    dlg.XamlRoot = root.XamlRoot;
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary && dlg.Pol != null)
                {
                    ShowInfo(".pol loaded (preview).", InfoBarSeverity.Informational);
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Failed to import .pol: " + ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void BtnImportReg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new ImportRegDialog();
                if (this.Content is FrameworkElement root)
                    dlg.XamlRoot = root.XamlRoot;
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary && dlg.ParsedReg != null)
                {
                    SetBusy(true, "Saving...");
                    try
                    {
                        bool tempMode =
                            PolicySourceManager.Instance.Mode == PolicySourceMode.TempPol;
                        if (tempMode)
                        {
                            EnsureTempPolPaths();
                            var (userPolNew, machinePolNew) = RegImportHelper.ToPolByHive(
                                dlg.ParsedReg
                            );
                            if (!string.IsNullOrEmpty(_tempPolUserPath))
                            {
                                var userPath = _tempPolUserPath!;
                                var existingUser = File.Exists(userPath)
                                    ? PolFile.Load(userPath)
                                    : new PolFile();
                                userPolNew.Apply(existingUser);
                                existingUser.Save(userPath);
                            }
                            if (!string.IsNullOrEmpty(_tempPolCompPath))
                            {
                                var compPath = _tempPolCompPath!;
                                var existingComp = File.Exists(compPath)
                                    ? PolFile.Load(compPath)
                                    : new PolFile();
                                machinePolNew.Apply(existingComp);
                                existingComp.Save(compPath);
                            }
                            ShowInfo(
                                ".reg imported to temp POLs (User/Machine).",
                                InfoBarSeverity.Success
                            );
                        }
                        else
                        {
                            var (userPol, machinePol) = RegImportHelper.ToPolByHive(dlg.ParsedReg);
                            string? machineB64 = null,
                                userB64 = null;
                            if (machinePol != null)
                            {
                                using var msM = new MemoryStream();
                                using var bwM = new BinaryWriter(
                                    msM,
                                    System.Text.Encoding.Unicode,
                                    true
                                );
                                machinePol.Save(bwM);
                                msM.Position = 0;
                                machineB64 = Convert.ToBase64String(msM.ToArray());
                            }
                            if (userPol != null)
                            {
                                using var msU = new MemoryStream();
                                using var bwU = new BinaryWriter(
                                    msU,
                                    System.Text.Encoding.Unicode,
                                    true
                                );
                                userPol.Save(bwU);
                                msU.Position = 0;
                                userB64 = Convert.ToBase64String(msU.ToArray());
                            }
                            var res = await ElevationService.Instance.WriteLocalGpoBytesAsync(
                                machineB64,
                                userB64,
                                triggerRefresh: true
                            );
                            if (!res.Ok)
                            {
                                ShowInfo(
                                    ".reg import failed: " + (res.Error ?? "elevation error"),
                                    InfoBarSeverity.Error
                                );
                                return;
                            }
                            RefreshLocalSources();
                            ShowInfo(".reg imported to Local GPO.");
                        }
                        RefreshVisibleRows();
                    }
                    finally
                    {
                        SetBusy(false);
                    }
                }
            }
            catch (Exception ex)
            {
                SetBusy(false);
                ShowInfo("Failed to import .reg: " + ex.Message, InfoBarSeverity.Error);
            }
        }

        private void RefreshLocalSources()
        {
            try
            {
                PolicySourceManager.Instance.Refresh();
            }
            catch { }
            try
            {
                if (
                    PolicyList != null
                    && PolicyList.ItemsSource is System.Collections.IEnumerable visSeq
                )
                {
                    var ids = visSeq
                        .OfType<PolicyListRow>()
                        .Select(r => r?.Policy?.UniqueID)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Select(id => id!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(2000)
                        .ToList();
                    if (ids.Count > 0)
                    {
                        EventHub.PublishPolicySourcesRefreshed(ids);
                        return;
                    }
                }
                EventHub.PublishPolicySourcesRefreshed(null);
            }
            catch
            {
                EventHub.PublishPolicySourcesRefreshed(null);
            }
        }

        private FilterViewModel? FilterVM =>
            (ScaleHost?.Resources?["FilterVM"] as FilterViewModel)
            ?? (RootGrid?.Resources?["FilterVM"] as FilterViewModel); // include ScaleHost resources (actual location)

        private void ObserveFilterOptions()
        {
            var vm = FilterVM;
            if (vm == null)
                return;
            vm.PropertyChanged += (_, args) =>
            {
                try
                {
                    if (args.PropertyName == nameof(FilterViewModel.ConfiguredOnly))
                    {
                        _configuredOnly = vm.ConfiguredOnly;
                        _navTyping = false;
                        RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                    }
                    else if (args.PropertyName == nameof(FilterViewModel.BookmarksOnly))
                    {
                        _bookmarksOnly = vm.BookmarksOnly;
                        _navTyping = false;
                        RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                    }
                    else if (args.PropertyName == nameof(FilterViewModel.LimitUnfilteredTo1000))
                    {
                        _limitUnfilteredTo1000 = vm.LimitUnfilteredTo1000;
                        _navTyping = false;
                        RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                    }
                    else if (args.PropertyName == nameof(FilterViewModel.HideEmptyCategories))
                    {
                        _hideEmptyCategories = vm.HideEmptyCategories;
                        BuildCategoryTree();
                    }
                    else if (args.PropertyName == nameof(FilterViewModel.ShowDetails))
                    {
                        _showDetails = vm.ShowDetails;
                        ApplyDetailsPaneVisibility();
                    }
                }
                catch { }
            };
            _configuredOnly = vm.ConfiguredOnly;
            _bookmarksOnly = vm.BookmarksOnly;
            _limitUnfilteredTo1000 = vm.LimitUnfilteredTo1000;
        }

        private void AppWindow_Closing(object? sender, AppWindowClosingEventArgs e)
        {
            try
            {
                if (PendingChangesService.Instance.IsDirty)
                {
                    PendingChangesService.Instance.DiscardAll();
                }
            }
            catch { }
        }

        private string? _lastInfoMessage;
        private DateTime _lastInfoTime;

        private void ShowInfoDedup(
            string message,
            InfoBarSeverity severity = InfoBarSeverity.Informational,
            int minIntervalMs = 1200
        )
        {
            try
            {
                var now = DateTime.UtcNow;
                if (
                    string.Equals(_lastInfoMessage, message, StringComparison.OrdinalIgnoreCase)
                    && (now - _lastInfoTime).TotalMilliseconds < minIntervalMs
                )
                {
                    // Update existing bar severity only (avoid flicker)
                    if (StatusBar != null)
                    {
                        StatusBar.Severity = severity;
                        if (!StatusBar.IsOpen)
                            StatusBar.IsOpen = true;
                    }
                    return;
                }
                _lastInfoMessage = message;
                _lastInfoTime = now;
                ShowInfo(message, severity);
            }
            catch { }
        }

        private void UpdateSourceStatusUnified()
        {
            var loaderInfo = GetLoaderInfo();
            if (loaderInfo != null)
            {
                loaderInfo.Text = SourceStatusFormatter.FormatStatus();
            }
            ShowInfoDedup(SourceStatusFormatter.FormatStatus(), InfoBarSeverity.Informational);
        }

        private void ChkUseTempPol_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is CheckBox cb)
                {
                    if (cb.IsChecked == true)
                    {
                        PolicySourceManager.Instance.Switch(PolicySourceDescriptor.TempPol());
                    }
                    else if (PolicySourceManager.Instance.Mode == PolicySourceMode.TempPol)
                    {
                        PolicySourceManager.Instance.Switch(PolicySourceDescriptor.LocalGpo());
                    }
                    UpdateSourceStatusUnified();
                    RefreshVisibleRows();
                }
            }
            catch
            {
                ShowInfo("Temp POL toggle failed", InfoBarSeverity.Error);
            }
        }

        private async void BtnLanguage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsService.Instance.LoadSettings();
                string currentLang = settings.Language ?? CultureInfo.CurrentUICulture.Name;
                bool fallbackBefore = settings.PrimaryLanguageFallbackEnabled ?? true; // capture prior fallback state
                string? admxPathCandidate = settings.AdmxSourcePath; // may be null
                string admxPath =
                    !string.IsNullOrWhiteSpace(admxPathCandidate)
                    && Directory.Exists(admxPathCandidate)
                        ? admxPathCandidate
                        : Environment.ExpandEnvironmentVariables(@"%WINDIR%\\PolicyDefinitions");
                var dlg = new LanguageOptionsDialog();
                if (Content is FrameworkElement fe)
                    dlg.XamlRoot = fe.XamlRoot;
                dlg.Initialize(admxPath, currentLang);
                var res = await dlg.ShowAsync();
                if (res != ContentDialogResult.Primary)
                    return;

                // Determine changes
                string? newPrimary = dlg.SelectedLanguage;
                bool primaryChanged = false;
                if (
                    !string.IsNullOrWhiteSpace(newPrimary)
                    && !string.Equals(
                        newPrimary,
                        settings.Language,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    SettingsService.Instance.UpdateLanguage(newPrimary);
                    primaryChanged = true;
                }
                // Even if unchanged, ensure settings reflects selection when previously null.
                else if (
                    string.IsNullOrWhiteSpace(settings.Language)
                    && !string.IsNullOrWhiteSpace(newPrimary)
                )
                {
                    SettingsService.Instance.UpdateLanguage(newPrimary);
                    primaryChanged = true;
                }

                bool beforeSecondEnabled = settings.SecondLanguageEnabled ?? false;
                string beforeSecond = settings.SecondLanguage ?? string.Empty;
                bool afterSecondEnabled = dlg.SecondLanguageEnabled;
                string? afterSecond = dlg.SelectedSecondLanguage;
                bool secondEnabledChanged = beforeSecondEnabled != afterSecondEnabled;
                bool secondLangChanged =
                    afterSecondEnabled
                    && !string.IsNullOrEmpty(afterSecond)
                    && !string.Equals(
                        beforeSecond,
                        afterSecond,
                        StringComparison.OrdinalIgnoreCase
                    );

                // Fallback preference (updated inside dialog already via SettingsService)
                bool fallbackAfter = dlg.PrimaryFallbackEnabledValue; // dialog property reflects user choice
                bool fallbackChanged = fallbackBefore != fallbackAfter;

                SettingsService.Instance.UpdateSecondLanguageEnabled(afterSecondEnabled);
                if (afterSecondEnabled && !string.IsNullOrEmpty(afterSecond))
                {
                    SettingsService.Instance.UpdateSecondLanguage(afterSecond);
                }

                if (primaryChanged || secondEnabledChanged || secondLangChanged || fallbackChanged)
                {
                    var updated = SettingsService.Instance.LoadSettings();
                    string? reloadPathCandidate = updated.AdmxSourcePath;
                    string reloadPath =
                        !string.IsNullOrWhiteSpace(reloadPathCandidate)
                        && Directory.Exists(reloadPathCandidate)
                            ? reloadPathCandidate
                            : admxPath;
                    if (Directory.Exists(reloadPath))
                    {
                        await LoadAdmxFolderAsync(reloadPath);
                        if (_configuredOnly)
                        {
                            _forceComputeStatesOnce = true;
                            try
                            {
                                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                            }
                            catch { }
                        }
                        ApplySecondLanguageVisibilityToViewMenu();
                        // Auto-show second name column if user had preference saved and second language just got enabled.
                        try
                        {
                            if (secondEnabledChanged && !beforeSecondEnabled && afterSecondEnabled)
                            {
                                var colsPref = updated.Columns;
                                bool wantSecond = colsPref?.ShowSecondName == true;
                                if (
                                    wantSecond
                                    && ColSecondName != null
                                    && ViewSecondNameToggle != null
                                )
                                {
                                    ViewSecondNameToggle.IsChecked = true;
                                    ColSecondName.Visibility = Visibility.Visible;
                                    SaveColumnPrefs(); // persist visibility change
                                }
                            }
                        }
                        catch { }
                        UpdateColumnVisibilityFromFlags();
                    }
                }
                else
                {
                    // No structural changes; still refresh in case only second language visibility toggled off
                    ApplySecondLanguageVisibilityToViewMenu();
                    UpdateColumnVisibilityFromFlags();
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Failed to apply language: " + ex.Message, InfoBarSeverity.Error);
            }
        }
    }
}
