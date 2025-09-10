using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Microsoft.UI.Xaml.Documents;
using PolicyPlus.WinUI3.Windows;
using Microsoft.UI.Dispatching;
using System.Threading;
using Microsoft.UI.Xaml.Media.Animation;
using PolicyPlus.WinUI3.Utils;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Models;
using System.IO;
using CommunityToolkit.WinUI.UI.Controls;
using Windows.Foundation;
using PolicyPlus.WinUI3.Dialogs;
using PolicyPlus.WinUI3.ViewModels;
using PolicyPlus.Core.Utilities;
using PolicyPlus.Core.IO;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.Admx;
using System.Collections.Specialized;
using Microsoft.UI;
using Microsoft.UI.Windowing;
#if USE_VELOPACK
using Velopack;
#endif

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow : Window
    {
        public event EventHandler? Saved;
        // Raised whenever Local GPO policy sources are reloaded (after save/apply operations)
        public static event EventHandler? PolicySourcesRefreshed;
        
        private bool _hideEmptyCategories = true;
        private bool _showDetails = true;
        private GridLength? _savedDetailRowHeight;
        private GridLength? _savedSplitterRowHeight;

        private PolicyLoader? _loader;
        private AdmxBundle? _bundle;
        private List<PolicyPlusPolicy> _allPolicies = new();
        private List<PolicyPlusPolicy> _visiblePolicies = new();
        private Dictionary<string, List<PolicyPlusPolicy>> _nameGroups = new(System.StringComparer.InvariantCultureIgnoreCase);
        private int _totalGroupCount = 0;
        private AdmxPolicySection _appliesFilter = AdmxPolicySection.Both;
        private PolicyPlusCategory? _selectedCategory = null;
        private IPolicySource? _compSource;
        private IPolicySource? _userSource;
        private bool _configuredOnly = false;
        private bool _limitUnfilteredTo1000 = true;
        private bool _bookmarksOnly = false; // persisted flag (moved from Filtering.cs usage)
        private bool _suppressBookmarksOnlyChanged; // suppress persistence during load

        // Exposed for internal consumers instead of reflection (read-only to preserve invariants)
        internal AdmxBundle? Bundle => _bundle;
        internal IPolicySource? CompSource => _compSource;
        internal IPolicySource? UserSource => _userSource;

        // Map of visible policy id -> row for fast partial updates
        private readonly Dictionary<string, PolicyListRow> _rowByPolicyId = new(StringComparer.OrdinalIgnoreCase);

        // Temp .pol mode and save tracking
        private string? _tempPolCompPath = null;
        private string? _tempPolUserPath = null;
        private bool _useTempPol;

        private static readonly System.Text.RegularExpressions.Regex UrlRegex = new(@"(https?://[^\s]+)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Prevents SelectionChanged from re-applying category during programmatic tree updates
        private bool _suppressCategorySelectionChanged;
        private bool _suppressAppliesToSelectionChanged;
        private bool _suppressConfiguredOnlyChanged;

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

#if USE_VELOPACK
        // UpdateManager handled by UpdateHelper now.
#endif
        public MainWindow()
        {
            _suppressSearchOptionEvents = true;
            this.InitializeComponent();
            HookPendingQueue();
            TryInitCustomTitleBar();
            RootGrid.Loaded += (s, e) =>
            {
                try { ScaleHelper.Attach(this, ScaleHost, RootGrid); } catch { }
                InitUpdateMenuVisibility();
            };
            try { BookmarkService.Instance.ActiveListChanged += BookmarkService_ActiveListChanged; } catch { }
            try { Closed += (s, e) => { try { BookmarkService.Instance.ActiveListChanged -= BookmarkService_ActiveListChanged; } catch { } }; } catch { }
        }
        private void InitUpdateMenuVisibility()
        {
            try
            {
                if (Content is not FrameworkElement fe) return;
                var checkItem = fe.FindName("MenuCheckForUpdates") as MenuFlyoutItem;
                var storeItem = fe.FindName("MenuOpenStorePage") as MenuFlyoutItem;
                if (UpdateHelper.IsVelopackAvailable && checkItem != null) checkItem.Visibility = Visibility.Visible;
                if (UpdateHelper.IsStoreBuild && storeItem != null) storeItem.Visibility = Visibility.Visible;
            }
            catch { }
        }
        private async void MenuCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (!UpdateHelper.IsVelopackAvailable)
            { ShowInfo("Updates not available in this build."); return; }

            if (UpdateHelper.IsRestartPending)
            {
                if (UpdateHelper.IsDeferredInstall)
                {
                    ContentDialog df = new()
                    {
                        Title = "Update Ready (Deferred)",
                        Content = "An update has been prepared and will install when the app exits. Close and restart the application to complete installation.",
                        PrimaryButtonText = "Exit & Install Now",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    if (this.Content is FrameworkElement feDf) df.XamlRoot = feDf.XamlRoot;
                    ContentDialogResult dr;
                    try { dr = await df.ShowAsync(); } catch { dr = ContentDialogResult.None; }
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
                        Content = "An update has been downloaded and is ready to install. Restart the application now?",
                        PrimaryButtonText = "Restart Now",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary
                    };
                    if (this.Content is FrameworkElement feR) rd.XamlRoot = feR.XamlRoot;
                    ContentDialogResult rRes;
                    try { rRes = await rd.ShowAsync(); } catch { rRes = ContentDialogResult.None; }
                    if (rRes == ContentDialogResult.Primary)
                    {
                        App.Current.Exit();
                    }
                    else
                    {
                        ShowInfo("Restart later to finish applying the update.", InfoBarSeverity.Informational);
                    }
                    return;
                }
            }

            ShowInfo("Checking for updates...");
            var (ok, hasUpdate, message) = await UpdateHelper.CheckVelopackUpdatesAsync();
            if (!ok)
            { ShowInfo("Update check failed: " + message, InfoBarSeverity.Error); return; }
            if (!hasUpdate)
            { if (message != null) ShowInfo(message, InfoBarSeverity.Informational); return; }

            string notes = UpdateHelper.GetPendingUpdateNotes() ?? string.Empty;
            string body = string.IsNullOrEmpty(notes) ? "An update is available. Choose how to apply it." : "An update is available. Choose how to apply it.\n\n" + notes;

            ContentDialog choiceDlg = new()
            {
                Title = "Update Available",
                Content = body,
                PrimaryButtonText = "Install & Restart Now",
                SecondaryButtonText = "Install On Exit",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            if (this.Content is FrameworkElement root)
                choiceDlg.XamlRoot = root.XamlRoot;
            ContentDialogResult choiceRes;
            try { choiceRes = await choiceDlg.ShowAsync(); } catch { choiceRes = ContentDialogResult.None; }

            if (choiceRes != ContentDialogResult.Primary && choiceRes != ContentDialogResult.Secondary)
            {
                _ = UpdateHelper.ApplyVelopackPendingAsync(UpdateHelper.VelopackUpdateApplyChoice.Cancel);
                ShowInfo("Update canceled.", InfoBarSeverity.Informational);
                return;
            }

            var applyChoice = choiceRes == ContentDialogResult.Primary ? UpdateHelper.VelopackUpdateApplyChoice.RestartNow : UpdateHelper.VelopackUpdateApplyChoice.OnExit;
            ShowInfo(applyChoice == UpdateHelper.VelopackUpdateApplyChoice.RestartNow ? "Applying update and restarting..." : "Downloading update for apply-on-exit...");
            var (applyOk, restartInitiated, applyMessage) = await UpdateHelper.ApplyVelopackPendingAsync(applyChoice);
            if (!applyOk)
            { ShowInfo("Update failed: " + applyMessage, InfoBarSeverity.Error); return; }

            if (restartInitiated)
            {
                ShowInfo(applyMessage ?? "Restarting...", InfoBarSeverity.Success);
            }
            else
            {
                if (applyChoice == UpdateHelper.VelopackUpdateApplyChoice.OnExit)
                {
                    ShowInfo(applyMessage ?? "Update will be applied on exit.", InfoBarSeverity.Informational);
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
            { ShowInfo("Store page not available in this build."); return; }
            var (ok, message) = await UpdateHelper.OpenStorePageAsync();
            if (!ok) ShowInfo("Failed to open Store page: " + message, InfoBarSeverity.Error);
        }
        private void BookmarkService_ActiveListChanged(object? sender, EventArgs e)
        {
            try
            {
                // If bookmark-only filter is enabled, active list switch changes the visible set.
                if (_bookmarksOnly)
                {
                    DispatcherQueue.TryEnqueue(() => RebindConsideringAsync(SearchBox?.Text ?? string.Empty));
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
                    fe.Loaded += (_, __) => this.SetTitleBar(fe.FindName("AppTitleBar") as UIElement);
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
                    bar.Margin = new Thickness(left + 8, bar.Margin.Top, right + 8, bar.Margin.Bottom);
                }
            }
            catch { }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try { (RootGrid?.FindName("VersionText") as TextBlock)!.Text = BuildInfo.Version; } catch { }
            try
            {
                var s = SettingsService.Instance.LoadSettings();

                _hideEmptyCategories = s.HideEmptyCategories ?? true;
                try { ToggleHideEmptyMenu.IsChecked = _hideEmptyCategories; } catch { }

                // Restore persisted filter flags
                _configuredOnly = s.ConfiguredOnly ?? false;
                _bookmarksOnly = s.BookmarksOnly ?? false;
                try {
                    if (ChkConfiguredOnly != null) { _suppressConfiguredOnlyChanged = true; ChkConfiguredOnly.IsChecked = _configuredOnly; _suppressConfiguredOnlyChanged = false; }
                    if (ChkBookmarksOnly != null) { _suppressBookmarksOnlyChanged = true; ChkBookmarksOnly.IsChecked = _bookmarksOnly; _suppressBookmarksOnlyChanged = false; }
                } catch { }

                try { UpdateSearchPlaceholder(); } catch { }

                // Apply persisted detail pane ratio before showing
                ApplySavedDetailPaneRatioIfAny();

                _showDetails = s.ShowDetails ?? true;
                try { ViewDetailsToggle.IsChecked = _showDetails; } catch { }
                ApplyDetailsPaneVisibility();

                _limitUnfilteredTo1000 = s.LimitUnfilteredTo1000 ?? true; // default enabled
                try { ToggleLimitUnfilteredMenu.IsChecked = _limitUnfilteredTo1000; } catch { }

                var themePref = s.Theme ?? "System";
                ApplyTheme(themePref);
                App.SetGlobalTheme(themePref switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default });

                var scalePref = s.UIScale ?? "100%";
                SetScaleFromString(scalePref, updateSelector: false, save: false);

                // Column visibility from settings via menu toggles
                LoadColumnPrefs();

                // Ensure column events (Loaded / reorder / layout) are hooked so saved order & widths restore correctly
                try { HookColumnLayoutEvents(); } catch { }

                // Apply saved per-column layout (order, widths, visibility) AFTER initial toggle state
                try { ApplySavedColumnLayout(); } catch { }
                // Update 2nd language column header/visibility immediately
                try { ApplySecondLanguageVisibilityToViewMenu(); } catch { }

                // Apply saved layout (widths, sort)
                try { ApplyPersistedLayout(); } catch { }

                HookDoubleTapHandlers();

                string defaultPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\\PolicyDefinitions");
                string lastPath = s.AdmxSourcePath ?? defaultPath;
                if (Directory.Exists(lastPath))
                {
                    LoadAdmxFolderAsync(lastPath);
                }

                try { InitNavigation(); } catch { }
                try { UpdateSearchClearButtonVisibility(); } catch { }

                try
                {
                    var hist = SettingsService.Instance.LoadHistory();
                    foreach (var h in hist) PendingChangesService.Instance.History.Add(h);
                }
                catch { }

                // Sync initial UI state for search options (after reading settings)
                var so2 = s.Search;
                if (so2 != null)
                {
                    try
                    {
                        _suppressSearchOptionEvents = true;
                        if (SearchOptName != null) SearchOptName.IsChecked = so2.InName;
                        if (SearchOptId != null) SearchOptId.IsChecked = so2.InId;
                        if (SearchOptDesc != null) SearchOptDesc.IsChecked = so2.InDescription;
                        if (SearchOptComments != null) SearchOptComments.IsChecked = so2.InComments;
                        if (SearchOptRegKey != null) SearchOptRegKey.IsChecked = so2.InRegistryKey;
                        if (SearchOptRegValue != null) SearchOptRegValue.IsChecked = so2.InRegistryValue;
                    }
                    finally { _suppressSearchOptionEvents = false; }
                }
            }
            finally
            {
                // Ensure suppression is released even if an exception occurs
                _suppressSearchOptionEvents = false;
            }
        }

        private void HookDoubleTapHandlers()
        {
            if (_doubleTapHooked) return;
            try
            {
                if (PolicyList != null)
                {
                    PolicyList.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(PolicyList_DoubleTapped), true);
                }
                if (CategoryTree != null)
                {
                    CategoryTree.AddHandler(UIElement.TappedEvent, new TappedEventHandler(CategoryTree_Tapped), true);
                    CategoryTree.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(CategoryTree_DoubleTapped), true);
                }
                _doubleTapHooked = true;
            }
            catch { }
        }

        private CheckBox? GetFlag(string name)
            => (RootGrid?.FindName(name) as CheckBox);

        private void HookPendingQueue()
        {
            try
            {
                PendingChangesWindow.ChangesAppliedOrDiscarded += (_, __) => { UpdateUnsavedIndicator(); RefreshList(); PersistHistory(); };
                PendingChangesService.Instance.Pending.CollectionChanged += Pending_CollectionChanged;
                PendingChangesService.Instance.History.CollectionChanged += (_, __) => { PersistHistory(); };
                UpdateUnsavedIndicator();
            }
            catch { }
        }

        private void Pending_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            try
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (e.NewItems != null)
                {
                    foreach (var obj in e.NewItems)
                    {
                        if (obj is PendingChange pc && !string.IsNullOrEmpty(pc.PolicyId)) ids.Add(pc.PolicyId);
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (var obj in e.OldItems)
                    {
                        if (obj is PendingChange pc && !string.IsNullOrEmpty(pc.PolicyId)) ids.Add(pc.PolicyId);
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
                foreach (var id in ids)
                {
                    if (_rowByPolicyId.TryGetValue(id, out var row))
                    {
                        row.RefreshStateFromSourcesAndPending(_compSource, _userSource);
                    }
                }
            }
            catch { }
        }

        private void RefreshVisibleRows()
        {
            try
            {
                if (PolicyList?.ItemsSource is System.Collections.IEnumerable seq)
                {
                    foreach (var it in seq)
                    {
                        if (it is PolicyListRow row)
                        {
                            row.RefreshStateFromSourcesAndPending(_compSource, _userSource);
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
                    try { await Task.Delay(60, token); } catch { return; }
                    if (token.IsCancellationRequested) return;
                    DispatcherQueue.TryEnqueue(() => RebindConsideringAsync(SearchBox?.Text ?? string.Empty));
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
                    UnsavedIndicator.Visibility = (PendingChangesService.Instance.Pending.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
                }
            });
        }

        private void SetBusy(bool busy)
        {
            if (BusyOverlay == null) return;
            if (busy)
            {
                try { if (BusyText != null && string.IsNullOrWhiteSpace(BusyText.Text)) BusyText.Text = "Working..."; } catch { }
            }
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetBusy(bool busy, string? message)
        {
            if (BusyOverlay == null) return;
            try { if (BusyText != null && !string.IsNullOrEmpty(message)) BusyText.Text = message; } catch { }
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void LoadAdmxFolderAsync(string path)
        {
            SetBusy(true, "Loading...");
            try
            {
                var settings = SettingsService.Instance.LoadSettings();
                var langPref = settings.Language ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
                var secondEnabled = settings.SecondLanguageEnabled ?? false;
                var secondLang = secondEnabled ? (settings.SecondLanguage ?? "en-US") : string.Empty;
                bool useSecond = secondEnabled && !string.IsNullOrEmpty(secondLang) && !string.Equals(secondLang, langPref, StringComparison.OrdinalIgnoreCase);

                _currentAdmxPath = path; // for cache keys
                _currentLanguage = langPref; // for cache keys

                AdmxBundle? newBundle = null;
                int failureCount = 0;
                List<PolicyPlusPolicy>? allLocal = null;
                int totalGroupsLocal = 0;
                List<(PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower)>? searchIndexLocal = null;
                Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower)>? searchIndexByIdLocal = null;

                await Task.Run(() =>
                {
                    var b = new AdmxBundle();
                    var fails = b.LoadFolder(path, langPref);
                    newBundle = b;
                    failureCount = fails.Count();
                    allLocal = b.Policies.Values.ToList();
                    totalGroupsLocal = allLocal.GroupBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase).Count();

                    try
                    {
                        searchIndexLocal = allLocal.Select(p => (
                            Policy: p,
                            NameLower: SearchText.Normalize(p.DisplayName),
                            SecondLower: useSecond ? SearchText.Normalize(LocalizedTextService.GetPolicyNameIn(p, secondLang)) : string.Empty,
                            IdLower: SearchText.Normalize(p.UniqueID),
                            DescLower: SearchText.Normalize(p.DisplayExplanation)
                        )).ToList();
                        searchIndexByIdLocal = new Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower)>(StringComparer.OrdinalIgnoreCase);
                        foreach (var e in searchIndexLocal)
                        {
                            searchIndexByIdLocal[e.Policy.UniqueID] = e;
                        }
                    }
                    catch
                    {
                        searchIndexLocal = new List<(PolicyPlusPolicy, string, string, string, string)>();
                        searchIndexByIdLocal = new Dictionary<string, (PolicyPlusPolicy, string, string, string, string)>(StringComparer.OrdinalIgnoreCase);
                    }
                });

                _bundle = newBundle;
                _allPolicies = allLocal ?? new List<PolicyPlusPolicy>();
                _totalGroupCount = totalGroupsLocal;
                RegistryReferenceCache.Clear();
                _descIndexBuilt = false; // force rebuild or load from cache for description index

                if (searchIndexLocal != null && searchIndexByIdLocal != null)
                { _searchIndex = searchIndexLocal; _searchIndexById = searchIndexByIdLocal; }
                else { RebuildSearchIndex(); }

                // Attempt to prime second-language NGram from cache; do not force rebuild if available.
                if (useSecond)
                {
                    try
                    {
                        var fp2 = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, secondLang);
                        if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, secondLang, fp2, _secondIndex.N, "sec-" + secondLang, out var snap2) && snap2 != null)
                        {
                            _secondIndex.LoadSnapshot(snap2);
                            _secondIndexBuilt = true;
                        }
                        else
                        {
                            _secondIndexBuilt = false; // lazy build on demand
                        }
                    }
                    catch { _secondIndexBuilt = false; }
                }
                else
                {
                    _secondIndexBuilt = true; // nothing to build
                }

                StartPrebuildDescIndex();

                BuildCategoryTreeAsync();

                if (failureCount > 0)
                    ShowInfo($"ADMX load completed with {failureCount} issue(s).", InfoBarSeverity.Warning);
                else
                    ShowInfo($"ADMX loaded ({langPref}).");

                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            }
            finally { SetBusy(false); }
            await Task.CompletedTask;
        }

        private void AppliesToSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAppliesToSelectionChanged) return;
            var sel = (AppliesToSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            _appliesFilter = sel switch { "Computer" => AdmxPolicySection.Machine, "User" => AdmxPolicySection.User, _ => AdmxPolicySection.Both };
            _navTyping = false;
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            UpdateNavButtons();
        }

        private void EnsureLocalSources()
        {
            if (_loader is null || _userSource is null || _compSource is null)
            {
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                if (LoaderInfo != null) LoaderInfo.Text = _loader.GetDisplayInfo();
            }
            else
            {
                // Force switch to Local GPO
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                if (LoaderInfo != null) LoaderInfo.Text = _loader.GetDisplayInfo();
            }
        }

        private void EnsureTempPolPaths()
        {
            try
            {
                var baseDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PolicyPlus");
                System.IO.Directory.CreateDirectory(baseDir);
                if (string.IsNullOrEmpty(_tempPolCompPath))
                {
                    _tempPolCompPath = System.IO.Path.Combine(baseDir, "machine.pol");
                    try { var pol = new PolFile(); pol.Save(_tempPolCompPath); } catch { }
                }
                if (string.IsNullOrEmpty(_tempPolUserPath))
                {
                    _tempPolUserPath = System.IO.Path.Combine(baseDir, "user.pol");
                    try { var pol = new PolFile(); pol.Save(_tempPolUserPath); } catch { }
                }
            }
            catch { }
        }

        private void EnsureLocalSourcesUsingTemp()
        {
            if (!_useTempPol)
            {
                EnsureLocalSources();
                return;
            }
            EnsureTempPolPaths();
            var compLoader = new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolCompPath ?? string.Empty, false);
            var userLoader = new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolUserPath ?? string.Empty, true);
            _compSource = compLoader.OpenSource();
            _userSource = userLoader.OpenSource();
            _loader = compLoader;
            if (LoaderInfo != null) LoaderInfo.Text = "Temp POL (Comp/User)";
        }

        private void PolicyList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            try
            {
                if (PolicyList == null) return;
                DependencyObject? dep = e.OriginalSource as DependencyObject;
                DataGridRow? row = null;
                while (dep != null && row == null)
                {
                    if (dep is DataGridRow dgRow) row = dgRow; else dep = VisualTreeHelper.GetParent(dep);
                }
                if (row != null && row.DataContext != null)
                {
                    // Update selection so context actions operate on the intended item.
                    PolicyList.SelectedItem = row.DataContext;
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task OpenEditDialogForPolicyInternalAsync(PolicyPlusPolicy representative, bool ensureFront)
        {
            try { SearchRankingService.RecordUsage(representative.UniqueID); } catch { }
            await this.OpenEditDialogForPolicyAsync(representative, ensureFront);
        }

        private void BtnViewFormatted_Click(object sender, RoutedEventArgs e)
        {
            var row = PolicyList?.SelectedItem as PolicyListRow; var p = row?.Policy; if (p is null || _bundle is null) return;
            try { SearchRankingService.RecordUsage(p.UniqueID); } catch { }
            var win = new DetailPolicyFormattedWindow();
            win.Initialize(p, _bundle, _compSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource(), _userSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(), p.RawPolicy.Section);
            win.Activate();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _navTyping = false;
            SearchBox.Text = string.Empty; _selectedCategory = null; _configuredOnly = false; _bookmarksOnly = false; 
            if (ChkConfiguredOnly != null) { _suppressConfiguredOnlyChanged = true; ChkConfiguredOnly.IsChecked = false; _suppressConfiguredOnlyChanged = false; }
            if (ChkBookmarksOnly != null) { _suppressBookmarksOnlyChanged = true; ChkBookmarksOnly.IsChecked = false; _suppressBookmarksOnlyChanged = false; }
            try { SettingsService.Instance.UpdateConfiguredOnly(false); } catch { }
            try { SettingsService.Instance.UpdateBookmarksOnly(false); } catch { }
            UpdateSearchPlaceholder(); RunAsyncFilterAndBind();
            UpdateNavButtons();
        }

        private void ChkConfiguredOnly_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressConfiguredOnlyChanged) return; _configuredOnly = ChkConfiguredOnly?.IsChecked == true; try { SettingsService.Instance.UpdateConfiguredOnly(_configuredOnly); } catch { } _navTyping = false; RebindConsideringAsync(SearchBox?.Text ?? string.Empty); UpdateNavButtons();
        }

        private void ChkUseTempPol_Checked(object sender, RoutedEventArgs e)
        {
            _useTempPol = ChkUseTempPol?.IsChecked == true;
            if (_useTempPol)
            {
                EnsureTempPolPaths();
            }
            EnsureLocalSourcesUsingTemp();
            if (_useTempPol)
                ShowInfo($"Using temp .pol (Comp: {_tempPolCompPath}, User: {_tempPolUserPath})");
            else
                ShowInfo("Using Local GPO");
        }

        private void UpdateClearCategoryFilterButtonState()
        {
            try
            {
                var btn = RootGrid?.FindName("ClearCategoryFilterButton") as Button;
                if (btn != null) btn.IsEnabled = _selectedCategory != null;
            }
            catch { }
        }

        private async void BtnLanguage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string defaultPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\\PolicyDefinitions");
                string admxPath = SettingsService.Instance.LoadSettings().AdmxSourcePath ?? defaultPath;
                string currentLang = SettingsService.Instance.LoadSettings().Language ?? System.Globalization.CultureInfo.CurrentUICulture.Name;

                var dlg = new LanguageOptionsDialog();
                if (this.Content is FrameworkElement root)
                {
                    dlg.XamlRoot = root.XamlRoot;
                }
                dlg.Initialize(admxPath, currentLang);
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var chosen = dlg.SelectedLanguage;
                    bool langChanged = !string.IsNullOrEmpty(chosen) && !string.Equals(chosen, currentLang, StringComparison.OrdinalIgnoreCase);
                    if (langChanged)
                    {
                        try { SettingsService.Instance.UpdateLanguage(chosen!); } catch { }
                        LoadAdmxFolderAsync(admxPath); // this will handle second language priming
                    }
                    // 2nd language
                    var before = SettingsService.Instance.LoadSettings();
                    bool beforeEnabled = before.SecondLanguageEnabled ?? false;
                    string beforeSecond = before.SecondLanguage ?? "en-US";
                    bool afterEnabled = dlg.SecondLanguageEnabled;
                    string afterSecond = dlg.SelectedSecondLanguage ?? beforeSecond;
                    bool secondChanged = (beforeEnabled != afterEnabled) || !string.Equals(beforeSecond, afterSecond, StringComparison.OrdinalIgnoreCase);
                    try { SettingsService.Instance.UpdateSecondLanguageEnabled(afterEnabled); } catch { }
                    if (afterEnabled && !string.IsNullOrEmpty(afterSecond))
                    {
                        try { SettingsService.Instance.UpdateSecondLanguage(afterSecond); } catch { }
                    }

                    if (secondChanged && !langChanged)
                    {
                        // Rebuild in-memory search tuples to include new 2nd language names
                        RebuildSearchIndex();
                        // Try to load existing second-language NGram snapshot; fall back to lazy build
                        try
                        {
                            if (afterEnabled && !string.IsNullOrEmpty(afterSecond) && !string.Equals(afterSecond, currentLang, StringComparison.OrdinalIgnoreCase))
                            {
                                var fp2 = CacheService.ComputeAdmxFingerprint(_currentAdmxPath ?? admxPath, afterSecond);
                                if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath ?? admxPath, afterSecond, fp2, _secondIndex.N, "sec-" + afterSecond, out var snap2) && snap2 != null)
                                { _secondIndex.LoadSnapshot(snap2); _secondIndexBuilt = true; }
                                else { _secondIndexBuilt = false; }
                            }
                            else
                            {
                                _secondIndexBuilt = true; // nothing needed
                            }
                        }
                        catch { _secondIndexBuilt = false; }
                    }

                    ApplySecondLanguageVisibilityToViewMenu();
                    UpdateColumnVisibilityFromFlags();
                    if (!langChanged) RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
            catch
            {
                ShowInfo("Unable to open Language dialog.", InfoBarSeverity.Error);
            }
        }

        private async void BtnExportReg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                var hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeChoices.Add("Registry scripts", new System.Collections.Generic.List<string> { ".reg" });
                picker.SuggestedFileName = "export";
                var file = await picker.PickSaveFileAsync();
                if (file is null) return;

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
                if (this.Content is FrameworkElement root) dlg.XamlRoot = root.XamlRoot;
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
                if (this.Content is FrameworkElement root) dlg.XamlRoot = root.XamlRoot;
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary && dlg.ParsedReg != null)
                {
                    SetBusy(true, "Saving...");
                    try
                    {
                        if (_useTempPol)
                        {
                            // Apply into both temp POLs by hive
                            EnsureTempPolPaths();
                            var (userPolNew, machinePolNew) = RegImportHelper.ToPolByHive(dlg.ParsedReg);

                            if (!string.IsNullOrEmpty(_tempPolUserPath))
                            {
                                var userPath = _tempPolUserPath!;
                                var existingUser = File.Exists(userPath) ? PolFile.Load(userPath) : new PolFile();
                                userPolNew.Apply(existingUser);
                                existingUser.Save(userPath);
                            }

                            if (!string.IsNullOrEmpty(_tempPolCompPath))
                            {
                                var compPath = _tempPolCompPath!;
                                var existingComp = File.Exists(compPath) ? PolFile.Load(compPath) : new PolFile();
                                machinePolNew.Apply(existingComp);
                                existingComp.Save(compPath);
                            }

                            EnsureLocalSourcesUsingTemp();
                            ShowInfo(".reg imported to temp POLs (User/Machine).", InfoBarSeverity.Success);
                        }
                        else
                        {
                            // Apply into Local GPO via elevation host: split by hive and send each scope separately
                            var (userPol, machinePol) = RegImportHelper.ToPolByHive(dlg.ParsedReg);
                            string? machineB64 = null, userB64 = null;
                            if (machinePol != null)
                            {
                                using var msM = new MemoryStream(); using var bwM = new BinaryWriter(msM, System.Text.Encoding.Unicode, true);
                                machinePol.Save(bwM); msM.Position = 0; machineB64 = Convert.ToBase64String(msM.ToArray());
                            }
                            if (userPol != null)
                            {
                                using var msU = new MemoryStream(); using var bwU = new BinaryWriter(msU, System.Text.Encoding.Unicode, true);
                                userPol.Save(bwU); msU.Position = 0; userB64 = Convert.ToBase64String(msU.ToArray());
                            }
                            var res = await ElevationService.Instance.WriteLocalGpoBytesAsync(machineB64, userB64, triggerRefresh: true);
                            if (!res.Ok)
                            { ShowInfo(".reg import failed: " + (res.Error ?? "elevation error"), InfoBarSeverity.Error); return; }
                            RefreshLocalSources();
                            ShowInfo(".reg imported to Local GPO.");
                        }

                        // Refresh list to reflect new states
                        RefreshList();
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

        private static string SanitizeFileName(string name)
        {
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            return string.IsNullOrWhiteSpace(name) ? "export" : name;
        }

        public void RefreshLocalSources()
        {
            try
            {
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                if (LoaderInfo != null) LoaderInfo.Text = _loader.GetDisplayInfo();
            }
            catch { }
           try { PolicySourcesRefreshed?.Invoke(this, EventArgs.Empty); } catch { }
        }

        private void RefreshList()
        {
            try { RebindConsideringAsync(SearchBox?.Text ?? string.Empty); } catch { }
        }

        private void GoBackAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnBack_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void GoForwardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnForward_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                var point = e.GetCurrentPoint(this.Content as UIElement);
                var props = point.Properties;
                if (props.IsXButton1Pressed)
                {
                    BtnBack_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (props.IsXButton2Pressed)
                {
                    BtnForward_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
            catch { }
        }

        private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                if (PolicyList?.SelectedItem is PolicyListRow row && row.Policy is not null)
                {
                    e.Handled = true;
                    await OpenEditDialogForPolicyInternalAsync(row.Policy, ensureFront: true);
                }
                else if (PolicyList?.SelectedItem is PolicyListRow row2 && row2.Category is not null)
                {
                    e.Handled = true;
                    _selectedCategory = row2.Category;
                    UpdateSearchPlaceholder();
                    _navTyping = false;
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                    UpdateNavButtons();
                }
            }
        }

        private bool IsFromBookmarkButton(DependencyObject? dep)
        {
            while (dep != null)
            {
                if (dep is Button btn && btn.Tag is PolicyPlusPolicy)
                {
                    // Bookmark button identified by having a Policy tag and no complex content (FontIcon only)
                    return true;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void PolicyList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            DependencyObject? dep = e.OriginalSource as DependencyObject;

            // Suppress edit when the double-tap was on the bookmark toggle button
            if (IsFromBookmarkButton(dep)) { e.Handled = true; return; }

            object? item = null;

            var dgRow = FindAncestorDataGridRow(dep);
            if (dgRow != null)
            {
                item = dgRow.DataContext;
            }

            if (item == null)
                item = (e.OriginalSource as FrameworkElement)?.DataContext;
            if (item == null)
                item = PolicyList?.SelectedItem;

            if (item is PolicyListRow row && row.Policy is PolicyPlusPolicy pol)
            {
                e.Handled = true;
                _ = OpenEditDialogForPolicyInternalAsync(pol, ensureFront: true);
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
                if (start is DataGridRow dgr) return dgr;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
        }

        private void PolicyList_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            try
            {
                string? key = null;
                if (e.Column == ColName) key = nameof(PolicyListRow.DisplayName);
                else if (e.Column == ColId) key = nameof(PolicyListRow.ShortId);
                else if (e.Column == ColCategory) key = nameof(PolicyListRow.CategoryName); // Parent
                else if (e.Column == ColTopCategory) key = nameof(PolicyListRow.TopCategoryName); // Top
                else if (e.Column == ColCategoryPath) key = nameof(PolicyListRow.CategoryFullPath); // Full path
                else if (e.Column == ColApplies) key = nameof(PolicyListRow.AppliesText);
                else if (e.Column == ColSupported) key = nameof(PolicyListRow.SupportedText);
                if (string.IsNullOrEmpty(key)) return;

                if (string.Equals(_sortColumn, key, StringComparison.Ordinal))
                {
                    if (_sortDirection == DataGridSortDirection.Ascending) _sortDirection = DataGridSortDirection.Descending;
                    else if (_sortDirection == DataGridSortDirection.Descending) { _sortColumn = null; _sortDirection = null; }
                    else _sortDirection = DataGridSortDirection.Ascending;
                }
                else { _sortColumn = key; _sortDirection = DataGridSortDirection.Ascending; }

                try
                {
                    if (_sortColumn == null || _sortDirection == null) SettingsService.Instance.UpdateSort(null, null);
                    else SettingsService.Instance.UpdateSort(_sortColumn, _sortDirection == DataGridSortDirection.Descending ? "Desc" : "Asc");
                }
                catch { }

                foreach (var col in PolicyList.Columns) col.SortDirection = null;
                if (_sortColumn != null && _sortDirection != null)
                {
                    if (_sortColumn == nameof(PolicyListRow.DisplayName)) ColName.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.ShortId)) ColId.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.CategoryName)) ColCategory.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.TopCategoryName)) ColTopCategory.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.CategoryFullPath)) ColCategoryPath.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.AppliesText)) ColApplies.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.SupportedText)) ColSupported.SortDirection = _sortDirection;
                }

                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            }
            catch { }
        }

        private void UpdateSearchPlaceholder()
        {
            if (SearchBox == null) return;
            SearchBox.PlaceholderText = _selectedCategory != null ? $"Search policies in {_selectedCategory.DisplayName}" : "Search policies";
            UpdateClearCategoryFilterButtonState();
        }

        private void ViewDetailsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                _showDetails = t.IsChecked;
                try { SettingsService.Instance.UpdateShowDetails(_showDetails); } catch { }
                ApplyDetailsPaneVisibility();
            }
        }

        private void ClearCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            _selectedCategory = null;
            if (CategoryTree != null)
            {
                _suppressCategorySelectionChanged = true;
                var old = CategoryTree.SelectionMode;
                CategoryTree.SelectionMode = Microsoft.UI.Xaml.Controls.TreeViewSelectionMode.None;
                BuildCategoryTree();
                CategoryTree.SelectionMode = old;
                _suppressCategorySelectionChanged = false;
            }
            UpdateSearchPlaceholder();
            UpdateClearCategoryFilterButtonState();
            _navTyping = false;
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            UpdateNavButtons();
        }

        private void BtnLoadLocalGpo_Click(object sender, RoutedEventArgs e)
        {
            _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            _compSource = _loader.OpenSource();
            _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
            if (LoaderInfo != null) LoaderInfo.Text = _loader.GetDisplayInfo();
            ShowInfo("Local GPO sources initialized.");
        }

        private async void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new AboutDialog();
                if (this.Content is FrameworkElement fe)
                    dlg.XamlRoot = fe.XamlRoot;
                await dlg.ShowAsync();
            }
            catch { }
        }

        private async void BtnLoadAdmxFolder_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;
            try { SettingsService.Instance.UpdateAdmxSourcePath(folder.Path); } catch { }
            LoadAdmxFolderAsync(folder.Path);
        }

        private void ToggleLimitUnfilteredMenu_Click(object sender, RoutedEventArgs e)
        { try { if (sender is ToggleMenuFlyoutItem t) { _limitUnfilteredTo1000 = t.IsChecked; SettingsService.Instance.UpdateLimitUnfilteredTo1000(_limitUnfilteredTo1000); RebindConsideringAsync(SearchBox?.Text ?? string.Empty); } } catch { } }

        private void SearchOptionsFlyout_Opened(object sender, object e)
        { try { var so = SettingsService.Instance.LoadSettings().Search ?? new SearchOptions(); _suppressSearchOptionEvents = true; if (SearchOptName != null) SearchOptName.IsChecked = so.InName; if (SearchOptId != null) SearchOptId.IsChecked = so.InId; if (SearchOptDesc != null) SearchOptDesc.IsChecked = so.InDescription; if (SearchOptComments != null) SearchOptComments.IsChecked = so.InComments; if (SearchOptRegKey != null) SearchOptRegKey.IsChecked = so.InRegistryKey; if (SearchOptRegValue != null) SearchOptRegValue.IsChecked = so.InRegistryValue; } finally { _suppressSearchOptionEvents = false; } }

        private void RebindConsideringAsync(string q)
        {
            try { if (string.IsNullOrWhiteSpace(q)) RunAsyncFilterAndBind(); else RunAsyncSearchAndBind(q); } catch { } }

        private void UnsavedIndicator_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                var win = new PendingChangesWindow();
                win.Activate();
                try { WindowHelpers.BringToFront(win); } catch { }
            }
            catch { }
        }
    }
}
