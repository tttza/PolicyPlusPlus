using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PolicyPlusPlus.Services;
using System.Linq;
using System.Collections.Generic;
using PolicyPlus.Core.Core;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private bool _suppressHistoryPush;
        private Dictionary<string, PolicyPlusCategory>? _catIndex;

        private void InitNavigation()
        {
            try
            {
                var nav = ViewNavigationService.Instance;
                nav.HistoryChanged += (s, e) => UpdateNavButtons();
                // Build category index for stable ID->instance resolution
                if (_bundle != null) _catIndex = CategoryIndex.BuildIndex(_bundle);
                // Push initial baseline state
                MaybePushCurrentState();
                UpdateNavButtons();
            }
            catch { }
        }

        private void UpdateNavButtons()
        {
            try
            {
                if (BtnBack != null) BtnBack.IsEnabled = ViewNavigationService.Instance.CanGoBack;
                if (BtnForward != null) BtnForward.IsEnabled = ViewNavigationService.Instance.CanGoForward;
            }
            catch { }
        }

        private void MaybePushCurrentState()
        {
            if (_suppressHistoryPush || _navTyping) return;
            try
            {
                var q = SearchBox?.Text ?? string.Empty;
                var catId = _selectedCategory?.UniqueID;
                var state = ViewState.Create(catId, q, _appliesFilter, _configuredOnly);
                ViewNavigationService.Instance.Push(state);
                UpdateNavButtons();
            }
            catch { }
        }

        private void ApplyViewState(ViewState? state)
        {
            if (state == null) return;
            _suppressHistoryPush = true;
            try
            {
                if (_bundle != null && _catIndex == null) _catIndex = CategoryIndex.BuildIndex(_bundle);

                // Applies filter
                _appliesFilter = state.AppliesTo;
                if (AppliesToSelector != null)
                {
                    _suppressAppliesToSelectionChanged = true;
                    var idx = state.AppliesTo == AdmxPolicySection.Machine ? 1 : state.AppliesTo == AdmxPolicySection.User ? 2 : 0;
                    AppliesToSelector.SelectedIndex = idx;
                    _suppressAppliesToSelectionChanged = false;
                }

                // Configured only
                _configuredOnly = state.ConfiguredOnly;
                if (ChkConfiguredOnly != null)
                {
                    _suppressConfiguredOnlyChanged = true;
                    ChkConfiguredOnly.IsChecked = _configuredOnly;
                    _suppressConfiguredOnlyChanged = false;
                }

                // Resolve category via flat index to handle nested categories
                PolicyPlusCategory? catToApply = null;
                if (!string.IsNullOrEmpty(state.CategoryId) && _catIndex != null)
                {
                    _catIndex.TryGetValue(state.CategoryId!, out catToApply);
                }

                // Search text
                if (SearchBox != null) SearchBox.Text = state.Query ?? string.Empty;

                // Set selection first, then bind once
                if (catToApply != null)
                {
                    _selectedCategory = catToApply;
                    SelectCategoryInTree(catToApply);
                    ApplyFiltersAndBind(state.Query ?? string.Empty);
                }
                else
                {
                    _selectedCategory = null;
                    if (CategoryTree != null)
                    {
                        _suppressCategorySelectionChanged = true;
                        var old = CategoryTree.SelectionMode;
                        CategoryTree.SelectionMode = Microsoft.UI.Xaml.Controls.TreeViewSelectionMode.None;
                        CategoryTree.SelectedNode = null;
                        CategoryTree.SelectionMode = old;
                        _suppressCategorySelectionChanged = false;
                    }
                    ApplyFiltersAndBind(state.Query ?? string.Empty);
                }
            }
            finally
            {
                _suppressHistoryPush = false;
                _navTyping = false;
                UpdateNavButtons();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            var s = ViewNavigationService.Instance.GoBack();
            ApplyViewState(s);
        }

        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            var s = ViewNavigationService.Instance.GoForward();
            ApplyViewState(s);
        }
    }
}
