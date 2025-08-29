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
using Microsoft.UI.Xaml.Controls; // TreeView
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

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow : Window
    {
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
        private bool _pendingChanges;

        // Temp .pol mode and save tracking
        private string? _tempPolCompPath;
        private string? _tempPolUserPath;
        private bool _useTempPol;

        private static readonly System.Text.RegularExpressions.Regex UrlRegex = new(@"(https?://[^\s]+)", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Prevents SelectionChanged from re-applying category during programmatic tree updates
        private bool _suppressCategorySelectionChanged;

        // Debounce category select to avoid double-tap side effects
        private CancellationTokenSource? _categorySelectCts;

        // Auto-close InfoBar cancellation
        private CancellationTokenSource? _infoBarCloseCts;

        public MainWindow()
        {
            this.InitializeComponent();
            HookPendingQueue();
        }

        private void HookPendingQueue()
        {
            try
            {
                PendingChangesWindow.ChangesAppliedOrDiscarded += (_, __) => UpdateUnsavedIndicator();
                PendingChangesService.Instance.Pending.CollectionChanged += (_, __) => UpdateUnsavedIndicator();
                UpdateUnsavedIndicator();
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
            try { _config = new ConfigurationStorage(Microsoft.Win32.RegistryHive.CurrentUser, @"Software\Policy Plus"); } catch { }
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
                var items = ThemeSelector?.Items?.OfType<ComboBoxItem>().ToList();
                if (items != null)
                {
                    var match = items.FirstOrDefault(i => string.Equals(Convert.ToString(i.Content), themePref, StringComparison.OrdinalIgnoreCase));
                    if (match != null) ThemeSelector.SelectedItem = match;
                }
            }
            catch { }

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
                var baseSeq = BaseSequenceForFilters();
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

        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters()
        {
            IEnumerable<PolicyPlusPolicy> seq = _allPolicies;
            if (_appliesFilter == AdmxPolicySection.Machine)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both);
            else if (_appliesFilter == AdmxPolicySection.User)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both);
            if (_selectedCategory is not null)
            {
                var allowed = new HashSet<string>();
                CollectPoliciesRecursive(_selectedCategory, allowed);
                seq = seq.Where(p => allowed.Contains(p.UniqueID));
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
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters();
            if (!string.IsNullOrWhiteSpace(query))
                seq = seq.Where(p => p.DisplayName.Contains(query, StringComparison.InvariantCultureIgnoreCase) || p.UniqueID.Contains(query, StringComparison.InvariantCultureIgnoreCase));
            BindSequence(seq);
        }

        private void BindSequence(IEnumerable<PolicyPlusPolicy> seq)
        {
            var grouped = seq.GroupBy(p => p.DisplayName, System.StringComparer.InvariantCultureIgnoreCase);
            _nameGroups = grouped.ToDictionary(g => g.Key, g => g.ToList(), StringComparer.InvariantCultureIgnoreCase);
            _visiblePolicies = grouped.Select(PickRepresentative).OrderBy(p => p.DisplayName).ToList();
            PolicyList.ItemsSource = _visiblePolicies;
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
        { var p = PolicyList?.SelectedItem as PolicyPlusPolicy; SetDetails(p); }

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
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };
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

        private void MarkDirty()
        {
            _pendingChanges = true;
        }

        private PolicyPlusPolicy? GetContextMenuPolicy(object sender)
        {
            return (sender as FrameworkElement)?.Tag as PolicyPlusPolicy;
        }

        private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            BtnSave_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private void PolicyList_RightTapped(object sender, RightTappedRoutedEventArgs e) { }

        private async void BtnViewFormatted_Click(object sender, RoutedEventArgs e)
        {
            var p = PolicyList?.SelectedItem as PolicyPlusPolicy; if (p is null || _bundle is null) return;
            var win = new Windows.DetailPolicyFormattedWindow();
            win.Initialize(p, _bundle, _compSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource(), _userSource ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(), p.RawPolicy.Section);
            win.Activate();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty; _selectedCategory = null; _configuredOnly = false; if (ChkConfiguredOnly != null) ChkConfiguredOnly.IsChecked = false; ApplyFiltersAndBind();
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
        private void BtnSave_Click(object sender, RoutedEventArgs e) { _pendingChanges = false; ShowInfo("Saved."); }

        private void ContextViewFormatted_Click(object sender, RoutedEventArgs e) { BtnViewFormatted_Click(sender, e); }
        private void ContextCopyName_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy); if (p!=null) { var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(p.DisplayName); Clipboard.SetContent(dp);} }
        private void ContextCopyId_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy); if (p!=null) { var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(p.UniqueID); Clipboard.SetContent(dp);} }
        private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy); if (p==null) return; var sb=new StringBuilder(); var c=p.Category; var stack=new Stack<string>(); while(c!=null){stack.Push(c.DisplayName); c=c.Parent;} sb.AppendLine("Administrative Templates"); foreach(var name in stack) sb.AppendLine("+ "+name); sb.AppendLine("+ "+p.DisplayName); var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(sb.ToString()); Clipboard.SetContent(dp); }
        private void ContextRevealInTree_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy); if (p==null||CategoryTree==null) return; _selectedCategory=p.Category; BuildCategoryTree(); }
        private void ContextCopyRegPaths_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy); if (p==null) return; var list=PolicyProcessing.GetReferencedRegistryValues(p).Select(kv=>$"{kv.Key} [{kv.Value}]"); var dp=new DataPackage{RequestedOperation=DataPackageOperation.Copy}; dp.SetText(string.Join("\r\n",list)); Clipboard.SetContent(dp); }
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
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void CategoryTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            var cat = args.InvokedItem as PolicyPlusCategory;
            ScheduleApplyCategory(cat);
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

            // Find the TreeViewItem container that was double-tapped
            var src = e.OriginalSource as DependencyObject;
            TreeViewItem? container = null;
            while (src != null && container == null)
            {
                container = src as TreeViewItem;
                src = VisualTreeHelper.GetParent(src);
            }
            if (container == null) return;

            // Get the corresponding node and toggle expansion
            var node = CategoryTree.NodeFromContainer(container);
            if (node != null)
            {
                _suppressCategorySelectionChanged = true;
                node.IsExpanded = !node.IsExpanded;
                _suppressCategorySelectionChanged = false;

                // Apply filter for the double-tapped category as well
                if (node.Content is PolicyPlusCategory cat)
                {
                    _selectedCategory = cat;
                    ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                }
                e.Handled = true;
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
                if (ChkUseTempPol != null)
                {
                    ChkUseTempPol.IsChecked = t.IsChecked;
                    ChkUseTempPol_Checked(ChkUseTempPol, new RoutedEventArgs());
                }
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
                var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
                if (p != null)
                {
                    e.Handled = true;
                    await OpenEditDialogForPolicyAsync(p, ensureFront: true);
                }
            }
        }

        private void PolicyList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
            if (p is null) return;
            e.Handled = true;
            _ = OpenEditDialogForPolicyAsync(p, ensureFront: true);
        }

        private async void BtnEditSelected_Click(object sender, RoutedEventArgs e)
        {
            var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
            if (p is null) return;
            await OpenEditDialogForPolicyAsync(p, ensureFront: false);
        }

        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy);
            if (p != null) await OpenEditDialogForPolicyAsync(p, ensureFront: false);
        }

        private void ToggleHideEmptyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                _hideEmptyCategories = t.IsChecked;
                try { _config?.SetValue("HideEmptyCategories", _hideEmptyCategories ? 1 : 0); } catch { }
                BuildCategoryTree();
            }
        }
    }
}
