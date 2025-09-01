using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlus.WinUI3.Utils;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private void CategoryTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            var cat = args.InvokedItem as PolicyPlusCategory;
            if (cat == null) return;
            _selectedCategory = cat;
            UpdateSearchPlaceholder();
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            SelectCategoryInTree(cat);

            var now = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(_recentDoubleTapCategoryId)
                && string.Equals(_recentDoubleTapCategoryId, cat.UniqueID, StringComparison.OrdinalIgnoreCase)
                && (now - _recentDoubleTapAt).TotalMilliseconds < 500)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_lastInvokedCatId)
                && string.Equals(_lastInvokedCatId, cat.UniqueID, StringComparison.OrdinalIgnoreCase)
                && (now - _lastInvokedAt).TotalMilliseconds < 350)
            {
                var node = FindNodeByCategory(sender.RootNodes, cat.UniqueID);
                if (node != null) node.IsExpanded = !node.IsExpanded;
                _lastInvokedCatId = null;
                return;
            }

            _lastInvokedCatId = cat.UniqueID;
            _lastInvokedAt = now;
        }

        private void CategoryTree_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (CategoryTree == null) return;
            var dep = e.OriginalSource as DependencyObject;
            var container = FindAncestorTreeViewItem(dep);
            Microsoft.UI.Xaml.Controls.TreeViewNode? node = null;
            if (container != null)
                node = CategoryTree.NodeFromContainer(container);
            if (node == null)
                node = (e.OriginalSource as FrameworkElement)?.DataContext as Microsoft.UI.Xaml.Controls.TreeViewNode;
            if (node == null)
                node = CategoryTree.SelectedNode;
            if (node == null) return;

            _lastTapWasExpanded = node.IsExpanded;
            if (node.Content is PolicyPlusCategory cat)
                _lastTapCatId = cat.UniqueID;
            else
                _lastTapCatId = null;
            _lastTapAt = DateTime.UtcNow;
        }

        private void CategoryTree_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (CategoryTree == null) return;

            var dep = e.OriginalSource as DependencyObject;
            var container = FindAncestorTreeViewItem(dep);

            Microsoft.UI.Xaml.Controls.TreeViewNode? node = null;
            if (container != null)
            {
                node = CategoryTree.NodeFromContainer(container);
            }
            if (node == null)
            {
                node = (e.OriginalSource as FrameworkElement)?.DataContext as Microsoft.UI.Xaml.Controls.TreeViewNode;
            }
            if (node == null)
            {
                node = CategoryTree.SelectedNode;
            }
            if (node == null) return;

            e.Handled = true;

            bool desiredExpanded;
            if (node.Content is PolicyPlusCategory cat0
                && !string.IsNullOrEmpty(_lastTapCatId)
                && string.Equals(_lastTapCatId, cat0.UniqueID, StringComparison.OrdinalIgnoreCase)
                && (DateTime.UtcNow - _lastTapAt).TotalMilliseconds < 600)
            {
                desiredExpanded = !_lastTapWasExpanded;
            }
            else
            {
                desiredExpanded = !node.IsExpanded;
            }
            node.IsExpanded = desiredExpanded;

            if (node.Content is PolicyPlusCategory cat)
            {
                _selectedCategory = cat;
                UpdateSearchPlaceholder();
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                _suppressCategorySelectionChanged = true;
                try { CategoryTree.SelectedNode = node; }
                finally { _suppressCategorySelectionChanged = false; }

                _recentDoubleTapCategoryId = cat.UniqueID;
                _recentDoubleTapAt = DateTime.UtcNow;
            }
        }

        private static TreeViewItem? FindAncestorTreeViewItem(DependencyObject? start)
        {
            while (start != null)
            {
                if (start is TreeViewItem tvi) return tvi;
                start = VisualTreeHelper.GetParent(start);
            }
            return null;
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
                Microsoft.UI.Xaml.Controls.TreeViewNode? target = FindNodeByCategory(CategoryTree.RootNodes, category.UniqueID);
                if (target == null) return;

                var parent = target.Parent;
                while (parent != null)
                {
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }

                CategoryTree.SelectedNode = target;

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
                if (n.Content is PolicyPlusCategory pc && string.Equals(pc.UniqueID, uniqueId, StringComparison.OrdinalIgnoreCase))
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
                EnsureLocalSources();
                return ViewModels.CategoryVisibilityEvaluator.IsCategoryVisible(cat, _allPolicies, _appliesFilter, _configuredOnly, _compSource, _userSource);
            }
            catch { return true; }
        }

        private void CategoryTree_SelectionChanged(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewSelectionChangedEventArgs args)
        {
            if (_suppressCategorySelectionChanged) return;
            if (sender.SelectedNodes != null && sender.SelectedNodes.Count > 0)
            {
                var cat = sender.SelectedNodes.FirstOrDefault()?.Content as PolicyPlusCategory;
                if (cat != null)
                {
                    _selectedCategory = cat;
                    UpdateSearchPlaceholder();
                    ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                }
            }
        }
    }
}
