using System;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlusPlus.Dialogs;
using PolicyPlusPlus.Models; // for PolicyListRow
using PolicyPlusPlus.Services;
using Windows.System;
// Alias to avoid namespace resolution issues inside PolicyPlusPlus namespace
using CoreKeyStates = global::Windows.UI.Core.CoreVirtualKeyStates;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private void GoBackAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnBack_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void GoForwardAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnForward_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

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

        private void ClearCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            _selectedCategory = null;
            if (CategoryTree != null)
            {
                _suppressCategorySelectionChanged = true;
                var old = CategoryTree.SelectionMode;
                CategoryTree.SelectionMode = TreeViewSelectionMode.None;
                BuildCategoryTree();
                CategoryTree.SelectionMode = old;
                _suppressCategorySelectionChanged = false;
            }
            UpdateSearchPlaceholder();
            _navTyping = false;
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            UpdateNavButtons();
        }

        private void HintDisableConfiguredOnly_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _configuredOnly = false;
                if (ChkConfiguredOnly != null)
                    ChkConfiguredOnly.IsChecked = false;
                try
                {
                    SettingsService.Instance.UpdateConfiguredOnly(false);
                }
                catch { }
                RebindConsideringAsync(GetSearchBox()?.Text ?? string.Empty);
            }
            catch { }
        }

        private void HintDisableBookmarksOnly_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _bookmarksOnly = false;
                if (ChkBookmarksOnly != null)
                {
                    _suppressBookmarksOnlyChanged = true;
                    ChkBookmarksOnly.IsChecked = false;
                    _suppressBookmarksOnlyChanged = false;
                }
                try
                {
                    SettingsService.Instance.UpdateBookmarksOnly(false);
                }
                catch { }
                RebindConsideringAsync(GetSearchBox()?.Text ?? string.Empty);
            }
            catch { }
        }

        private void HintClearAllFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool any = false;
                if (_configuredOnly)
                {
                    _configuredOnly = false;
                    if (ChkConfiguredOnly != null)
                        ChkConfiguredOnly.IsChecked = false;
                    try
                    {
                        SettingsService.Instance.UpdateConfiguredOnly(false);
                    }
                    catch { }
                    any = true;
                }
                if (_bookmarksOnly)
                {
                    _bookmarksOnly = false;
                    if (ChkBookmarksOnly != null)
                    {
                        _suppressBookmarksOnlyChanged = true;
                        ChkBookmarksOnly.IsChecked = false;
                        _suppressBookmarksOnlyChanged = false;
                    }
                    try
                    {
                        SettingsService.Instance.UpdateBookmarksOnly(false);
                    }
                    catch { }
                    any = true;
                }
                if (_selectedCategory != null)
                {
                    _selectedCategory = null;
                    if (CategoryTree != null)
                    {
                        _suppressCategorySelectionChanged = true;
                        var old = CategoryTree.SelectionMode;
                        CategoryTree.SelectionMode = TreeViewSelectionMode.None;
                        BuildCategoryTree();
                        CategoryTree.SelectionMode = old;
                        _suppressCategorySelectionChanged = false;
                    }
                    any = true;
                }
                if (any)
                {
                    UpdateSearchPlaceholder();
                    _navTyping = false;
                    RebindConsideringAsync(GetSearchBox()?.Text ?? string.Empty);
                    UpdateNavButtons();
                }
            }
            catch { }
        }

        private void PolicyList_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            try
            {
                string? key = null;
                if (e.Column == ColName)
                    key = nameof(PolicyListRow.DisplayName);
                else if (e.Column == ColId)
                    key = nameof(PolicyListRow.ShortId);
                else if (e.Column == ColCategory)
                    key = nameof(PolicyListRow.CategoryName);
                else if (e.Column == ColTopCategory)
                    key = nameof(PolicyListRow.TopCategoryName);
                else if (e.Column == ColCategoryPath)
                    key = nameof(PolicyListRow.CategoryFullPath);
                else if (e.Column == ColApplies)
                    key = nameof(PolicyListRow.AppliesText);
                else if (e.Column == ColSupported)
                    key = nameof(PolicyListRow.SupportedText);
                if (string.IsNullOrEmpty(key))
                    return;

                if (string.Equals(_sortColumn, key, StringComparison.Ordinal))
                {
                    if (_sortDirection == DataGridSortDirection.Ascending)
                        _sortDirection = DataGridSortDirection.Descending;
                    else if (_sortDirection == DataGridSortDirection.Descending)
                    {
                        _sortColumn = null;
                        _sortDirection = null;
                    }
                    else
                        _sortDirection = DataGridSortDirection.Ascending;
                }
                else
                {
                    _sortColumn = key;
                    _sortDirection = DataGridSortDirection.Ascending;
                }

                try
                {
                    if (_sortColumn == null || _sortDirection == null)
                        SettingsService.Instance.UpdateSort(null, null);
                    else
                        SettingsService.Instance.UpdateSort(
                            _sortColumn,
                            _sortDirection == DataGridSortDirection.Descending ? "Desc" : "Asc"
                        );
                }
                catch { }

                foreach (var col in PolicyList.Columns)
                    col.SortDirection = null;
                if (_sortColumn != null && _sortDirection != null)
                {
                    if (_sortColumn == nameof(PolicyListRow.DisplayName))
                        ColName.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.ShortId))
                        ColId.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.CategoryName))
                        ColCategory.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.TopCategoryName))
                        ColTopCategory.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.CategoryFullPath))
                        ColCategoryPath.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.AppliesText))
                        ColApplies.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.SupportedText))
                        ColSupported.SortDirection = _sortDirection;
                }

                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            }
            catch { }
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Type-to-search: when user types A-Z anywhere, focus SearchBox and start typing immediately.
            try
            {
                var key = e.Key;
                bool isLetter = key >= VirtualKey.A && key <= VirtualKey.Z;
                if (isLetter)
                {
                    // Ignore when Alt is down or Control is down (don't steal accelerators)
                    bool altDown = e.KeyStatus.IsMenuKeyDown;
                    bool ctrlDown =
                        (
                            InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                            & CoreKeyStates.Down
                        ) == CoreKeyStates.Down;
                    if (!altDown && !ctrlDown)
                    {
                        // Do not steal keys while a text input control already has focus (including inside SearchBox)
                        // Use XamlRoot-aware focus retrieval; plain GetFocusedElement() can be null in WinUI 3 desktop.
                        DependencyObject? focused = null;
                        try
                        {
                            var xr = this.Content is FrameworkElement fe ? fe.XamlRoot : null;
                            focused =
                                (
                                    xr != null
                                        ? FocusManager.GetFocusedElement(xr)
                                        : FocusManager.GetFocusedElement()
                                ) as DependencyObject;
                        }
                        catch (Exception ex)
                        {
                            Logging.Log.Debug(
                                "MainWindow",
                                "GetFocusedElement failed: " + ex.Message
                            );
                        }
                        if (focused == null)
                        {
                            // Fallback to event source if FocusManager did not return a focused element.
                            focused = e.OriginalSource as DependencyObject;
                            Logging.Log.Debug(
                                "MainWindow",
                                "Focused element null; using OriginalSource fallback"
                            );
                        }
                        bool inSearchBox = SearchBox != null && IsWithin(focused, SearchBox);
                        bool focusedIsTextInput =
                            focused is TextBox || focused is RichEditBox || focused is PasswordBox;
                        if (!inSearchBox && !focusedIsTextInput && SearchBox != null)
                        {
                            // Focus the search box and place caret at the end; let the original key input type the first character.
                            try
                            {
                                _suppressInitialSearchBoxFocus = false; // user started typing; allow search focus
                                SearchBox.Focus(FocusState.Keyboard);
                            }
                            catch (Exception ex)
                            {
                                Logging.Log.Warn("MainWindow", "SearchBox.Focus failed", ex);
                            }
                            try
                            {
                                var innerTb = FindDescendantByName(SearchBox, "TextBox") as TextBox;
                                if (innerTb != null)
                                {
                                    int len = innerTb.Text?.Length ?? 0;
                                    innerTb.SelectionStart = len;
                                    innerTb.SelectionLength = 0;
                                }
                                else
                                {
                                    Logging.Log.Debug(
                                        "MainWindow",
                                        "inner TextBox not found in SearchBox"
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                Logging.Log.Debug(
                                    "MainWindow",
                                    "SearchBox caret move failed: " + ex.Message
                                );
                            }
                            // Do not mark handled: let this key press flow into the now-focused search box so the first character appears once.
                            return;
                        }
                    }
                }
            }
            catch { }

            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                try
                {
                    if (PolicyList?.SelectedItem is PolicyListRow row && row.Policy is not null)
                    {
                        e.Handled = true;
                        _ = OpenEditDialogForPolicyInternalAsync(row.Policy, ensureFront: true);
                    }
                    else if (
                        PolicyList?.SelectedItem is PolicyListRow row2
                        && row2.Category is not null
                    )
                    {
                        e.Handled = true;
                        _selectedCategory = row2.Category;
                        UpdateSearchPlaceholder();
                        _navTyping = false;
                        RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                        UpdateNavButtons();
                    }
                }
                catch { }
            }
        }

        private static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
        {
            // Returns true if 'node' is within the visual tree subtree of 'ancestor'.
            var current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
