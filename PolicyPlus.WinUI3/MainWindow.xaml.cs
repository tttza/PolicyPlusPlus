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
using PolicyPlus.WinUI3.Dialogs; // FindByRegistryWinUI
using PolicyPlus.WinUI3.ViewModels;
using PolicyPlus.Core.Utilities;
using PolicyPlus.Core.IO;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.Admx; // RegistryViewFormatter
using System.Collections.Specialized;
using Microsoft.UI;
using Microsoft.UI.Windowing;

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

        // Suppress Search options change handlers while syncing UI
        private bool _suppressSearchOptionEvents;

        // Auto-close InfoBar cancellation
        private CancellationTokenSource? _infoBarCloseCts;

        private double? _savedVerticalOffset;
        private string? _savedSelectedPolicyId;

        // Track scroll viewer and last-known user offset to survive rapid refreshes
        private ScrollViewer? _policyListScroll;
        private double? _lastKnownVerticalOffset;

        // Debounce refresh caused by pending changes traffic
        private CancellationTokenSource? _refreshDebounceCts;

        // Preserve the selected row's viewport Y so it can be put back to the same on-screen position
        private double? _savedAnchorViewportY;
        private double? _savedAnchorRatio;

        // Persisted column prefs keys
        private const string ColIdKey = "Columns.ShowId";
        private const string ColCategoryKey = "Columns.ShowCategory";
        private const string ColAppliesKey = "Columns.ShowApplies";
        private const string ColSupportedKey = "Columns.ShowSupported";
        private const string ColUserStateKey = "Columns.ShowComputerState";
        private const string ColCompStateKey = "Columns.ShowUserState";

        private const string ScaleKey = "UIScale"; // e.g. "100%"
        private const string ShowDetailsKey = "View.ShowDetails";

        // One-time hook to ensure double-tap is captured even if handled by child controls
        private bool _doubleTapHooked;

        // Recent input tracking to reconcile ItemInvoked vs DoubleTapped
        private string? _recentDoubleTapCategoryId;
        private DateTime _recentDoubleTapAt;
        private string? _lastInvokedCatId;
        private DateTime _lastInvokedAt;
        private string? _lastTapCatId;
        private bool _lastTapWasExpanded;
        private DateTime _lastTapAt;

        // Typing suppression flag for navigation pushes
        private bool _navTyping;

        // Lightweight search index for faster case-insensitive substring matching (include English name)
        private List<(PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower)> _searchIndex = new();
        private Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower)> _searchIndexById = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _searchDebounceCts;

        // Debounce for search option changes
        private CancellationTokenSource? _searchOptionsDebounceCts;

        // Search options
        private bool _searchInName = true;
        private bool _searchInId = true;
        private bool _searchInRegistryKey = true;   // interprets as path by default
        private bool _searchInRegistryValue = true; // interprets as key name
        private bool _searchInDescription = false;
        private bool _searchInComments = false;

        // Comments storage (in-memory)
        private readonly Dictionary<string, string> _compComments = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _userComments = new(StringComparer.OrdinalIgnoreCase);

        // Sorting state
        private string? _sortColumn;
        private DataGridSortDirection? _sortDirection;

        // Track current ADMX path and language for cache keys
        private string? _currentAdmxPath;
        private string? _currentLanguage;

        public MainWindow()
        {
            _suppressSearchOptionEvents = true;
            this.InitializeComponent();
            HookPendingQueue();
            TryInitCustomTitleBar();
            RootGrid.Loaded += (s, e) =>
            {
                try { ScaleHelper.Attach(this, ScaleHost, RootGrid); } catch { }
            };
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

                var so = s.Search;
                if (so != null)
                {
                    _searchInName = so.InName;
                    _searchInId = so.InId;
                    _searchInRegistryKey = so.InRegistryKey;
                    _searchInRegistryValue = so.InRegistryValue;
                    _searchInDescription = so.InDescription;
                    _searchInComments = so.InComments;
                }

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

        // preference helper implementations are defined in MainWindow.Preferences.cs

        private void StartPrebuildDescIndex()
        {
            try
            {
                var path = _currentAdmxPath;
                var lang = _currentLanguage;
                var fp = (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(lang)) ? CacheService.ComputeAdmxFingerprint(path!, lang!) : string.Empty;
                var policies = _allPolicies.ToList();
                _ = Task.Run(() =>
                {
                    try
                    {
                        var idx = new NGramTextIndex(2);
                        var items = policies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayExplanation)));
                        idx.Build(items);
                        var snap = idx.GetSnapshot();
                        if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(lang))
                        {
                            if (!string.IsNullOrEmpty(fp))
                                CacheService.SaveNGramSnapshot(path!, lang!, fp, snap);
                            else
                                CacheService.SaveNGramSnapshot(path!, lang!, snap);
                        }
                        _descIndex.LoadSnapshot(snap);
                        _descIndexBuilt = true;
                    }
                    catch { }
                });
            }
            catch { }
        }

        private async void LoadAdmxFolderAsync(string path)
        {
            SetBusy(true, "Loading...");
            try
            {
                var langPref = SettingsService.Instance.LoadSettings().Language ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
                _currentAdmxPath = path; // for cache keys
                _currentLanguage = langPref; // for cache keys

                AdmxBundle? newBundle = null;
                int failureCount = 0;
                List<PolicyPlusPolicy>? allLocal = null;
                int totalGroupsLocal = 0;
                List<(PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower)>? searchIndexLocal = null;
                Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower)>? searchIndexByIdLocal = null;

                await Task.Run(() =>
                {
                    var b = new AdmxBundle();
                    var fails = b.LoadFolder(path, langPref);
                    newBundle = b;
                    failureCount = fails.Count();
                    allLocal = b.Policies.Values.ToList();
                    totalGroupsLocal = allLocal.GroupBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase).Count();

                    // Build search index off the UI thread
                    try
                    {
                        searchIndexLocal = allLocal.Select(p => (
                            Policy: p,
                            NameLower: SearchText.Normalize(p.DisplayName),
                            EnglishLower: SearchText.Normalize(EnglishTextService.GetEnglishPolicyName(p)),
                            IdLower: SearchText.Normalize(p.UniqueID),
                            DescLower: SearchText.Normalize(p.DisplayExplanation)
                        )).ToList();
                        searchIndexByIdLocal = new Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower)>(StringComparer.OrdinalIgnoreCase);
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

                // Adopt prebuilt search index
                if (searchIndexLocal != null && searchIndexByIdLocal != null)
                {
                    _searchIndex = searchIndexLocal;
                    _searchIndexById = searchIndexByIdLocal;
                }
                else
                {
                    RebuildSearchIndex();
                }

                // Prebuild description index and cache in the background
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

        private void RebuildSearchIndex()
        {
            try
            {
                _searchIndex = _allPolicies.Select(p => (
                    Policy: p,
                    NameLower: SearchText.Normalize(p.DisplayName),
                    EnglishLower: SearchText.Normalize(EnglishTextService.GetEnglishPolicyName(p)),
                    IdLower: SearchText.Normalize(p.UniqueID),
                    DescLower: SearchText.Normalize(p.DisplayExplanation)
                )).ToList();
                _searchIndexById = new Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower)>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in _searchIndex)
                {
                    _searchIndexById[e.Policy.UniqueID] = e;
                }
            }
            catch
            {
                _searchIndex = new List<(PolicyPlusPolicy, string, string, string, string)>();
                _searchIndexById = new Dictionary<string, (PolicyPlusPolicy, string, string, string, string)>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static int MatchScoreFor(string textLower, string qLower)
        {
            if (string.IsNullOrEmpty(qLower)) return 0;
            if (string.Equals(textLower, qLower, StringComparison.Ordinal)) return 100;
            if (textLower.StartsWith(qLower, StringComparison.Ordinal)) return 60;
            int idx = textLower.IndexOf(qLower, StringComparison.Ordinal);
            if (idx > 0)
            {
                char prev = textLower[idx - 1];
                if (!char.IsLetterOrDigit(prev)) return 40;
                return 20;
            }
            return -1000; // no match
        }

        private List<string> BuildSuggestions(string q, HashSet<string> allowed)
        {
            var qLower = SearchText.Normalize(q);
            var bestByName = new Dictionary<string, (int score, string name)>(StringComparer.OrdinalIgnoreCase);
            bool smallSubset = allowed.Count > 0 && allowed.Count < (_allPolicies.Count / 2);

            void Consider((PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower) e)
            {
                if (!allowed.Contains(e.Policy.UniqueID)) return;
                int score = 0;
                if (!string.IsNullOrEmpty(qLower))
                {
                    int nameScore = MatchScoreFor(e.NameLower, qLower);
                    int enScore = string.IsNullOrEmpty(e.EnglishLower) ? -1000 : MatchScoreFor(e.EnglishLower, qLower);
                    int idScore = MatchScoreFor(e.IdLower, qLower);
                    int descScore = _searchInDescription ? MatchScoreFor(e.DescLower, qLower) : -1000;
                    if (nameScore <= -1000 && enScore <= -1000 && idScore <= -1000 && descScore <= -1000) return;
                    score += Math.Max(0, Math.Max(nameScore, enScore)) * 3;
                    score += Math.Max(0, idScore) * 2;
                    score += Math.Max(0, descScore);
                }
                score += SearchRankingService.GetBoost(e.Policy.UniqueID);

                var name = e.Policy.DisplayName ?? string.Empty;
                if (string.IsNullOrEmpty(name)) name = e.Policy.UniqueID;
                if (bestByName.TryGetValue(name, out var cur))
                {
                    if (score > cur.score) bestByName[name] = (score, name);
                }
                else
                {
                    bestByName[name] = (score, name);
                }
            }

            if (smallSubset)
            {
                foreach (var id in allowed)
                {
                    if (_searchIndexById.TryGetValue(id, out var entry))
                        Consider(entry);
                }
            }
            else
            {
                foreach (var e in _searchIndex)
                    Consider(e);
            }

            if (bestByName.Count == 0 && string.IsNullOrEmpty(qLower))
            {
                foreach (var id in allowed)
                {
                    if (_searchIndexById.TryGetValue(id, out var e))
                    {
                        int score = SearchRankingService.GetBoost(id);
                        var name = e.Policy.DisplayName ?? string.Empty;
                        if (bestByName.TryGetValue(name, out var cur))
                        { if (score > cur.score) bestByName[name] = (score, name); }
                        else bestByName[name] = (score, name);
                    }
                }
            }

            var ordered = bestByName.Values
                .OrderByDescending(v => v.score)
                .ThenBy(v => v.name, StringComparer.InvariantCultureIgnoreCase)
                .Take(10)
                .Select(v => v.name)
                .ToList();
            return ordered;
        }

        private void SearchClearBtn_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                _navTyping = false;
                if (SearchBox != null)
                {
                    SearchBox.Text = string.Empty;
                }
                UpdateSearchClearButtonVisibility();
                RunAsyncFilterAndBind();
                UpdateNavButtons();
                e.Handled = true;
            }
            catch { }
        }

        private void UpdateSearchClearButtonVisibility()
        {
            try
            {
                var btn = RootGrid?.FindName("SearchClearBtn") as UIElement;
                if (btn != null)
                {
                    var hasText = !string.IsNullOrEmpty(SearchBox?.Text);
                    btn.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            try { HideBuiltInSearchClearButton(); } catch { }
            try { UpdateSearchClearButtonVisibility(); } catch { }
        }

        private void HideBuiltInSearchClearButton()
        {
            try
            {
                if (SearchBox == null) return;
                var deleteBtn = FindDescendantByName(SearchBox, "DeleteButton") as UIElement;
                if (deleteBtn != null)
                {
                    deleteBtn.Visibility = Visibility.Collapsed;
                    deleteBtn.IsHitTestVisible = false;
                    if (deleteBtn is Control c)
                    {
                        c.IsEnabled = false;
                        c.Opacity = 0;
                    }
                }
            }
            catch { }
        }

        private static DependencyObject? FindDescendantByName(DependencyObject? root, string name)
        {
            if (root == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement fe && string.Equals(fe.Name, name, StringComparison.Ordinal)) return child;
                var result = FindDescendantByName(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void SearchBox_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            // Keep clear button visibility synced for any text change
            UpdateSearchClearButtonVisibility();

            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var q = (SearchBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(q))
                {
                    // User cleared input: cancel any pending search tasks and immediately show baseline
                    try { _navTyping = false; } catch { }
                    try { _searchDebounceCts?.Cancel(); } catch { }
                    try { _typingRebindCts?.Cancel(); } catch { }
                    try { RunImmediateFilterAndBind(); } catch { }
                    try { ShowBaselineSuggestions(); } catch { }
                    UpdateNavButtons();
                    return;
                }
                _navTyping = true;
                RunAsyncSearchAndBind(q);
            }
        }

        private void ShowBaselineSuggestions()
        {
            try
            {
                if (SearchBox == null) return;
                var allowed = new HashSet<string>(_allPolicies.Select(p => p.UniqueID), StringComparer.OrdinalIgnoreCase);
                var list = BuildSuggestions(string.Empty, allowed);
                SearchBox.ItemsSource = list;
            }
            catch { }
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

        private void PolicyList_RightTapped(object sender, RightTappedRoutedEventArgs e) { }

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
                        LoadAdmxFolderAsync(admxPath);
                    }
                    // 2nd language
                    bool beforeEnabled = SettingsService.Instance.LoadSettings().SecondLanguageEnabled ?? false;
                    string beforeSecond = SettingsService.Instance.LoadSettings().SecondLanguage ?? "en-US";
                    bool afterEnabled = dlg.SecondLanguageEnabled;
                    string afterSecond = dlg.SelectedSecondLanguage ?? beforeSecond;
                    try { SettingsService.Instance.UpdateSecondLanguageEnabled(afterEnabled); } catch { }
                    if (afterEnabled && !string.IsNullOrEmpty(afterSecond))
                    {
                        try { SettingsService.Instance.UpdateSecondLanguage(afterSecond); } catch { }
                    }

                    // Apply UI changes immediately without restart
                    ApplySecondLanguageVisibilityToViewMenu();
                    UpdateColumnVisibilityFromFlags();
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
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
                            if (!res.ok)
                            { ShowInfo(".reg import failed: " + (res.error ?? "elevation error"), InfoBarSeverity.Error); return; }
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
            if (_selectedCategory != null)
                SearchBox.PlaceholderText = $"Search policies in {_selectedCategory.DisplayName}";
            else
                SearchBox.PlaceholderText = "Search policies";
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

        private void SearchOption_Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressSearchOptionEvents) return;
            try
            {
                _searchInName = SearchOptName?.IsChecked == true;
                _searchInId = SearchOptId?.IsChecked == true;
                _searchInDescription = SearchOptDesc?.IsChecked == true;
                _searchInComments = SearchOptComments?.IsChecked == true;
                _searchInRegistryKey = SearchOptRegKey?.IsChecked == true;
                _searchInRegistryValue = SearchOptRegValue?.IsChecked == true;
            }
            catch { }
            // Persist
            try
            {
                SettingsService.Instance.UpdateSearchOptions(new SearchOptions
                {
                    InName = _searchInName,
                    InId = _searchInId,
                    InRegistryKey = _searchInRegistryKey,
                    InRegistryValue = _searchInRegistryValue,
                    InDescription = _searchInDescription,
                    InComments = _searchInComments
                });
            }
            catch { }

            // Optimization: if no query text, do not reload the list
            var q = (SearchBox?.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(q))
            {
                return;
            }

            // Debounce rebind when toggling multiple options rapidly
            _searchOptionsDebounceCts?.Cancel();
            _searchOptionsDebounceCts = new CancellationTokenSource();
            var token = _searchOptionsDebounceCts.Token;
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(250, token); } catch { return; }
                if (token.IsCancellationRequested) return;
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) return;
                    RunAsyncSearchAndBind(q);
                });
            });
        }

        private void ToggleLimitUnfilteredMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ToggleMenuFlyoutItem t)
                {
                    _limitUnfilteredTo1000 = t.IsChecked;
                    SettingsService.Instance.UpdateLimitUnfilteredTo1000(_limitUnfilteredTo1000);
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
            catch { }
        }

        private void SearchOptionsFlyout_Opened(object sender, object e)
        {
            try
            {
                var so = SettingsService.Instance.LoadSettings().Search ?? new SearchOptions();
                _suppressSearchOptionEvents = true;
                if (SearchOptName != null) SearchOptName.IsChecked = so.InName;
                if (SearchOptId != null) SearchOptId.IsChecked = so.InId;
                if (SearchOptDesc != null) SearchOptDesc.IsChecked = so.InDescription;
                if (SearchOptComments != null) SearchOptComments.IsChecked = so.InComments;
                if (SearchOptRegKey != null) SearchOptRegKey.IsChecked = so.InRegistryKey;
                if (SearchOptRegValue != null) SearchOptRegValue.IsChecked = so.InRegistryValue;
            }
            finally { _suppressSearchOptionEvents = false; }
        }

        private void RebindConsideringAsync(string q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    RunAsyncFilterAndBind();
                }
                else
                {
                    RunAsyncSearchAndBind(q);
                }
            }
            catch { }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            try
            {
                _navTyping = false;
                var q = args?.QueryText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(q))
                {
                    RunImmediateFilterAndBind();
                    ShowBaselineSuggestions();
                }
                else
                {
                    RunAsyncSearchAndBind(q.Trim());
                }
                UpdateNavButtons();
            }
            catch { }
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            try
            {
                _navTyping = false;
                var chosen = args?.SelectedItem?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(chosen))
                {
                    RunImmediateFilterAndBind();
                    ShowBaselineSuggestions();
                }
                else
                {
                    RunAsyncSearchAndBind(chosen.Trim());
                }
                UpdateNavButtons();
            }
            catch { }
        }

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

        // Proxy methods call into real implementations (defined in Filtering.cs)
        partial void Filtering_RunAsyncFilterAndBindProxy();
        partial void Filtering_RunImmediateFilterAndBindProxy();

        private async void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Dialogs.AboutDialog();
                if (Content is FrameworkElement root) dlg.XamlRoot = root.XamlRoot;
                await dlg.ShowAsync();
            }
            catch { }
        }

        // (ChkBookmarksOnly_Checked handled in Commands.cs partial class)
    }
}
