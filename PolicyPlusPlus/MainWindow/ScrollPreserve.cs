using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        // Fields are defined in MainWindow.xaml.cs

        private void PreserveScrollPosition()
        {
            try
            {
                if (PolicyList == null) return;
                EnsureScrollViewerHook();
                var current = _policyListScroll?.VerticalOffset;
                var known = _lastKnownVerticalOffset;
                _savedVerticalOffset = (known.HasValue ? known : current);

                if (PolicyList.SelectedItem is Models.PolicyListRow sel && sel.Policy != null)
                {
                    _savedSelectedPolicyId = sel.Policy.UniqueID;
                    var row = FindRowContainerForItem(PolicyList.SelectedItem);
                    if (row != null && _policyListScroll != null)
                    {
                        try
                        {
                            var pt = row.TransformToVisual(_policyListScroll).TransformPoint(new Point(0, 0));
                            _savedAnchorViewportY = pt.Y;
                            var vh = _policyListScroll.ViewportHeight;
                            _savedAnchorRatio = (vh > 0) ? Math.Clamp(_savedAnchorViewportY.Value / vh, 0.0, 1.0) : null;
                        }
                        catch { _savedAnchorViewportY = null; _savedAnchorRatio = null; }
                    }
                    else
                    {
                        _savedAnchorViewportY = null; _savedAnchorRatio = null;
                    }
                }
                else
                {
                    _savedSelectedPolicyId = null;
                    _savedAnchorViewportY = null; _savedAnchorRatio = null;
                }
            }
            catch { }
        }

        private void RestorePositionOrSelection()
        {
            if (!string.IsNullOrEmpty(_savedSelectedPolicyId))
                return;
            RestoreScrollPosition();
        }

        private void EnsureScrollViewerHook()
        {
            try
            {
                if (PolicyList == null) return;
                var sc = FindDescendantScrollViewer(PolicyList);
                if (!object.ReferenceEquals(sc, _policyListScroll))
                {
                    if (_policyListScroll != null)
                        _policyListScroll.ViewChanged -= PolicyScroll_ViewChanged;
                    _policyListScroll = sc;
                    if (_policyListScroll != null)
                    {
                        _lastKnownVerticalOffset = _policyListScroll.VerticalOffset;
                        _policyListScroll.ViewChanged += PolicyScroll_ViewChanged;
                    }
                }
            }
            catch { }
        }

        private void PolicyScroll_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            try { _lastKnownVerticalOffset = _policyListScroll?.VerticalOffset; } catch { }
        }

        private CommunityToolkit.WinUI.UI.Controls.DataGridRow? FindRowContainerForItem(object? item)
        {
            if (item == null || PolicyList == null) return null;
            try { return FindRowContainerRecursive(PolicyList, item); } catch { return null; }
        }

        private CommunityToolkit.WinUI.UI.Controls.DataGridRow? FindRowContainerRecursive(DependencyObject root, object item)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is CommunityToolkit.WinUI.UI.Controls.DataGridRow row)
                {
                    if (ReferenceEquals(row.DataContext, item) || Equals(row.DataContext, item))
                        return row;
                }
                var found = FindRowContainerRecursive(child, item);
                if (found != null) return found;
            }
            return null;
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

        private void TryRestoreSelectionAsync(System.Collections.Generic.IList<object> items)
        {
            if (PolicyList == null) return;
            if (string.IsNullOrEmpty(_savedSelectedPolicyId)) { /* no selected policy to restore */ RestoreScrollPosition(); return; }

            _ = DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(16);
                    object? match = null;
                    foreach (var it in items)
                    {
                        if (it is Models.PolicyListRow row && row.Policy != null && string.Equals(row.Policy.UniqueID, _savedSelectedPolicyId, System.StringComparison.OrdinalIgnoreCase))
                        { match = it; break; }
                    }
                    if (match != null)
                    {
                        PolicyList.SelectedItem = match;
                        EnsureScrollViewerHook();
                        try { PolicyList.ScrollIntoView(match, null); } catch { }

                        await System.Threading.Tasks.Task.Delay(16);
                        var rowContainer = FindRowContainerForItem(match);
                        if (rowContainer != null)
                        {
                            try
                            {
                                if (_savedAnchorRatio.HasValue)
                                {
                                    var opts = new BringIntoViewOptions
                                    {
                                        VerticalAlignmentRatio = _savedAnchorRatio.Value,
                                        AnimationDesired = false
                                    };
                                    rowContainer.StartBringIntoView(opts);
                                }
                                else
                                {
                                    rowContainer.StartBringIntoView();
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        // No matching item; just restore scroll position
                        RestoreScrollPosition();
                    }
                }
                finally
                {
                    _savedSelectedPolicyId = null;
                    _savedAnchorViewportY = null; _savedAnchorRatio = null;
                }
            });
        }

        private void RestoreScrollPosition()
        {
            try
            {
                if (PolicyList == null) return;
                if (!_savedVerticalOffset.HasValue) return;
                double offset = _savedVerticalOffset.Value;

                EnsureScrollViewerHook();
                var sc = _policyListScroll ?? FindDescendantScrollViewer(PolicyList);
                sc?.ChangeView(null, offset, null, true);
            }
            catch { }
        }
    }
}
