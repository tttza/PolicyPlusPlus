using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.WinUI.UI.Controls;
using System;
using System.IO;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Dialogs;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Models; // for PolicyListRow

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
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
                { BtnBack_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (props.IsXButton2Pressed)
                { BtnForward_Click(this, new RoutedEventArgs()); e.Handled = true; }
            }
            catch { }
        }

        private async void BtnLanguage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string defaultPath = Environment.ExpandEnvironmentVariables(@"%WINDIR%\\PolicyDefinitions");
                string admxPath = SettingsService.Instance.LoadSettings().AdmxSourcePath ?? defaultPath;
                string currentLang = SettingsService.Instance.LoadSettings().Language ?? System.Globalization.CultureInfo.CurrentUICulture.Name;

                var dlg = new LanguageOptionsDialog();
                if (this.Content is FrameworkElement root)
                { dlg.XamlRoot = root.XamlRoot; }
                dlg.Initialize(admxPath, currentLang);
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var chosen = dlg.SelectedLanguage;
                    bool langChanged = !string.IsNullOrEmpty(chosen) && !string.Equals(chosen, currentLang, StringComparison.OrdinalIgnoreCase);
                    if (langChanged)
                    { try { SettingsService.Instance.UpdateLanguage(chosen!); } catch { } LoadAdmxFolderAsync(admxPath); }

                    var before = SettingsService.Instance.LoadSettings();
                    bool beforeEnabled = before.SecondLanguageEnabled ?? false;
                    string beforeSecond = before.SecondLanguage ?? "en-US";
                    bool afterEnabled = dlg.SecondLanguageEnabled;
                    string afterSecond = dlg.SelectedSecondLanguage ?? beforeSecond;
                    bool secondChanged = (beforeEnabled != afterEnabled) || !string.Equals(beforeSecond, afterSecond, StringComparison.OrdinalIgnoreCase);
                    try { SettingsService.Instance.UpdateSecondLanguageEnabled(afterEnabled); } catch { }
                    if (afterEnabled && !string.IsNullOrEmpty(afterSecond)) { try { SettingsService.Instance.UpdateSecondLanguage(afterSecond); } catch { } }

                    if (secondChanged && !langChanged)
                    { RebuildSearchIndex(); }
                    ApplySecondLanguageVisibilityToViewMenu();
                    UpdateColumnVisibilityFromFlags();
                    if (!langChanged) RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
            catch { ShowInfo("Unable to open Language dialog.", InfoBarSeverity.Error); }
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

        private void PolicyList_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            try
            {
                string? key = null;
                if (e.Column == ColName) key = nameof(PolicyListRow.DisplayName);
                else if (e.Column == ColId) key = nameof(PolicyListRow.ShortId);
                else if (e.Column == ColCategory) key = nameof(PolicyListRow.CategoryName);
                else if (e.Column == ColTopCategory) key = nameof(PolicyListRow.TopCategoryName);
                else if (e.Column == ColCategoryPath) key = nameof(PolicyListRow.CategoryFullPath);
                else if (e.Column == ColApplies) key = nameof(PolicyListRow.AppliesText);
                else if (e.Column == ColSupported) key = nameof(PolicyListRow.SupportedText);
                if (string.IsNullOrEmpty(key)) return;

                if (string.Equals(_sortColumn, key, StringComparison.Ordinal))
                {
                    if (_sortDirection == DataGridSortDirection.Ascending) _sortDirection = DataGridSortDirection.Descending;
                    else if (_sortDirection == DataGridSortDirection.Descending) { _sortColumn = null; _sortDirection = null; }
                    else _sortDirection = DataGridSortDirection.Ascending;
                }
                else { _sortColumn = key; _sortDirection = DataGridSortDirection.Ascending; }

                try
                {
                    if (_sortColumn == null || _sortDirection == null) SettingsService.Instance.UpdateSort(null, null);
                    else SettingsService.Instance.UpdateSort(_sortColumn, _sortDirection == DataGridSortDirection.Descending ? "Desc" : "Asc");
                }
                catch { }

                foreach (var col in PolicyList.Columns) col.SortDirection = null;
                if (_sortColumn != null && _sortDirection != null)
                {
                    if (_sortColumn == nameof(PolicyListRow.DisplayName)) ColName.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.ShortId)) ColId.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.CategoryName)) ColCategory.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.TopCategoryName)) ColTopCategory.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.CategoryFullPath)) ColCategoryPath.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.AppliesText)) ColApplies.SortDirection = _sortDirection;
                    else if (_sortColumn == nameof(PolicyListRow.SupportedText)) ColSupported.SortDirection = _sortDirection;
                }

                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            }
            catch { }
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == global::Windows.System.VirtualKey.Enter)
            {
                try
                {
                    if (PolicyList?.SelectedItem is PolicyListRow row && row.Policy is not null)
                    {
                        e.Handled = true;
                        _ = OpenEditDialogForPolicyInternalAsync(row.Policy, ensureFront: true);
                    }
                    else if (PolicyList?.SelectedItem is PolicyListRow row2 && row2.Category is not null)
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
    }
}
