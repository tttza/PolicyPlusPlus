using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using PolicyPlus; // Core namespace
using System.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.Generic;
using PolicyPlus.WinUI3.Dialogs;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Windows.System;
using Microsoft.UI.Xaml.Documents;
using PolicyPlus.WinUI3.Windows;
using Microsoft.UI.Dispatching;
using System.Threading;
using Microsoft.UI.Xaml.Media.Animation;
using PolicyPlus.WinUI3.Utils;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Models;
using System.IO;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow : Window
    {
        public event EventHandler? Saved;

        private bool _hideEmptyCategories = true;

        // State fields
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

        // Debounce category select to avoid double-tap side effects
        private CancellationTokenSource? _categorySelectCts;

        // Auto-close InfoBar cancellation
        private CancellationTokenSource? _infoBarCloseCts;

        private double? _savedVerticalOffset;

        // Persisted column prefs keys
        private const string ColIdKey = "Columns.ShowId";
        private const string ColCategoryKey = "Columns.ShowCategory";
        private const string ColAppliesKey = "Columns.ShowApplies";
        private const string ColSupportedKey = "Columns.ShowSupported";
        private const string ColUserStateKey = "Columns.ShowUserState";
        private const string ColCompStateKey = "Columns.ShowComputerState";

        private const string ScaleKey = "UIScale"; // e.g. "100%"

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

        private CheckBox? GetFlag(string name)
            => (RootGrid?.FindName(name) as CheckBox);

        private void HookPendingQueue()
        {
            try
            {
                PendingChangesWindow.ChangesAppliedOrDiscarded += (_, __) => { UpdateUnsavedIndicator(); RefreshList(); };
                PendingChangesService.Instance.Pending.CollectionChanged += (_, __) => { UpdateUnsavedIndicator(); RefreshList(); };
                UpdateUnsavedIndicator();
            }
            catch { }
        }

        private void LoadColumnPrefs()
        {
            try
            {
                if (_config == null) return;
                bool Get(string k, bool defVal) { try { return Convert.ToInt32(_config.GetValue(k, defVal ? 1 : 0)) != 0; } catch { return defVal; } }
                var id = GetFlag("ColIdFlag"); if (id != null) id.IsChecked = Get(ColIdKey, true);
                var cat = GetFlag("ColCategoryFlag"); if (cat != null) cat.IsChecked = Get(ColCategoryKey, false);
                var app = GetFlag("ColAppliesFlag"); if (app != null) app.IsChecked = Get(ColAppliesKey, false);
                var sup = GetFlag("ColSupportedFlag"); if (sup != null) sup.IsChecked = Get(ColSupportedKey, false);
                var us = GetFlag("ColUserStateFlag"); if (us != null) us.IsChecked = Get(ColUserStateKey, true);
                var cs = GetFlag("ColCompStateFlag"); if (cs != null) cs.IsChecked = Get(ColCompStateKey, true);
            }
            catch { }
        }

        private void SaveColumnPrefs()
        {
            try
            {
                if (_config == null) return;
                void Set(string k, bool v) { try { _config.SetValue(k, v ? 1 : 0); } catch { } }
                var id = GetFlag("ColIdFlag"); if (id != null) Set(ColIdKey, id.IsChecked == true);
                var cat = GetFlag("ColCategoryFlag"); if (cat != null) Set(ColCategoryKey, cat.IsChecked == true);
                var app = GetFlag("ColAppliesFlag"); if (app != null) Set(ColAppliesKey, app.IsChecked == true);
                var sup = GetFlag("ColSupportedFlag"); if (sup != null) Set(ColSupportedKey, sup.IsChecked == true);
                var us = GetFlag("ColUserStateFlag"); if (us != null) Set(ColUserStateKey, us.IsChecked == true);
                var cs = GetFlag("ColCompStateFlag"); if (cs != null) Set(ColCompStateKey, cs.IsChecked == true);
            }
            catch { }
        }

        private void UpdateColumnVisibilityFromFlags()
        {
            try
            {
                if (ColId != null) ColId.Visibility = (ColIdFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColCategory != null) ColCategory.Visibility = (ColCategoryFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColApplies != null) ColApplies.Visibility = (ColAppliesFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColSupported != null) ColSupported.Visibility = (ColSupportedFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private void ColumnToggle_Click(object sender, RoutedEventArgs e)
        {
            // Sync menu toggle -> hidden checkbox (named via Tag)
            if (sender is ToggleMenuFlyoutItem t && t.Tag is string name)
            {
                if (RootGrid?.FindName(name) is CheckBox cb)
                {
                    cb.IsChecked = t.IsChecked;
                }
            }
            SaveColumnPrefs();
            UpdateColumnVisibilityFromFlags();
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void HiddenFlag_Checked(object sender, RoutedEventArgs e)
        {
            SaveColumnPrefs();
            UpdateColumnVisibilityFromFlags();
        }

        private void RefreshList()
        {
            try
            {
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            }
            catch { }
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

        private void ShowInfo(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
        {
            if (StatusBar == null) return;

            // Cancel any pending auto-close and reset visuals
            _infoBarCloseCts?.Cancel();
            StatusBar.Opacity = 1;

            StatusBar.Title = null;
            StatusBar.Severity = severity;
            StatusBar.Message = message;
            StatusBar.IsOpen = true;

            StartInfoBarAutoClose();
        }

        private void StartInfoBarAutoClose()
        {
            _infoBarCloseCts?.Cancel();
            _infoBarCloseCts = new CancellationTokenSource();
            var token = _infoBarCloseCts.Token;

            _ = Task.Run(async () =>
            {
                try { await Task.Delay(TimeSpan.FromSeconds(3), token); }
                catch (TaskCanceledException) { return; }
                if (token.IsCancellationRequested) return;

                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        await FadeOutAndCloseInfoBarAsync();
                    }
                });
            });
        }

        private async Task FadeOutAndCloseInfoBarAsync()
        {
            if (StatusBar == null || !StatusBar.IsOpen) return;

            var anim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut }
            };
            var sb = new Storyboard();
            Storyboard.SetTarget(anim, StatusBar);
            Storyboard.SetTargetProperty(anim, "Opacity");
            sb.Children.Add(anim);

            var tcs = new TaskCompletionSource<object?>();
            sb.Completed += (s, e) => tcs.TrySetResult(null);
            sb.Begin();
            await tcs.Task;

            StatusBar.IsOpen = false;
            StatusBar.Opacity = 1;
        }

        private void HideInfo()
        {
            if (StatusBar == null) return;
            _infoBarCloseCts?.Cancel();
            StatusBar.IsOpen = false;
            StatusBar.Opacity = 1;
        }

        private void SetBusy(bool busy)
        {
            if (BusyOverlay == null) return;
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try { _config = new ConfigurationStorage(Microsoft.Win32.RegistryHive.CurrentUser, @"Software\\Policy Plus"); } catch { }
            try
            {
                _hideEmptyCategories = Convert.ToInt32(_config?.GetValue("HideEmptyCategories", 1)) != 0;
            }
            catch { _hideEmptyCategories = true; }

            // reflect menu toggle state if present
            try { ToggleHideEmptyMenu.IsChecked = _hideEmptyCategories; } catch { }

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

            // Load saved scale
            try
            {
                var scalePref = Convert.ToString(_config?.GetValue(ScaleKey, "100%")) ?? "100%";
                SetScaleFromString(scalePref, updateSelector: true, save: false);
            }
            catch { }

            // Load column visibility preferences after XAML created
            LoadColumnPrefs();
            UpdateColumnVisibilityFromFlags();

            try
            {
                string defaultPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\PolicyDefinitions");
                var lastObj = _config?.GetValue("AdmxSource", defaultPath);
                string lastPath = lastObj == null ? defaultPath : Convert.ToString(lastObj) ?? defaultPath;
                if (System.IO.Directory.Exists(lastPath))
                {
                    LoadAdmxFolderAsync(lastPath);
                }
            }
            catch { }
        }

        private void SetScaleFromString(string s, bool updateSelector, bool save)
        {
            double scale = 1.0;
            if (!string.IsNullOrEmpty(s) && s.EndsWith("%"))
            {
                if (double.TryParse(s.TrimEnd('%'), out var pct))
                {
                    scale = Math.Max(0.5, pct / 100.0);
                }
            }
            App.SetGlobalScale(scale);
            if (updateSelector)
            {
                try
                {
                    if (ScaleSelector != null)
                    {
                        var items = ScaleSelector.Items?.OfType<ComboBoxItem>()?.ToList();
                        var match = items?.FirstOrDefault(i => string.Equals(Convert.ToString(i.Content), s, StringComparison.OrdinalIgnoreCase));
                        if (match != null) ScaleSelector.SelectedItem = match;
                    }
                }
                catch { }
            }
            if (save)
            {
                try { _config?.SetValue(ScaleKey, s); } catch { }
            }
        }

        private void ScaleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var item = (ScaleSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "100%";
                SetScaleFromString(item, updateSelector: false, save: true);
            }
            catch { }
        }

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
                _totalGroupCount = _allPolicies.GroupBy(p => p.DisplayName, System.StringComparer.InvariantCultureIgnoreCase).Count();
                ApplyFiltersAndBind();
            }
            finally
            {
                SetBusy(false);
            }
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private void ApplyTheme(string pref)
        {
            if (RootGrid == null) return;
            var theme = pref switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
            RootGrid.RequestedTheme = theme;
            App.SetGlobalTheme(theme);
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = ThemeSelector as ComboBox;
            var item = ((cb?.SelectedItem as ComboBoxItem)?.Content?.ToString());
            var pref = item ?? "System";
            ApplyTheme(pref);
            try { _config?.SetValue("Theme", pref); } catch { }
        }

        private void BtnLoadLocalGpo_Click(object sender, RoutedEventArgs e)
        {
            _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            _compSource = _loader.OpenSource();
            _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
            if (LoaderInfo != null)
                LoaderInfo.Text = _loader.GetDisplayInfo();
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
                var q = (SearchBox.Text ?? string.Empty).Trim();
                // Suggestions should search recursively within the selected category
                var baseSeq = BaseSequenceForFilters(includeSubcategories: true);
                var suggestions = baseSeq.Where(p => p.DisplayName.Contains(q, StringComparison.InvariantCultureIgnoreCase))
                                         .Take(10)
                                         .Select(p => p.DisplayName)
                                         .Distinct(StringComparer.InvariantCultureIgnoreCase)
                                         .ToList();
                SearchBox.ItemsSource = suggestions;
                ApplyFiltersAndBind(q);
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        { var q = args.QueryText ?? string.Empty; ApplyFiltersAndBind(q); }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        { var chosen = args.SelectedItem?.ToString() ?? string.Empty; ApplyFiltersAndBind(chosen); }

        private void AppliesToSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = (AppliesToSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            _appliesFilter = sel switch { "Computer" => AdmxPolicySection.Machine, "User" => AdmxPolicySection.User, _ => AdmxPolicySection.Both };
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void EnsureLocalSources()
        {
            if (_loader is null || _compSource is null || _userSource is null)
            {
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                if (LoaderInfo != null)
                    LoaderInfo.Text = _loader.GetDisplayInfo();
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
            if (LoaderInfo != null)
                LoaderInfo.Text = "Temp POL (Comp/User)";
        }

        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters(bool includeSubcategories)
        {
            IEnumerable<PolicyPlusPolicy> seq = _allPolicies;
            if (_appliesFilter == AdmxPolicySection.Machine)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both);
            else if (_appliesFilter == AdmxPolicySection.User)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both);

            if (_selectedCategory is not null)
            {
                // If searching or configured-only, include subcategories; otherwise handle category view separately
                if (includeSubcategories)
                {
                    var allowed = new HashSet<string>();
                    CollectPoliciesRecursive(_selectedCategory, allowed);
                    seq = seq.Where(p => allowed.Contains(p.UniqueID));
                }
                else
                {
                    // Only policies directly under the selected category
                    var direct = new HashSet<string>(_selectedCategory.Policies.Select(p => p.UniqueID));
                    seq = seq.Where(p => direct.Contains(p.UniqueID));
                }
            }

            if (_configuredOnly)
            {
                EnsureLocalSources();
                if (_compSource != null && _userSource != null)
                {
                    seq = seq.Where(p =>
                    {
                        var comp = PolicyProcessing.GetPolicyState(_compSource, p);
                        var user = PolicyProcessing.GetPolicyState(_userSource, p);
                        return comp == PolicyState.Enabled || comp == PolicyState.Disabled || user == PolicyState.Enabled || user == PolicyState.Disabled;
                    });
                }
            }
            return seq;
        }

        private void ApplyFiltersAndBind(string query = "", PolicyPlusCategory? category = null)
        {
            if (PolicyList == null || PolicyCount == null) return;
            if (category != null) _selectedCategory = category;

            PreserveScrollPosition();

            UpdateSearchPlaceholder();

            bool searching = !string.IsNullOrWhiteSpace(query);
            bool flat = searching || _configuredOnly;
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(includeSubcategories: flat);
            if (searching)
                seq = seq.Where(p => p.DisplayName.Contains(query, StringComparison.InvariantCultureIgnoreCase) || p.UniqueID.Contains(query, StringComparison.InvariantCultureIgnoreCase));

            BindSequenceEnhanced(seq, flat);

            RestoreScrollPosition();
        }

        private void BindSequenceEnhanced(IEnumerable<PolicyPlusPolicy> seq, bool flat)
        {
            EnsureLocalSources();

            if (flat)
            {
                // Show all individual policies when searching or configured-only (flat list)
                var ordered = seq.OrderBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase)
                                 .ThenBy(p => p.UniqueID, StringComparer.InvariantCultureIgnoreCase)
                                 .ToList();
                _visiblePolicies = ordered.ToList();

                var rows = ordered.Select(p => (object)PolicyListRow.FromPolicy(p, _compSource, _userSource)).ToList();
                PolicyList.ItemsSource = rows;

                // Show count against total number of policies
                PolicyCount.Text = $"{_visiblePolicies.Count} / {_allPolicies.Count} policies";
                SetDetails(null);
                return;
            }

            // Group policies by display name (original behavior when not searching and not configured-only)
            var grouped = seq.GroupBy(p => p.DisplayName, System.StringComparer.InvariantCultureIgnoreCase);
            _nameGroups = grouped.ToDictionary(g => g.Key, g => g.ToList(), StringComparer.InvariantCultureIgnoreCase);
            var representatives = grouped.Select(PickRepresentative).OrderBy(p => p.DisplayName).ToList();

            _visiblePolicies = representatives;

            var groupRows = representatives.Select(p =>
            {
                _nameGroups.TryGetValue(p.DisplayName, out var groupList);
                groupList ??= new List<PolicyPlusPolicy> { p };
                return (object)PolicyListRow.FromGroup(p, groupList, _compSource, _userSource);
            }).ToList<object>();

            // If a category is selected and not flat, prepend its subcategories to the list view
            if (_selectedCategory != null && !_configuredOnly)
            {
                var items = new List<object>();
                var children = _selectedCategory.Children
                    .Where(c => !_hideEmptyCategories || HasAnyVisiblePolicyInCategory(c))
                    .OrderBy(c => c.DisplayName)
                    .Select(c => (object)PolicyListRow.FromCategory(c))
                    .ToList();
                items.AddRange(children);
                items.AddRange(groupRows);
                PolicyList.ItemsSource = items;
            }
            else
            {
                PolicyList.ItemsSource = groupRows;
            }

            PolicyCount.Text = $"{_visiblePolicies.Count} / {_totalGroupCount} policies";
            SetDetails(null);
        }

        private PolicyPlusPolicy PickRepresentative(IGrouping<string, PolicyPlusPolicy> g)
        {
            var list = g.ToList();
            var both = list.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Both);
            if (both != null) return both;
            var comp = list.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Machine);
            return comp ?? list[0];
        }

        private void CollectPoliciesRecursive(PolicyPlusCategory cat, HashSet<string> sink)
        { foreach (var p in cat.Policies) sink.Add(p.UniqueID); foreach (var child in cat.Children) CollectPoliciesRecursive(child, sink); }

        private void PolicyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { var row = PolicyList?.SelectedItem; var p = (row as PolicyListRow)?.Policy ?? row as PolicyPlusPolicy; SetDetails(p); }

        private void SetDetails(PolicyPlusPolicy? p)
        {
            if (DetailTitle == null) return;
            if (p is null)
            {
                DetailTitle.Blocks.Clear(); DetailId.Blocks.Clear(); DetailCategory.Blocks.Clear(); DetailApplies.Blocks.Clear(); DetailSupported.Blocks.Clear();
                if (DetailExplain != null) DetailExplain.Blocks.Clear();
                return;
            }
            SetPlainText(DetailTitle, p.DisplayName);
            SetPlainText(DetailId, p.UniqueID);
            SetPlainText(DetailCategory, p.Category is null ? string.Empty : $"Category: {p.Category.DisplayName}");
            var applies = p.RawPolicy.Section switch { AdmxPolicySection.Machine => "Computer", AdmxPolicySection.User => "User", _ => "Both" };
            SetPlainText(DetailApplies, $"Applies to: {applies}");
            SetPlainText(DetailSupported, p.SupportedOn is null ? string.Empty : $"Supported on: {p.SupportedOn.DisplayName}");
            SetExplanationText(p.DisplayExplanation ?? string.Empty);
        }

        private static void SetPlainText(RichTextBlock rtb, string text)
        {
            rtb.Blocks.Clear();
            var p = new Paragraph();
            p.Inlines.Add(new Run { Text = text ?? string.Empty });
            rtb.Blocks.Add(p);
        }

        private static bool IsInsideDoubleQuotes(string s, int index)
        {
            bool inQuote = false;
            int i = 0;
            while (i < index)
            {
                if (s[i] == '"')
                {
                    int bs = 0; int j = i - 1; while (j >= 0 && s[j] == '\\') { bs++; j--; }
                    if ((bs % 2) == 0) inQuote = !inQuote;
                }
                i++;
            }
            return inQuote;
        }

        private void SetExplanationText(string text)
        {
            if (DetailExplain == null) return;
            DetailExplain.Blocks.Clear();
            var para = new Paragraph();
            if (string.IsNullOrEmpty(text)) { DetailExplain.Blocks.Add(para); return; }

            int lastIndex = 0;
            foreach (Match m in UrlRegex.Matches(text))
            {
                if (IsInsideDoubleQuotes(text, m.Index))
                    continue;

                if (m.Index > lastIndex)
                {
                    var before = text.Substring(lastIndex, m.Index - lastIndex);
                    para.Inlines.Add(new Run { Text = before });
                }

                string url = m.Value;
                var link = new Hyperlink();
                link.Inlines.Add(new Run { Text = url });
                link.Click += async (s, e) =>
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        try { await Launcher.LaunchUriAsync(uri); } catch { }
                    }
                };
                para.Inlines.Add(link);
                lastIndex = m.Index + m.Length;
            }
            if (lastIndex < text.Length)
            {
                para.Inlines.Add(new Run { Text = text.Substring(lastIndex) });
            }
            DetailExplain.Blocks.Add(para);
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

        private async Task OpenEditDialogForPolicyAsync(PolicyPlusPolicy representative, bool ensureFront = false)
        {
            if (_bundle is null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            { _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false); _compSource = _loader.OpenSource(); _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(); }

            var displayName = representative.DisplayName;
            _nameGroups.TryGetValue(displayName, out var groupList);
            groupList ??= _allPolicies.Where(p => string.Equals(p.DisplayName, displayName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            PolicyPlusPolicy targetPolicy = groupList.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.User)
                                        ?? groupList.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Machine)
                                        ?? representative;

            var initialSection = targetPolicy.RawPolicy.Section == AdmxPolicySection.Both
                ? AdmxPolicySection.User
                : targetPolicy.RawPolicy.Section;

            if (App.TryActivateExistingEdit(targetPolicy.UniqueID))
                return;

            var compLoader = _useTempPol && _tempPolCompPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolCompPath, false)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            var userLoader = _useTempPol && _tempPolUserPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolUserPath, true)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true);

            var win = new EditSettingWindow();
            win.Initialize(targetPolicy,
                initialSection,
                _bundle!, _compSource!, _userSource!,
                compLoader, userLoader,
                new Dictionary<string, string>(), new Dictionary<string, string>());
            win.Saved += (s, e) => MarkDirty();
            win.Activate();
            WindowHelpers.BringToFront(win);

            if (ensureFront)
            {
                await Task.Delay(150);
                try { WindowHelpers.BringToFront(win); } catch { }
            }
        }

        public async Task OpenEditDialogForPolicyIdAsync(string policyId, bool ensureFront)
        {
            if (_bundle == null) return;
            PolicyPlusPolicy? representative = _allPolicies.FirstOrDefault(p => p.UniqueID == policyId);
            if (representative == null)
            {
                if (!_bundle.Policies.TryGetValue(policyId, out var fromBundle)) return;
                representative = fromBundle;
            }
            await OpenEditDialogForPolicyAsync(representative, ensureFront);
        }

        public async Task OpenEditDialogForPolicyIdAsync(string policyId, AdmxPolicySection preferredSection, bool ensureFront)
        {
            if (_bundle == null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            { _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false); _compSource = _loader.OpenSource(); _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(); }

            if (!_bundle.Policies.TryGetValue(policyId, out var policy))
            {
                policy = _allPolicies.FirstOrDefault(p => p.UniqueID == policyId);
                if (policy == null) return;
            }

            var compLoader = _useTempPol && _tempPolCompPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolCompPath, false)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            var userLoader = _useTempPol && _tempPolUserPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolUserPath, true)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true);

            var win = new EditSettingWindow();
            win.Initialize(policy, preferredSection, _bundle!, _compSource!, _userSource!, compLoader, userLoader, new Dictionary<string, string>(), new Dictionary<string, string>());
            win.Saved += (s, e) => MarkDirty();
            win.Activate();
            WindowHelpers.BringToFront(win);
            if (ensureFront)
            {
                await Task.Delay(150);
                try { WindowHelpers.BringToFront(win); } catch { }
            }
        }

        private void MarkDirty()
        {
            UpdateUnsavedIndicator();
        }

        private PolicyPlusPolicy? GetContextMenuPolicy(object sender)
        {
            // Items in the list now bind ContextFlyout Tag to Policy if available
            if (sender is FrameworkElement fe)
            {
                if (fe.Tag is PolicyPlusPolicy p1) return p1;
                if (fe.DataContext is PolicyListRow row && row.Policy != null) return row.Policy;
            }
            return null;
        }

        private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            BtnSave_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private void PolicyList_RightTapped(object sender, RightTappedRoutedEventArgs e) { }

        private void BtnViewFormatted_Click(object sender, RoutedEventArgs e)
        {
            var row = PolicyList?.SelectedItem as PolicyListRow; var p = row?.Policy; if (p is null || _bundle is null) return;
            var win = new Windows.DetailPolicyFormattedWindow();
            win.Initialize(p, _bundle, _compSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource(), _userSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(), p.RawPolicy.Section);
            win.Activate();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty; _selectedCategory = null; _configuredOnly = false; if (ChkConfiguredOnly != null) ChkConfiguredOnly.IsChecked = false; UpdateSearchPlaceholder(); ApplyFiltersAndBind();
        }

        private void ChkConfiguredOnly_Checked(object sender, RoutedEventArgs e)
        { _configuredOnly = ChkConfiguredOnly?.IsChecked == true; ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty); }

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
        private async Task<(bool ok, string? error)> SavePendingAsync(PendingChange[] items)
        {
            if (items == null || items.Length == 0) return (true, null);
            if (_bundle == null) return (false, "No ADMX bundle loaded");

            bool wroteOk = true; string? writeErr = null;
            await Task.Run(async () =>
            {
                PolFile? compPolBuffer = null;
                PolFile? userPolBuffer = null;
                try { compPolBuffer = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource() as PolFile; } catch { compPolBuffer = new PolFile(); }
                try { userPolBuffer = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource() as PolFile; } catch { userPolBuffer = new PolFile(); }

                foreach (var c in items)
                {
                    if (string.IsNullOrEmpty(c.PolicyId)) continue;
                    if (!_bundle.Policies.TryGetValue(c.PolicyId, out var pol)) continue;
                    var target = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? (IPolicySource?)userPolBuffer : (IPolicySource?)compPolBuffer;
                    if (target == null) continue;
                    PolicyProcessing.ForgetPolicy(target, pol);
                    if (c.DesiredState == PolicyState.Enabled)
                        PolicyProcessing.SetPolicyState(target, pol, PolicyState.Enabled, c.Options ?? new Dictionary<string, object>());
                    else if (c.DesiredState == PolicyState.Disabled)
                        PolicyProcessing.SetPolicyState(target, pol, PolicyState.Disabled, new Dictionary<string, object>());
                }

                try
                {
                    string? compBase64 = null;
                    string? userBase64 = null;

                    if (compPolBuffer != null)
                    {
                        using var ms = new MemoryStream();
                        using (var bw = new BinaryWriter(ms, System.Text.Encoding.Unicode, true)) { compPolBuffer.Save(bw); }
                        compBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                    if (userPolBuffer != null)
                    {
                        using var ms2 = new MemoryStream();
                        using (var bw2 = new BinaryWriter(ms2, System.Text.Encoding.Unicode, true)) { userPolBuffer.Save(bw2); }
                        userBase64 = Convert.ToBase64String(ms2.ToArray());
                    }

                    var res = await ElevationService.Instance.WriteLocalGpoBytesAsync(compBase64, userBase64, triggerRefresh: true).ConfigureAwait(false);
                    if (!res.ok) { wroteOk = false; writeErr = res.error; }
                }
                catch (Exception ex) { wroteOk = false; writeErr = ex.Message; }
            });

            return (wroteOk, writeErr);
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pending = PendingChangesService.Instance.Pending.ToArray();
                if (pending.Length > 0)
                {
                    SetBusy(true);
                    var (ok, err) = await SavePendingAsync(pending);
                    SetBusy(false);

                    if (ok)
                    {
                        PendingChangesService.Instance.Applied(pending);
                        RefreshLocalSources();
                        UpdateUnsavedIndicator();
                        ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                        ShowInfo("Saved.", InfoBarSeverity.Success);
                        try { Saved?.Invoke(this, EventArgs.Empty); } catch { }
                    }
                    else
                    {
                        ShowInfo("Save failed: " + (err ?? "unknown"), InfoBarSeverity.Error);
                    }
                }
            }
            catch
            {
                SetBusy(false);
            }
        }

        private void ContextViewFormatted_Click(object sender, RoutedEventArgs e) { BtnViewFormatted_Click(sender, e); }
        private void ContextCopyName_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyListRow)?.Policy; if (p!=null) { var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(p.DisplayName); Clipboard.SetContent(dp);} }
        private void ContextCopyId_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyListRow)?.Policy; if (p!=null) { var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(p.UniqueID); Clipboard.SetContent(dp);} }
        private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyListRow)?.Policy; if (p==null) return; var sb=new StringBuilder(); var c=p.Category; var stack=new Stack<string>(); while(c!=null){stack.Push(c.DisplayName); c=c.Parent;} sb.AppendLine("Administrative Templates"); foreach(var name in stack) sb.AppendLine("+ "+name); sb.AppendLine("+ "+p.DisplayName); var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(sb.ToString()); Clipboard.SetContent(dp); }
        private void ContextRevealInTree_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyListRow)?.Policy; if (p==null||CategoryTree==null) return; _selectedCategory=p.Category; SelectCategoryInTree(_selectedCategory); UpdateSearchPlaceholder(); ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty); }
        private void ContextCopyRegPaths_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyListRow)?.Policy; if (p==null) return; var list=PolicyProcessing.GetReferencedRegistryValues(p).Select(kv=>$"{kv.Key} [{kv.Value}]"); var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(string.Join("\r\n",list)); Clipboard.SetContent(dp); }
        private void ContextCopyRegExport_Click(object sender, RoutedEventArgs e)
        { ShowInfo("Copy .reg export not implemented in this build."); }

        private void ClearCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            _selectedCategory = null;
            _categorySelectCts?.Cancel();
            if (CategoryTree != null)
            {
                _suppressCategorySelectionChanged = true;
                var old = CategoryTree.SelectionMode;
                CategoryTree.SelectionMode = Microsoft.UI.Xaml.Controls.TreeViewSelectionMode.None;
                BuildCategoryTree();
                CategoryTree.SelectionMode = old; // clears any selection
                _suppressCategorySelectionChanged = false;
            }
            UpdateSearchPlaceholder();
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void CategoryTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            var cat = args.InvokedItem as PolicyPlusCategory;
            if (cat == null) return;
            _selectedCategory = cat;
            UpdateSearchPlaceholder();
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            SelectCategoryInTree(cat);
        }

        private void CategoryTree_SelectionChanged(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewSelectionChangedEventArgs args)
        {
            if (_suppressCategorySelectionChanged) return;
            if (sender.SelectedNodes != null && sender.SelectedNodes.Count > 0)
            {
                var cat = sender.SelectedNodes.FirstOrDefault()?.Content as PolicyPlusCategory;
                ScheduleApplyCategory(cat);
            }
            // If no selected nodes, keep current _selectedCategory (don't clear filter)
        }

        private void CategoryTree_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (CategoryTree == null) return;
            // Cancel any pending category apply since this is a double-tap
            _categorySelectCts?.Cancel();

            // Resolve the bound item in the simplest and most reliable way
            object? item = (e.OriginalSource as FrameworkElement)?.DataContext ?? PolicyList?.SelectedItem;

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
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                SelectCategoryInTree(cat);
            }
        }

        private void ScheduleApplyCategory(PolicyPlusCategory? cat)
        {
            if (cat == null) return;
            _categorySelectCts?.Cancel();
            var cts = new CancellationTokenSource();
            _categorySelectCts = cts;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(250, cts.Token);
                    if (cts.IsCancellationRequested) return;
                    _ = DispatcherQueue.TryEnqueue(() =>
                    {
                        if (!cts.IsCancellationRequested)
                        {
                            _selectedCategory = cat;
                            UpdateSearchPlaceholder();
                            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                        }
                    });
                }
                catch (TaskCanceledException) { }
            });
        }

        private void ToggleTempPolMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                ChkUseTempPol.IsChecked = t.IsChecked;
                ChkUseTempPol_Checked(ChkUseTempPol, new RoutedEventArgs());
            }
        }

        private void BuildCategoryTree()
        {
            if (CategoryTree == null || _bundle == null) return;
            _suppressCategorySelectionChanged = true;
            try
            {
                var oldMode = CategoryTree.SelectionMode;
                CategoryTree.SelectionMode = Microsoft.UI.Xaml.Controls.TreeViewSelectionMode.None;

                CategoryTree.RootNodes.Clear();
                foreach (var kv in _bundle.Categories.OrderBy(k => k.Value.DisplayName))
                {
                    var cat = kv.Value;
                    if (_hideEmptyCategories && IsCategoryEmpty(cat))
                        continue;
                    var node = new Microsoft.UI.Xaml.Controls.TreeViewNode() { Content = cat, IsExpanded = false };
                    BuildChildCategoryNodes(node, cat);
                    if (node.Children.Count > 0 || !_hideEmptyCategories)
                        CategoryTree.RootNodes.Add(node);
                }

                CategoryTree.SelectionMode = oldMode;

                if (_selectedCategory != null)
                {
                    SelectCategoryInTree(_selectedCategory);
                }
            }
            finally
            {
                _suppressCategorySelectionChanged = false;
            }
        }

        private void BuildChildCategoryNodes(Microsoft.UI.Xaml.Controls.TreeViewNode parentNode, PolicyPlusCategory parentCat)
        {
            foreach (var child in parentCat.Children.OrderBy(c => c.DisplayName))
            {
                if (_hideEmptyCategories && IsCategoryEmpty(child))
                    continue;
                var node = new Microsoft.UI.Xaml.Controls.TreeViewNode() { Content = child };
                parentNode.Children.Add(node);
                if (child.Children.Count > 0)
                    BuildChildCategoryNodes(node, child);
            }
        }

        private void SelectCategoryInTree(PolicyPlusCategory? category)
        {
            if (CategoryTree == null || category == null) return;
            _suppressCategorySelectionChanged = true;
            try
            {
                // Find the TreeViewNode for the category anywhere in the tree
                Microsoft.UI.Xaml.Controls.TreeViewNode? target = FindNodeByCategory(CategoryTree.RootNodes, category.UniqueID);
                if (target == null) return;

                // Expand parents so the node is realized
                var parent = target.Parent;
                while (parent != null)
                {
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }

                // Select the node directly
                CategoryTree.SelectedNode = target;

                // Bring into view (after layout if needed)
                CategoryTree.UpdateLayout();
                var container = CategoryTree.ContainerFromNode(target) as TreeViewItem;
                if (container != null)
                {
                    container.StartBringIntoView();
                }
                else
                {
                    _ = DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100);
                        var c2 = CategoryTree.ContainerFromNode(target) as TreeViewItem;
                        c2?.StartBringIntoView();
                    });
                }
            }
            finally
            {
                _suppressCategorySelectionChanged = false;
            }
        }

        private Microsoft.UI.Xaml.Controls.TreeViewNode? FindNodeByCategory(System.Collections.Generic.IList<Microsoft.UI.Xaml.Controls.TreeViewNode> nodes, string uniqueId)
        {
            foreach (var n in nodes)
            {
                if (n.Content is PolicyPlusCategory pc && string.Equals(pc.UniqueID, uniqueId, StringComparison.InvariantCultureIgnoreCase))
                    return n;
                var child = FindNodeByCategory(n.Children, uniqueId);
                if (child != null) return child;
            }
            return null;
        }

        private bool IsCategoryEmpty(PolicyPlusCategory cat)
        {
            if (cat.Policies.Count > 0)
                return false;
            foreach (var child in cat.Children)
            {
                if (!IsCategoryEmpty(child))
                    return false;
            }
            return true;
        }

        private bool HasAnyVisiblePolicyInCategory(PolicyPlusCategory cat)
        {
            try
            {
                // When not using Configured Only, fall back to simple emptiness check
                if (!_configuredOnly)
                {
                    return !IsCategoryEmpty(cat);
                }

                // Collect all policies within this category subtree
                var ids = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                CollectPoliciesRecursive(cat, ids);
                IEnumerable<PolicyPlusPolicy> seq = _allPolicies.Where(p => ids.Contains(p.UniqueID));

                // Respect the Applies filter
                if (_appliesFilter == AdmxPolicySection.Machine)
                    seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both);
                else if (_appliesFilter == AdmxPolicySection.User)
                    seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both);

                if (!seq.Any()) return false;

                EnsureLocalSources();
                if (_compSource == null || _userSource == null) return false;

                foreach (var p in seq)
                {
                    var comp = PolicyProcessing.GetPolicyState(_compSource, p);
                    var user = PolicyProcessing.GetPolicyState(_userSource, p);
                    if (comp == PolicyState.Enabled || comp == PolicyState.Disabled || user == PolicyState.Enabled || user == PolicyState.Disabled)
                        return true;
                }
                return false;
            }
            catch
            {
                // In case of any unexpected error, do not hide the category
                return true;
            }
        }

        private void BtnPendingChanges_Click(object sender, RoutedEventArgs e)
        {
            var win = new PendingChangesWindow();
            win.Activate();
            try { WindowHelpers.BringToFront(win); } catch { }
        }

        private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                if (PolicyList?.SelectedItem is PolicyListRow row && row.Policy != null)
                {
                    e.Handled = true;
                    await OpenEditDialogForPolicyAsync(row.Policy, ensureFront: true);
                }
                else if (PolicyList?.SelectedItem is PolicyListRow row2 && row2.Category != null)
                {
                    e.Handled = true;
                    _selectedCategory = row2.Category;
                    UpdateSearchPlaceholder();
                    ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                }
            }
        }

        private void PolicyList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // Resolve the bound item in the simplest and most reliable way
            object? item = (e.OriginalSource as FrameworkElement)?.DataContext ?? PolicyList?.SelectedItem;

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
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                SelectCategoryInTree(cat);
            }
        }

        private void UpdateSearchPlaceholder()
        {
            if (SearchBox == null)
                return;
            if (_selectedCategory != null)
                SearchBox.PlaceholderText = $"Search policies (name, id) in {_selectedCategory.DisplayName}";
            else
                SearchBox.PlaceholderText = "Search policies (name, id)";
        }

        private void PolicyList_ItemClick(object sender, ItemClickEventArgs e)
        {
            // no-op: switched to double-click only
        }

        private async void BtnEditSelected_Click(object sender, RoutedEventArgs e)
        {
            if (PolicyList?.SelectedItem is PolicyListRow row && row.Policy != null)
            {
                await OpenEditDialogForPolicyAsync(row.Policy, ensureFront: false);
            }
        }

        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyListRow)?.Policy;
            if (p != null) await OpenEditDialogForPolicyAsync(p, ensureFront: false);
        }

        private void ToggleHideEmptyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                _hideEmptyCategories = t.IsChecked;
                try { _config?.SetValue("HideEmptyCategories", _hideEmptyCategories ? 1 : 0); } catch { }
                BuildCategoryTree();
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            }
        }

        private void PreserveScrollPosition()
        {
            try
            {
                if (PolicyList == null) return;
                var scroll = FindDescendantScrollViewer(PolicyList);
                _savedVerticalOffset = scroll?.VerticalOffset;
            }
            catch { }
        }

        private void RestoreScrollPosition()
        {
            try
            {
                if (PolicyList == null) return;
                var scroll = FindDescendantScrollViewer(PolicyList);
                if (scroll != null && _savedVerticalOffset.HasValue)
                {
                    scroll.ChangeView(null, _savedVerticalOffset.Value, null, true);
                }
            }
            catch { }
        }

        private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
        {
            if (root == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer sv) return sv;
                var found = FindDescendantScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

        public void RefreshLocalSources()
        {
            try
            {
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                if (LoaderInfo != null)
                    LoaderInfo.Text = _loader.GetDisplayInfo();
            }
            catch { }
        }
    }
}
