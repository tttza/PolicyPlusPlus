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

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow : Window
    {
        // Constructor was missing; without it XAML was never loaded.
        public MainWindow()
        {
            this.InitializeComponent();
        }

        // Restored fields
        private PolicyLoader? _loader;
        private AdmxBundle? _bundle;
        private List<PolicyPlusPolicy> _allPolicies = new();
        private List<PolicyPlusPolicy> _visiblePolicies = new();
        private Dictionary<string, List<PolicyPlusPolicy>> _nameGroups = new(StringComparer.InvariantCultureIgnoreCase);
        private int _totalGroupCount = 0;
        private AdmxPolicySection _appliesFilter = AdmxPolicySection.Both;
        private PolicyPlusCategory? _selectedCategory = null;
        private ConfigurationStorage? _config;
        private IPolicySource? _compSource;
        private IPolicySource? _userSource;
        private bool _configuredOnly = false;

        private static readonly Regex UrlRegex = new Regex(@"(https?://[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static void SetPlainText(RichTextBlock rtb, string text)
        {
            rtb.Blocks.Clear();
            var p = new Paragraph();
            p.Inlines.Add(new Run { Text = text ?? string.Empty });
            rtb.Blocks.Add(p);
        }

        private void ShowInfo(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
        {
            if (StatusBar == null) return;
            StatusBar.Severity = severity;
            StatusBar.Message = message;
            StatusBar.IsOpen = true;
        }

        private void HideInfo()
        {
            if (StatusBar == null) return;
            StatusBar.IsOpen = false;
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
                var themePref = Convert.ToString(_config?.GetValue("Theme", "System")) ?? "System";
                ApplyTheme(themePref);
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
                var langPref = Convert.ToString(_config?.GetValue("UICulture", CultureInfo.CurrentUICulture.Name)) ?? CultureInfo.CurrentUICulture.Name;
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
            finally
            {
                SetBusy(false);
            }
        }

        private void ApplyTheme(string pref)
        {
            if (RootGrid == null) return;
            RootGrid.RequestedTheme = pref switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = ThemeSelector as ComboBox;
            var item = (cb?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var pref = item ?? "System";
            ApplyTheme(pref);
            try { _config?.SetValue("Theme", pref); } catch { }
        }

        private void BtnLoadLocalGpo_Click(object sender, RoutedEventArgs e)
        {
            _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            _compSource = _loader.OpenSource();
            _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
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
            if (category != null) _selectedCategory = category; // persist selection
            else if (category == null && _selectedCategory != null) { /* keep current */ }
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters();
            if (!string.IsNullOrWhiteSpace(query))
                seq = seq.Where(p => p.DisplayName.Contains(query, StringComparison.InvariantCultureIgnoreCase) || p.UniqueID.Contains(query, StringComparison.InvariantCultureIgnoreCase));
            BindSequence(seq);
        }

        private void BindSequence(IEnumerable<PolicyPlusPolicy> seq)
        {
            var grouped = seq.GroupBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase);
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
                    continue; // do not hyperlink URLs inside quoted strings (e.g., JSON)

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

        private async Task OpenEditDialogForPolicyAsync(PolicyPlusPolicy representative)
        {
            if (_bundle is null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            { _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false); _compSource = _loader.OpenSource(); _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(); }

            var displayName = representative.DisplayName;
            _nameGroups.TryGetValue(displayName, out var groupList);
            groupList ??= _allPolicies.Where(p => string.Equals(p.DisplayName, displayName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            // Default to User. If both variants exist, pick User; else fallback to Machine; else keep representative.
            PolicyPlusPolicy targetPolicy = groupList.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.User)
                                        ?? groupList.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Machine)
                                        ?? representative;

            // For Both-type policies, default to User section when opening.
            var initialSection = targetPolicy.RawPolicy.Section == AdmxPolicySection.Both
                ? AdmxPolicySection.User
                : targetPolicy.RawPolicy.Section;

            var dialog = new EditSettingDialog();
            dialog.XamlRoot = this.Content.XamlRoot;
            dialog.Initialize(targetPolicy,
                initialSection,
                _bundle, _compSource!, _userSource!,
                new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false), new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true),
                new Dictionary<string, string>(), new Dictionary<string, string>());
            await dialog.ShowAsync();
        }

        private async void BtnFind_Click(object sender, RoutedEventArgs e)
        {
            if (_bundle is null) return;
            var dialog = new FindByTextDialog(); dialog.XamlRoot = this.Content.XamlRoot;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.Searcher != null)
            {
                var searcher = dialog.Searcher;
                var baseSeq = BaseSequenceForFilters(); // constrain to selected category and applies filter
                var matches = baseSeq.Where(p => searcher(p));
                BindSequence(matches);
                ShowInfo($"Found {_visiblePolicies.Count} policy(ies).");
            }
        }

        private void BuildCategoryTree()
        {
            if (CategoryTree == null) return;
            CategoryTree.RootNodes.Clear();
            if (_bundle is null) return;
            foreach (var kv in _bundle.Categories.OrderBy(k => k.Value.DisplayName))
            {
                var cat = kv.Value;
                var node = new Microsoft.UI.Xaml.Controls.TreeViewNode() { Content = cat, IsExpanded = false };
                BuildChildCategoryNodes(node, cat);
                CategoryTree.RootNodes.Add(node);
            }
        }

        private void BuildChildCategoryNodes(Microsoft.UI.Xaml.Controls.TreeViewNode parentNode, PolicyPlusCategory parentCat)
        {
            foreach (var child in parentCat.Children.OrderBy(c => c.DisplayName))
            {
                var node = new Microsoft.UI.Xaml.Controls.TreeViewNode() { Content = child };
                parentNode.Children.Add(node);
                if (child.Children.Count > 0)
                    BuildChildCategoryNodes(node, child);
            }
        }
        private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
                if (p != null)
                {
                    e.Handled = true;
                    await OpenEditDialogForPolicyAsync(p);
                }
            }
        }

        private void CategoryTree_SelectionChanged(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewSelectionChangedEventArgs args)
        {
            if (CategoryTree?.SelectedNodes?.Count > 0)
            {
                var node = CategoryTree.SelectedNodes[0] as Microsoft.UI.Xaml.Controls.TreeViewNode;
                var cat = node?.Content as PolicyPlusCategory;
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty, cat);
            }
        }

        private void CategoryTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            var cat = args.InvokedItem as PolicyPlusCategory;
            if (cat is null) return;
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty, cat);
        }

        private async void PolicyList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
            if (p is null) return;
            await OpenEditDialogForPolicyAsync(p);
        }

        private void PolicyList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is ListView lv)
            {
                var element = e.OriginalSource as FrameworkElement;
                while (element != null && element.DataContext is not PolicyPlusPolicy)
                    element = element.Parent as FrameworkElement;
                if (element?.DataContext is PolicyPlusPolicy p)
                    lv.SelectedItem = p; // select item under pointer
            }
        }

        private PolicyPlusPolicy? GetContextMenuPolicy(object sender)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.Tag is PolicyPlusPolicy tagged)
                    return tagged;
                if (fe.DataContext is PolicyPlusPolicy dc)
                    return dc;
            }
            return PolicyList?.SelectedItem as PolicyPlusPolicy;
        }

        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy);
            if (p != null) await OpenEditDialogForPolicyAsync(p);
        }

        private async void ContextViewFormatted_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender);
            if (p is null || _bundle is null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            {
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
            }
            var dlg = new DetailPolicyFormattedDialog();
            dlg.XamlRoot = this.Content.XamlRoot;
            var section = p.RawPolicy.Section == AdmxPolicySection.Both ? _appliesFilter : p.RawPolicy.Section;
            dlg.Initialize(p, _bundle, _compSource, _userSource, section);
            await dlg.ShowAsync();
        }

        private void ContextCopyName_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy);
            if (p != null)
            {
                var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                data.SetText(p.DisplayName);
                Clipboard.SetContent(data);
            }
        }

        private void ContextCopyId_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as PolicyPlusPolicy);
            if (p != null)
            {
                var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                data.SetText(p.UniqueID);
                Clipboard.SetContent(data);
            }
        }

        private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender);
            if (p is null) return;
            var parts = new List<string>();
            var cat = p.Category;
            while (cat != null)
            {
                parts.Add(cat.DisplayName);
                cat = cat.Parent;
            }
            parts.Reverse();
            var applies = p.RawPolicy.Section switch { AdmxPolicySection.Machine => "Computer", AdmxPolicySection.User => "User", _ => "User" };
            var text = string.Join(" ", new[] { applies, ">", "Administrative Templates", ">", string.Join(" > ", parts), ">", p.DisplayName });
            var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            data.SetText(text);
            Clipboard.SetContent(data);
        }

        private void ContextCopyRegPaths_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender);
            if (p is null) return;
            var vals = PolicyProcessing.GetReferencedRegistryValues(p);
            var lines = vals.Select(kv => $"{kv.Key} ({kv.Value})");
            var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            data.SetText(string.Join(System.Environment.NewLine, lines));
            Clipboard.SetContent(data);
        }

        private void ClearCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            _selectedCategory = null;
            if (CategoryTree != null)
            {
                // Clear visual selection
                CategoryTree.SelectedNode = null;
                try { CategoryTree.SelectedNodes?.Clear(); } catch { }
            }
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty, null);
        }

        private async void BtnEditSelected_Click(object sender, RoutedEventArgs e)
        {
            var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
            if (p is null) return;
            await OpenEditDialogForPolicyAsync(p);
        }

        private async void BtnViewFormatted_Click(object sender, RoutedEventArgs e)
        {
            var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
            if (p is null || _bundle is null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            {
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
            }
            var dlg = new DetailPolicyFormattedDialog();
            dlg.XamlRoot = this.Content.XamlRoot;
            var section = p.RawPolicy.Section == AdmxPolicySection.Both ? _appliesFilter : p.RawPolicy.Section;
            dlg.Initialize(p, _bundle, _compSource, _userSource, section);
            await dlg.ShowAsync();
        }

        private string BuildRegExportForPolicy(PolicyPlusPolicy policy, IPolicySource src)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            var values = PolicyProcessing.GetReferencedRegistryValues(policy);
            var section = policy.RawPolicy.Section == AdmxPolicySection.User ? AdmxPolicySection.User : AdmxPolicySection.Machine;
            foreach (var kv in values)
            {
                sb.AppendLine();
                sb.AppendLine($"[{(section == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE")}\\{kv.Key}]");
                if (string.IsNullOrEmpty(kv.Value)) continue;
                var data = src.GetValue(kv.Key, kv.Value);
                if (data is null) continue;
                sb.AppendLine(FormatRegValue(kv.Value, data));
            }
            return sb.ToString();
        }

        private static string FormatRegValue(string name, object data)
        {
            if (data is uint u) return $"\"{name}\"=dword:{u:x8}";
            if (data is string s) return $"\"{name}\"=\"{EscapeRegString(s)}\"";
            if (data is string[] arr) return $"\"{name}\"=hex(7):{EncodeMultiString(arr)}";
            if (data is byte[] bin) return $"\"{name}\"=hex:{string.Join(",", bin.Select(b => b.ToString("x2")))}";
            if (data is ulong qu) { var b = BitConverter.GetBytes(qu); return $"\"{name}\"=hex(b):{string.Join(",", b.Select(x => x.ToString("x2")))}"; }
            return $"\"{name}\"=hex:{string.Join(",", (byte[])PolicyPlus.PolFile.ObjectToBytes(data, Microsoft.Win32.RegistryValueKind.Binary))}";
        }
        private static string EscapeRegString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string EncodeMultiString(string[] lines)
        {
            var bytes = new List<byte>();
            foreach (var line in lines)
            {
                var b = System.Text.Encoding.Unicode.GetBytes(line);
                bytes.AddRange(b);
                bytes.Add(0); bytes.Add(0);
            }
            bytes.Add(0); bytes.Add(0);
            return string.Join(",", bytes.Select(b => b.ToString("x2")));
        }

        private async void BtnFindReg_Click(object sender, RoutedEventArgs e)
        {
            if (_bundle is null) return;
            var dialog = new FindByRegistryDialog();
            dialog.XamlRoot = this.Content.XamlRoot;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.Searcher != null)
            {
                var searcher = dialog.Searcher;
                var baseSeq = BaseSequenceForFilters();
                var matches = baseSeq.Where(p => searcher(p));
                BindSequence(matches);
                ShowInfo($"Found {_visiblePolicies.Count} policy(ies).");
            }
        }

        private async void BtnFindId_Click(object sender, RoutedEventArgs e)
        {
            if (_bundle is null) return;
            var dialog = new FindByIdDialog();
            dialog.XamlRoot = this.Content.XamlRoot;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.Searcher != null)
            {
                var searcher = dialog.Searcher;
                var baseSeq = BaseSequenceForFilters();
                var matches = baseSeq.Where(p => searcher(p));
                BindSequence(matches);
                ShowInfo($"Found {_visiblePolicies.Count} policy(ies).");
            }
        }

        private async void BtnLanguage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LanguageOptionsDialog();
            dlg.XamlRoot = this.Content.XamlRoot;
            var path = Convert.ToString(_config?.GetValue("AdmxSource", Environment.ExpandEnvironmentVariables(@"%WINDIR%\PolicyDefinitions")));
            var current = Convert.ToString(_config?.GetValue("UICulture", CultureInfo.CurrentUICulture.Name)) ?? CultureInfo.CurrentUICulture.Name;
            dlg.Initialize(path ?? string.Empty, current);
            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.Primary && !string.IsNullOrEmpty(dlg.SelectedLanguage))
            {
                try { _config?.SetValue("UICulture", dlg.SelectedLanguage); } catch { }
                if (_bundle != null && !string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                    LoadAdmxFolderAsync(path);
            }
        }

        private async void BtnExportReg_Click(object sender, RoutedEventArgs e)
        {
            if (_bundle is null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            { _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false); _compSource = _loader.OpenSource(); _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(); }

            var savePicker = new FileSavePicker();
            InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(this));
            savePicker.FileTypeChoices.Add("Registration entries", new List<string> { ".reg" });
            savePicker.SuggestedFileName = "policies_export";
            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;

            var seq = BaseSequenceForFilters();
            var grouped = seq.GroupBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            foreach (var g in grouped)
            {
                // default to user
                var target = g.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.User)
                            ?? g.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Machine)
                            ?? g.First();
                var src = target.RawPolicy.Section == AdmxPolicySection.User ? _userSource! : _compSource!;
                var values = PolicyProcessing.GetReferencedRegistryValues(target);
                foreach (var kv in values)
                {
                    sb.AppendLine();
                    sb.AppendLine($"[{(target.RawPolicy.Section == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE")}\\{kv.Key}]");
                    if (string.IsNullOrEmpty(kv.Value)) continue;
                    var data = src.GetValue(kv.Key, kv.Value);
                    if (data is null) continue;
                    sb.AppendLine(FormatRegValue(kv.Value, data));
                }
            }
            await FileIO.WriteTextAsync(file, sb.ToString());
            ShowInfo("Exported .reg for current view.");
        }

        private void ChkConfiguredOnly_Checked(object sender, RoutedEventArgs e)
        {
            _configuredOnly = ChkConfiguredOnly.IsChecked == true;
            if (_configuredOnly)
                EnsureLocalSources();
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void ContextCopyRegExport_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender);
            if (p is null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            {
                _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                _compSource = _loader.OpenSource();
                _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
            }
            var src = (p.RawPolicy.Section == AdmxPolicySection.User ? _userSource : _compSource)!;
            var reg = BuildRegExportForPolicy(p, src);
            var data = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            data.SetText(reg);
            Clipboard.SetContent(data);
            ShowInfo(".reg export copied to clipboard");
        }

        private async void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(this));
            savePicker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
            savePicker.SuggestedFileName = "policies";
            var file = await savePicker.PickSaveFileAsync();
            if (file is null) return;
            var sb = new StringBuilder();
            sb.AppendLine("Name,ID,Applies,Category");
            foreach (var p in _visiblePolicies)
            {
                var applies = p.RawPolicy.Section switch { AdmxPolicySection.Machine => "Computer", AdmxPolicySection.User => "User", _ => "Both" };
                var cat = p.Category?.DisplayName ?? string.Empty;
                string esc(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
                sb.AppendLine(string.Join(",", new[] { esc(p.DisplayName), esc(p.UniqueID), esc(applies), esc(cat) }));
            }
            await FileIO.WriteTextAsync(file, sb.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8);
            ShowInfo("Exported CSV for current view.");
        }

        private void ContextRevealInTree_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender);
            if (p?.Category is null) return;
            // expand and select category in tree
            foreach (var root in CategoryTree.RootNodes)
            {
                if (ExpandToCategory(root, p.Category))
                {
                    CategoryTree.SelectedNode = FindNodeForCategory(root, p.Category);
                    break;
                }
            }
        }

        private bool ExpandToCategory(Microsoft.UI.Xaml.Controls.TreeViewNode node, PolicyPlusCategory target)
        {
            if (node.Content == target)
            {
                node.IsExpanded = true;
                return true;
            }
            foreach (var child in node.Children)
            {
                var tvn = (Microsoft.UI.Xaml.Controls.TreeViewNode)child;
                if (ExpandToCategory(tvn, target))
                {
                    node.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        private Microsoft.UI.Xaml.Controls.TreeViewNode? FindNodeForCategory(Microsoft.UI.Xaml.Controls.TreeViewNode node, PolicyPlusCategory target)
        {
            if (node.Content == target) return node;
            foreach (var child in node.Children)
            {
                var found = FindNodeForCategory((Microsoft.UI.Xaml.Controls.TreeViewNode)child, target);
                if (found != null) return found;
            }
            return null;
        }

        private async void BtnImportPol_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ImportPolDialog();
            dlg.XamlRoot = this.Content.XamlRoot;
            var res = await dlg.ShowAsync();
            if (res != ContentDialogResult.Primary || dlg.Pol is null) return;

            EnsureLocalSources();
            // Apply imported POL into the currently selected applies target
            var target = _appliesFilter == AdmxPolicySection.User ? _userSource! : _compSource!;
            dlg.Pol.Apply(target);
            ShowInfo("Imported .pol.");
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private async void BtnImportReg_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ImportRegDialog();
            dlg.XamlRoot = this.Content.XamlRoot;
            var res = await dlg.ShowAsync();
            if (res != ContentDialogResult.Primary || dlg.ParsedReg is null) return;

            EnsureLocalSources();
            var target = _appliesFilter == AdmxPolicySection.User ? _userSource! : _compSource!;
            dlg.ParsedReg.Apply(target);
            ShowInfo("Imported .reg.");
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            // Reset category
            _selectedCategory = null;
            // Reset applies filter
            _appliesFilter = AdmxPolicySection.Both;
            if (AppliesToSelector != null)
                AppliesToSelector.SelectedIndex = 0;
            // Reset configured-only
            _configuredOnly = false;
            if (ChkConfiguredOnly != null)
                ChkConfiguredOnly.IsChecked = false;
            // Reset search
            if (SearchBox != null)
            {
                SearchBox.Text = string.Empty;
                SearchBox.ItemsSource = null;
            }
            // Clear status
            HideInfo();
            // Rebind full sequence
            ApplyFiltersAndBind(string.Empty, null);
        }
        private void PolicyList_ItemClick(object sender, ItemClickEventArgs e)
        {
            // no-op: opening on double-click only
        }
        private void CategoryTree_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // Toggle expand/collapse on the node under pointer
            var element = e.OriginalSource as FrameworkElement;
            while (element != null && element.DataContext is not null && element.DataContext.GetType().Name != "TreeViewNode")
            {
                element = element.Parent as FrameworkElement;
            }
            Microsoft.UI.Xaml.Controls.TreeViewNode? node = null;
            if (element?.DataContext is Microsoft.UI.Xaml.Controls.TreeViewNode n)
            {
                node = n;
            }
            else if (CategoryTree?.SelectedNodes?.Count > 0)
            {
                node = CategoryTree.SelectedNodes[0] as Microsoft.UI.Xaml.Controls.TreeViewNode;
            }
            if (node != null)
            {
                node.IsExpanded = !node.IsExpanded;
                e.Handled = true;
            }
        }
    }
}
