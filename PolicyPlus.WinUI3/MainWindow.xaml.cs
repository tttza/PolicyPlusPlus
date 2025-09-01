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
using PolicyPlus.WinUI3.Dialogs;
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
using PolicyPlus; // Core APIs

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow : Window
    {
        public event EventHandler? Saved;

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
        private ConfigurationStorage? _config;
        private IPolicySource? _compSource;
        private IPolicySource? _userSource;
        private bool _configuredOnly = false;

        // Temp .pol mode and save tracking
        private string? _tempPolCompPath = null;
        private string? _tempPolUserPath = null;
        private bool _useTempPol;

        private static readonly System.Text.RegularExpressions.Regex UrlRegex = new(@"(https?://[^\s]+)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Prevents SelectionChanged from re-applying category during programmatic tree updates
        private bool _suppressCategorySelectionChanged;
        private bool _suppressAppliesToSelectionChanged;
        private bool _suppressConfiguredOnlyChanged;

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
        private const string ColUserStateKey = "Columns.ShowUserState";
        private const string ColCompStateKey = "Columns.ShowComputerState";

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

        public MainWindow()
        {
            this.InitializeComponent();
            HookPendingQueue();

            // attach scaling for main window content when layout is ready
            RootGrid.Loaded += (s, e) =>
            {
                try { ScaleHelper.Attach(this, ScaleHost, RootGrid); } catch { }
            };
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
                PendingChangesWindow.ChangesAppliedOrDiscarded += (_, __) => { UpdateUnsavedIndicator(); RefreshList(); };
                PendingChangesService.Instance.Pending.CollectionChanged += (_, __) => { UpdateUnsavedIndicator(); RefreshVisibleRows(); };
                UpdateUnsavedIndicator();
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
                    DispatcherQueue.TryEnqueue(() => ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty));
                });
            }
            catch { }
        }

        private void ColumnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t && t.Tag is string name)
            {
                if (RootGrid?.FindName(name) is CheckBox cb)
                    cb.IsChecked = t.IsChecked;
            }
            SaveColumnPrefs();
            UpdateColumnVisibilityFromFlags();
            _navTyping = false;
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            UpdateNavButtons();
        }

        private void HiddenFlag_Checked(object sender, RoutedEventArgs e)
        {
            SaveColumnPrefs();
            UpdateColumnVisibilityFromFlags();
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
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try { _config = new ConfigurationStorage(Microsoft.Win32.RegistryHive.CurrentUser, @"Software\\Policy Plus"); } catch { }
            try { _hideEmptyCategories = Convert.ToInt32(_config?.GetValue("HideEmptyCategories", 1)) != 0; } catch { _hideEmptyCategories = true; }
            try { ToggleHideEmptyMenu.IsChecked = _hideEmptyCategories; } catch { }

            try { _showDetails = Convert.ToInt32(_config?.GetValue(ShowDetailsKey, 1)) != 0; } catch { _showDetails = true; }
            try { ViewDetailsToggle.IsChecked = _showDetails; } catch { }
            ApplyDetailsPaneVisibility();

            try
            {
                var themePref = Convert.ToString(_config?.GetValue("Theme", "System")) ?? "System";
                ApplyTheme(themePref);
                App.SetGlobalTheme(themePref switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default });
                var itemsObj = ThemeSelector?.Items;
                if (itemsObj != null)
                {
                    var items = itemsObj.OfType<ComboBoxItem>().ToList();
                    var match = items.FirstOrDefault(i => string.Equals(Convert.ToString(i.Content), themePref, StringComparison.OrdinalIgnoreCase));
                    if (match != null) ThemeSelector!.SelectedItem = match;
                }
            }
            catch { }

            try
            {
                var scalePref = Convert.ToString(_config?.GetValue(ScaleKey, "100%")) ?? "100%";
                SetScaleFromString(scalePref, updateSelector: true, save: false);
            }
            catch { }

            LoadColumnPrefs();
            UpdateColumnVisibilityFromFlags();

            HookDoubleTapHandlers();

            try
            {
                string defaultPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\\PolicyDefinitions");
                var lastObj = _config?.GetValue("AdmxSource", defaultPath);
                string lastPath = lastObj == null ? defaultPath : Convert.ToString(lastObj) ?? defaultPath;
                if (Directory.Exists(lastPath))
                {
                    LoadAdmxFolderAsync(lastPath);
                }
            }
            catch { }

            // Initialize navigation service/buttons
            try { InitNavigation(); } catch { }
        }

        // preference helper implementations are defined in MainWindow.Preferences.cs

        private async void LoadAdmxFolderAsync(string path)
        {
            SetBusy(true);
            try
            {
                _bundle = new AdmxBundle();
                var langPref = Convert.ToString(_config?.GetValue("UICulture", System.Globalization.CultureInfo.CurrentUICulture.Name)) ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
                var fails = _bundle.LoadFolder(path, langPref);
                if (fails.Any())
                    ShowInfo($"ADMX load completed with {fails.Count()} issue(s).", InfoBarSeverity.Warning);
                else
                    ShowInfo($"ADMX loaded ({langPref}).");
                BuildCategoryTree();
                _allPolicies = _bundle.Policies.Values.ToList();
                _totalGroupCount = _allPolicies.GroupBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase).Count();
                ApplyFiltersAndBind();
            }
            finally { SetBusy(false); }
            await Task.CompletedTask;
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
            try { _config?.SetValue("AdmxSource", folder.Path); } catch { }
            LoadAdmxFolderAsync(folder.Path);
        }

        private void SearchBox_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                _navTyping = true;
                var q = (SearchBox.Text ?? string.Empty).Trim();
                var baseSeq = BaseSequenceForFilters(includeSubcategories: true);
                var suggestions = baseSeq.Where(p => p.DisplayName.Contains(q, StringComparison.InvariantCultureIgnoreCase))
                                         .Take(10)
                                         .Select(p => p.DisplayName)
                                         .Distinct(StringComparer.InvariantCultureIgnoreCase)
                                         .ToList();
                SearchBox.ItemsSource = suggestions;
                ApplyFiltersAndBind(q);
                UpdateNavButtons();
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        { _navTyping = false; var q = args.QueryText ?? string.Empty; ApplyFiltersAndBind(q); UpdateNavButtons(); }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        { _navTyping = false; var chosen = args.SelectedItem?.ToString() ?? string.Empty; ApplyFiltersAndBind(chosen); UpdateNavButtons(); }

        private void AppliesToSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAppliesToSelectionChanged) return;
            var sel = (AppliesToSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            _appliesFilter = sel switch { "Computer" => AdmxPolicySection.Machine, "User" => AdmxPolicySection.User, _ => AdmxPolicySection.Both };
            _navTyping = false;
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
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
        }

        private void EnsureLocalSourcesUsingTemp()
        {
            if (!_useTempPol)
            {
                EnsureLocalSources();
                return;
            }
            var compLoader = new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolCompPath ?? string.Empty, false);
            var userLoader = new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolUserPath ?? string.Empty, true);
            _compSource = compLoader.OpenSource();
            _userSource = userLoader.OpenSource();
            _loader = compLoader;
            if (LoaderInfo != null) LoaderInfo.Text = "Temp POL (Comp/User)";
        }

        private void PolicyList_RightTapped(object sender, RightTappedRoutedEventArgs e) { }

        private void BtnViewFormatted_Click(object sender, RoutedEventArgs e)
        {
            var row = PolicyList?.SelectedItem as PolicyListRow; var p = row?.Policy; if (p is null || _bundle is null) return;
            var win = new DetailPolicyFormattedWindow();
            win.Initialize(p, _bundle, _compSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource(), _userSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(), p.RawPolicy.Section);
            win.Activate();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _navTyping = false;
            SearchBox.Text = string.Empty; _selectedCategory = null; _configuredOnly = false; if (ChkConfiguredOnly != null) { _suppressConfiguredOnlyChanged = true; ChkConfiguredOnly.IsChecked = false; _suppressConfiguredOnlyChanged = false; } UpdateSearchPlaceholder(); ApplyFiltersAndBind();
            UpdateNavButtons();
        }

        private void ChkConfiguredOnly_Checked(object sender, RoutedEventArgs e)
        { if (_suppressConfiguredOnlyChanged) return; _configuredOnly = ChkConfiguredOnly?.IsChecked == true; _navTyping = false; ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty); UpdateNavButtons(); }

        private void ChkUseTempPol_Checked(object sender, RoutedEventArgs e)
        { _useTempPol = ChkUseTempPol?.IsChecked == true; EnsureLocalSourcesUsingTemp(); ShowInfo(_useTempPol ? "Using temp .pol" : "Using Local GPO"); }

        private void BtnLanguage_Click(object sender, RoutedEventArgs e)
        { ShowInfo("Language dialog not implemented in this build."); }

        private void BtnExportReg_Click(object sender, RoutedEventArgs e) { ShowInfo("Export .reg not implemented in this build."); }
        private void BtnExportCsv_Click(object sender, RoutedEventArgs e) { ShowInfo("Export CSV not implemented in this build."); }
        private void BtnImportPol_Click(object sender, RoutedEventArgs e) { ShowInfo("Import .pol not implemented in this build."); }
        private void BtnImportReg_Click(object sender, RoutedEventArgs e) { ShowInfo("Import .reg not implemented in this build."); }
        private void BtnFind_Click(object sender, RoutedEventArgs e) { SearchBox.Focus(FocusState.Programmatic); }
        private void BtnFindReg_Click(object sender, RoutedEventArgs e) { SearchBox.Focus(FocusState.Programmatic); }
        private void BtnFindId_Click(object sender, RoutedEventArgs e) { SearchBox.Focus(FocusState.Programmatic); }

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
            _navTyping = false;
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            UpdateNavButtons();
        }

        private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                if (PolicyList?.SelectedItem is PolicyListRow row and { Policy: not null })
                {
                    e.Handled = true;
                    await OpenEditDialogForPolicyAsync(row.Policy, ensureFront: true);
                }
                else if (PolicyList?.SelectedItem is PolicyListRow row2 and { Category: not null })
                {
                    e.Handled = true;
                    _selectedCategory = row2.Category;
                    UpdateSearchPlaceholder();
                    _navTyping = false;
                    ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                    UpdateNavButtons();
                }
            }
        }

        private void PolicyList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            DependencyObject? dep = e.OriginalSource as DependencyObject;
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
                _ = OpenEditDialogForPolicyAsync(pol, ensureFront: true);
                return;
            }
            if (item is PolicyListRow row2 && row2.Category is PolicyPlusCategory cat)
            {
                e.Handled = true;
                _selectedCategory = cat;
                UpdateSearchPlaceholder();
                _navTyping = false;
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
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

        private void UpdateSearchPlaceholder()
        {
            if (SearchBox == null) return;
            if (_selectedCategory != null)
                SearchBox.PlaceholderText = $"Search policies (name, id) in {_selectedCategory.DisplayName}";
            else
                SearchBox.PlaceholderText = "Search policies (name, id)";
        }

        private void PolicyList_ItemClick(object sender, ItemClickEventArgs e) { }

        private async void BtnEditSelected_Click(object sender, RoutedEventArgs e)
        {
            if (PolicyList?.SelectedItem is PolicyListRow row && row.Policy != null)
            {
                await OpenEditDialogForPolicyAsync(row.Policy, ensureFront: false);
            }
        }

        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            var p = (sender as FrameworkElement)?.Tag as PolicyPlusPolicy ?? (PolicyList?.SelectedItem as PolicyListRow)?.Policy;
            if (p != null) await OpenEditDialogForPolicyAsync(p, ensureFront: false);
        }

        private void ViewDetailsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                _showDetails = t.IsChecked;
                try { _config?.SetValue(ShowDetailsKey, _showDetails ? 1 : 0); } catch { }
                ApplyDetailsPaneVisibility();
            }
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
        }

        private void RefreshList()
        {
            try { ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty); } catch { }
        }

        private async Task<AdmxPolicySection?> PromptSectionChoiceAsync()
        {
            var dlg = new ContentDialog
            {
                Title = "Choose target",
                Content = new TextBlock { Text = "Edit Computer or User configuration?" },
                PrimaryButtonText = "Computer",
                SecondaryButtonText = "User",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };
            var xr = RootGrid?.XamlRoot ?? (this.Content as FrameworkElement)?.XamlRoot;
            if (xr != null) dlg.XamlRoot = xr;
            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.Primary) return AdmxPolicySection.Machine;
            if (res == ContentDialogResult.Secondary) return AdmxPolicySection.User;
            return null;
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
    }
}
