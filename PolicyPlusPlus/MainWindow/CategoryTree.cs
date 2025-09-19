using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlusCore.Core;
using PolicyPlusPlus.Services; // for PolicySourceAccessor
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        // Added fields moved from main partial during refactor
        private string? _recentDoubleTapCategoryId;
        private DateTime _recentDoubleTapAt;
        private string? _lastInvokedCatId;
        private DateTime _lastInvokedAt;
        private bool _lastTapWasExpanded;
        private string? _lastTapCatId;
        private DateTime _lastTapAt;

        private void CategoryTree_ItemInvoked(Microsoft.UI.Xaml.Controls.TreeView sender, Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs args)
        {
            var cat = args.InvokedItem as PolicyPlusCategory;
            if (cat == null) return;
            _selectedCategory = cat;
            UpdateSearchPlaceholder();
            _navTyping = false;
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
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
                _navTyping = false;
                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
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

        private sealed class CatNodeData
        {
            public required PolicyPlusCategory Cat { get; init; }
            public List<CatNodeData> Children { get; } = new();
        }

        private void BuildCategoryTreeAsync()
        {
            if (CategoryTree == null || _bundle == null) return;

            var bundleLocal = _bundle;
            var hideEmpty = _hideEmptyCategories; // user preference
            var selectedId = _selectedCategory?.UniqueID;
            // Intentionally NOT capturing _configuredOnly / _bookmarksOnly here so that the tree always shows the full taxonomy.

            _ = Task.Run(() =>
            {
                List<CatNodeData> roots;
                try
                {
                    bool IsTrulyEmpty(PolicyPlusCategory c)
                    {
                        if (c.Policies.Count > 0) return false;
                        foreach (var ch in c.Children)
                        {
                            if (!IsTrulyEmpty(ch)) return false;
                        }
                        return true;
                    }

                    bool Include(PolicyPlusCategory cat)
                    {
                        if (!hideEmpty) return true; // show everything when user disabled hiding
                        // Hide only categories that have no policies anywhere in their subtree.
                        return !IsTrulyEmpty(cat);
                    }

                    var rootCats = bundleLocal.Categories.Values.Where(c => c.Parent == null)
                        .OrderBy(c => c.DisplayName)
                        .ToList();

                    CatNodeData BuildNode(PolicyPlusCategory c)
                    {
                        var nd = new CatNodeData { Cat = c };
                        foreach (var ch in c.Children.OrderBy(x => x.DisplayName))
                        {
                            if (!Include(ch)) continue;
                            nd.Children.Add(BuildNode(ch));
                        }
                        return nd;
                    }

                    roots = new List<CatNodeData>();
                    foreach (var rc in rootCats)
                    {
                        if (Include(rc))
                        {
                            roots.Add(BuildNode(rc));
                        }
                    }
                }
                catch
                {
                    roots = new List<CatNodeData>();
                }

                DispatcherQueue.TryEnqueue(() => ApplyCategoryTreeData(roots, selectedId));
            });
        }

        private void ApplyCategoryTreeData(List<CatNodeData> roots, string? selectedId)
        {
            if (CategoryTree == null) return;
            _suppressCategorySelectionChanged = true;
            try
            {
                var oldMode = CategoryTree.SelectionMode;
                CategoryTree.SelectionMode = Microsoft.UI.Xaml.Controls.TreeViewSelectionMode.None;

                CategoryTree.RootNodes.Clear();

                Microsoft.UI.Xaml.Controls.TreeViewNode Convert(CatNodeData d)
                {
                    var node = new Microsoft.UI.Xaml.Controls.TreeViewNode { Content = d.Cat };
                    foreach (var ch in d.Children)
                        node.Children.Add(Convert(ch));
                    return node;
                }

                foreach (var d in roots)
                {
                    var node = Convert(d);
                    CategoryTree.RootNodes.Add(node);
                }

                CategoryTree.SelectionMode = oldMode;

                if (!string.IsNullOrEmpty(selectedId))
                {
                    var cat = FindCategoryById(selectedId);
                    if (cat != null)
                        SelectCategoryInTree(cat);
                }
            }
            finally
            {
                _suppressCategorySelectionChanged = false;
            }
        }

        private PolicyPlusCategory? FindCategoryById(string uniqueId)
        {
            try
            {
                return _bundle?.Categories.Values.FirstOrDefault(c => string.Equals(c.UniqueID, uniqueId, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        // Legacy synchronous builder kept for fallback
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
                var ctx = PolicySourceAccessor.Acquire();
                return ViewModels.CategoryVisibilityEvaluator.IsCategoryVisible(cat, _allPolicies, _appliesFilter, _configuredOnly, ctx.Comp, ctx.User);
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
                    _navTyping = false;
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
        }
    }
}
