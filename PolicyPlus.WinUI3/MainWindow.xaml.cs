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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PolicyPlus.WinUI3
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
    private PolicyLoader? _loader;
    private AdmxBundle? _bundle;
    private List<PolicyPlusPolicy> _allPolicies = new();
    private List<PolicyPlusPolicy> _visiblePolicies = new();
    private AdmxPolicySection _appliesFilter = AdmxPolicySection.Both;
    private ConfigurationStorage? _config;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Try auto-load from default Windows path if available
            try
            {
                _config = new ConfigurationStorage(Microsoft.Win32.RegistryHive.CurrentUser, @"Software\Policy Plus");
                string defaultPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\PolicyDefinitions");
                var lastObj = _config.GetValue("AdmxSource", defaultPath);
                string lastPath = lastObj == null ? defaultPath : Convert.ToString(lastObj) ?? defaultPath;
                if (System.IO.Directory.Exists(lastPath))
                {
                    _bundle = new AdmxBundle();
                    var fails = _bundle.LoadFolder(lastPath, System.Globalization.CultureInfo.CurrentUICulture.Name);
                    if (LoadErrors != null)
                        LoadErrors.Text = fails.Any() ? $"Load issues: {fails.Count()}" : string.Empty;
                    BuildCategoryTree();
                    _allPolicies = _bundle.Policies.Values.OrderBy(p => p.DisplayName).ToList();
                    ApplyFiltersAndBind();
                }
            }
            catch { /* ignore on startup */ }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = ThemeSelector as ComboBox;
            var item = (cb?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (item == "Light")
            {
                RootGrid.RequestedTheme = ElementTheme.Light;
            }
            else
            {
                RootGrid.RequestedTheme = ElementTheme.Dark;
            }
        }

        private void BtnLoadLocalGpo_Click(object sender, RoutedEventArgs e)
        {
            _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            var src = _loader.OpenSource();
            LoaderInfo.Text = _loader.GetDisplayInfo();
        }

        private async void BtnLoadAdmxFolder_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;

            _bundle = new AdmxBundle();
            var fails = _bundle.LoadFolder(folder.Path, System.Globalization.CultureInfo.CurrentUICulture.Name);
            LoadErrors.Text = fails.Any() ? $"Load issues: {fails.Count()}" : string.Empty;
            BuildCategoryTree();
            // Compose policy list
            _allPolicies = _bundle.Policies.Values.OrderBy(p => p.DisplayName).ToList();
            ApplyFiltersAndBind();
            try { _config?.SetValue("AdmxSource", folder.Path); } catch { }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_bundle is null || PolicyList == null || PolicyCount == null)
            {
                if (PolicyList != null) PolicyList.ItemsSource = null;
                if (PolicyCount != null) PolicyCount.Text = "";
                return;
            }
            var q = (SearchBox.Text ?? string.Empty).Trim();
            ApplyFiltersAndBind(q);
        }

        private void AppliesToSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = (AppliesToSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            _appliesFilter = sel switch
            {
                "Computer" => AdmxPolicySection.Machine,
                "User" => AdmxPolicySection.User,
                _ => AdmxPolicySection.Both
            };
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void CategoryTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            var cat = args.InvokedItem as PolicyPlusCategory;
            if (cat is null) return;
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty, cat);
        }

        private void ClearCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty, null);
        }

        private void BuildCategoryTree()
        {
            if (CategoryTree == null) return;
            CategoryTree.RootNodes.Clear();
            if (_bundle is null) return;
            // Build top-level categories
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

        private void ApplyFiltersAndBind(string query = "", PolicyPlusCategory? category = null)
        {
            if (PolicyList == null || PolicyCount == null) return;
            IEnumerable<PolicyPlusPolicy> seq = _allPolicies;
            // Applies-to filter
            if (_appliesFilter == AdmxPolicySection.Machine)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both);
            else if (_appliesFilter == AdmxPolicySection.User)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both);

            // Category filter
            if (category is not null)
            {
                var allowed = new HashSet<string>();
                CollectPoliciesRecursive(category, allowed);
                seq = seq.Where(p => allowed.Contains(p.UniqueID));
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(query))
            {
                seq = seq.Where(p => p.DisplayName.Contains(query, StringComparison.InvariantCultureIgnoreCase) || p.UniqueID.Contains(query, StringComparison.InvariantCultureIgnoreCase));
            }

            _visiblePolicies = seq.OrderBy(p => p.DisplayName).ToList();
            PolicyList.ItemsSource = _visiblePolicies;
            PolicyCount.Text = $"Policies: {_visiblePolicies.Count} / {_allPolicies.Count}";
            // Clear details when list changes
            SetDetails(null);
        }

        private void CollectPoliciesRecursive(PolicyPlusCategory cat, HashSet<string> sink)
        {
            foreach (var p in cat.Policies)
                sink.Add(p.UniqueID);
            foreach (var child in cat.Children)
                CollectPoliciesRecursive(child, sink);
        }

        private void PolicyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var p = PolicyList?.SelectedItem as PolicyPlusPolicy;
            SetDetails(p);
        }

        private void SetDetails(PolicyPlusPolicy? p)
        {
            if (DetailTitle == null) return;
            if (p is null)
            {
                DetailTitle.Text = "";
                DetailId.Text = "";
                DetailCategory.Text = "";
                DetailApplies.Text = "";
                DetailSupported.Text = "";
                DetailExplain.Text = "";
                return;
            }
            DetailTitle.Text = p.DisplayName;
            DetailId.Text = p.UniqueID;
            DetailCategory.Text = p.Category is null ? "" : $"Category: {p.Category.DisplayName}";
            var applies = p.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "Computer",
                AdmxPolicySection.User => "User",
                _ => "Both"
            };
            DetailApplies.Text = $"Applies to: {applies}";
            DetailSupported.Text = p.SupportedOn is null ? "" : $"Supported on: {p.SupportedOn.DisplayName}";
            DetailExplain.Text = p.DisplayExplanation ?? "";
        }
    }
}
