using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using PolicyPlus.WinUI3.Services;
using CommunityToolkit.WinUI.UI.Controls;
using PolicyPlus.WinUI3.Models;
using System.Collections.Generic;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private readonly string[] _columnKeys = new[] { "CatIcon", "UserIcon", "ComputerIcon", "Name", "SecondName", "Id", "Category", "Applies", "Supported" };

        private void ApplyDetailsPaneVisibility()
        {
            try
            {
                if (DetailsPane == null || DetailRow == null || DetailsSplitter == null || SplitterRow == null) return;

                if (_showDetails)
                {
                    DetailsPane.Visibility = Visibility.Visible;
                    DetailsSplitter.Visibility = Visibility.Visible;
                    // If we have a previously saved star height, keep it; otherwise use a reasonable default ratio
                    if (!_savedDetailRowHeight.HasValue || _savedDetailRowHeight.Value.Value <= 0)
                        _savedDetailRowHeight = new GridLength(0.3, GridUnitType.Star);

                    DetailRow.Height = _savedDetailRowHeight.Value;
                    SplitterRow.Height = _savedSplitterRowHeight ?? new GridLength(8);
                }
                else
                {
                    if (DetailRow.Height.IsStar && DetailRow.Height.Value > 0)
                        _savedDetailRowHeight = DetailRow.Height;
                    if (SplitterRow.Height.Value > 0)
                        _savedSplitterRowHeight = SplitterRow.Height;

                    DetailsPane.Visibility = Visibility.Collapsed;
                    DetailsSplitter.Visibility = Visibility.Collapsed;
                    DetailRow.Height = new GridLength(0);
                    SplitterRow.Height = new GridLength(0);
                }
            }
            catch { }
        }

        private void ApplySavedDetailPaneRatioIfAny()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                if (!s.DetailPaneHeightStar.HasValue || PolicyDetailGrid == null || DetailRow == null || SplitterRow == null) return;

                double v = s.DetailPaneHeightStar.Value;
                // Treat values in [0,1] as ratio; legacy values (>1) as star units for DetailRow only
                if (v > 0 && v <= 1.0)
                {
                    double r = Math.Clamp(v, 0.05, 0.95);
                    var topStar = 1.0 - r;
                    var botStar = r;
                    // Apply both rows as star so the ratio is preserved
                    if (PolicyDetailGrid.RowDefinitions.Count >= 3)
                    {
                        var topRow = PolicyDetailGrid.RowDefinitions[0];
                        topRow.Height = new GridLength(topStar, GridUnitType.Star);
                        SplitterRow.Height = new GridLength(8);
                        DetailRow.Height = new GridLength(botStar, GridUnitType.Star);
                        _savedDetailRowHeight = DetailRow.Height;
                    }
                }
                else if (v > 1.0)
                {
                    DetailRow.Height = new GridLength(v, GridUnitType.Star);
                    _savedDetailRowHeight = DetailRow.Height;
                }
            }
            catch { }
        }

        private void LoadColumnPrefs()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                var cols = s.Columns ?? new ColumnsOptions();

                // Reflect into View menu toggles first
                if (ViewIdToggle != null) ViewIdToggle.IsChecked = cols.ShowId;
                if (ViewCategoryToggle != null) ViewCategoryToggle.IsChecked = cols.ShowCategory;
                if (ViewAppliesToggle != null) ViewAppliesToggle.IsChecked = cols.ShowApplies;
                if (ViewSupportedToggle != null) ViewSupportedToggle.IsChecked = cols.ShowSupported;

                // Second language column is governed by language option
                if (ViewSecondNameToggle != null)
                {
                    bool enabled = s.SecondLanguageEnabled ?? false;
                    ViewSecondNameToggle.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                    ViewSecondNameToggle.IsChecked = enabled && cols.ShowEnglishName;
                }

                ApplyColumnVisibilityFromToggles();

                // Apply saved order and widths if present
                ApplySavedColumnLayout();

                HookColumnLayoutEvents();
            }
            catch { }
        }

        private void HookColumnLayoutEvents()
        {
            try
            {
                if (PolicyList == null) return;
                PolicyList.ColumnReordered -= PolicyList_ColumnReordered;
                PolicyList.ColumnReordered += PolicyList_ColumnReordered;
                PolicyList.LayoutUpdated -= PolicyList_LayoutUpdated;
                PolicyList.LayoutUpdated += PolicyList_LayoutUpdated;
            }
            catch { }
        }

        private void PolicyList_LayoutUpdated(object? sender, object e)
        {
            // Save widths opportunistically after layout settles
            SaveColumnLayout(includeOrder: false);
        }

        private void PolicyList_ColumnReordered(object? sender, DataGridColumnEventArgs e)
        {
            SaveColumnLayout(includeOrder: true);
        }

        private void SaveColumnLayout(bool includeOrder)
        {
            try
            {
                if (PolicyList == null) return;

                var existing = SettingsService.Instance.LoadColumnLayout().ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
                var list = new List<ColumnState>();
                foreach (var col in PolicyList.Columns)
                {
                    string key = GetKeyForColumn(col);
                    if (string.IsNullOrEmpty(key)) continue;

                    int index = col.DisplayIndex;
                    if (!includeOrder && existing.TryGetValue(key, out var prev)) index = prev.Index;

                    var state = new ColumnState
                    {
                        Key = key,
                        Index = index,
                        Width = col.ActualWidth,
                        Visible = col.Visibility == Visibility.Visible
                    };
                    list.Add(state);
                }
                SettingsService.Instance.UpdateColumnLayout(list);
            }
            catch { }
        }

        private string GetKeyForColumn(DataGridColumn col)
        {
            if (col == ColCatIcon) return "CatIcon";
            if (col == ColUserIcon) return "UserIcon";
            if (col == ColComputerIcon) return "ComputerIcon";
            if (col == ColName) return "Name";
            if (col == ColSecondName) return "SecondName";
            if (col == ColId) return "Id";
            if (col == ColCategory) return "Category";
            if (col == ColApplies) return "Applies";
            if (col == ColSupported) return "Supported";
            return string.Empty;
        }

        private DataGridColumn? GetColumnByKey(string key)
        {
            return key switch
            {
                "CatIcon" => ColCatIcon,
                "UserIcon" => ColUserIcon,
                "ComputerIcon" => ColComputerIcon,
                "Name" => ColName,
                "SecondName" => ColSecondName,
                "Id" => ColId,
                "Category" => ColCategory,
                "Applies" => ColApplies,
                "Supported" => ColSupported,
                _ => null
            };
        }

        private void ApplySavedColumnLayout()
        {
            try
            {
                var states = SettingsService.Instance.LoadColumnLayout();
                if (states == null || states.Count == 0 || PolicyList == null) return;

                // Apply visibility first (user-toggleable columns)
                foreach (var st in states)
                {
                    var col = GetColumnByKey(st.Key);
                    if (col == null) continue;
                    if (col == ColId || col == ColCategory || col == ColApplies || col == ColSupported || col == ColSecondName)
                    {
                        col.Visibility = st.Visible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }

                // Apply widths
                foreach (var st in states)
                {
                    var col = GetColumnByKey(st.Key);
                    if (col == null) continue;
                    if (st.Width > 0)
                    {
                        try { col.Width = new DataGridLength(st.Width); } catch { }
                    }
                }

                // Reorder all columns by saved Index (including 0)
                var orderedKeys = states
                    .Where(s => !string.IsNullOrEmpty(s.Key))
                    .OrderBy(s => s.Index)
                    .Select(s => s.Key)
                    .ToList();

                // Append any columns not present in saved states in current order
                var currentOrder = PolicyList.Columns.Select(GetKeyForColumn).Where(k => !string.IsNullOrEmpty(k)).ToList();
                foreach (var k in currentOrder)
                {
                    if (!orderedKeys.Contains(k)) orderedKeys.Add(k);
                }

                int i = 0;
                foreach (var key in orderedKeys)
                {
                    var col = GetColumnByKey(key);
                    if (col == null) continue;
                    try { col.DisplayIndex = i++; } catch { }
                }

                UpdateColumnMenuChecks();
            }
            catch { }
        }

        private void SaveColumnPrefs()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();

                bool showId = ViewIdToggle?.IsChecked == true;
                bool showCategory = ViewCategoryToggle?.IsChecked == true;
                bool showApplies = ViewAppliesToggle?.IsChecked == true;
                bool showSupported = ViewSupportedToggle?.IsChecked == true;
                bool showSecondName = ViewSecondNameToggle?.IsChecked == true;

                var cols = new ColumnsOptions
                {
                    ShowId = showId,
                    ShowCategory = showCategory,
                    ShowApplies = showApplies,
                    ShowSupported = showSupported,
                    ShowEnglishName = showSecondName,
                    ShowUserState = s.Columns?.ShowUserState ?? true,
                    ShowComputerState = s.Columns?.ShowComputerState ?? true,
                };

                SettingsService.Instance.UpdateColumns(cols);

                // Save layout snapshot including order
                SaveColumnLayout(includeOrder: true);
            }
            catch { }
        }

        private void UpdateColumnMenuChecks()
        {
            try
            {
                // Keep menu toggles in sync with actual visibility
                if (ViewIdToggle != null) ViewIdToggle.IsChecked = ColId?.Visibility == Visibility.Visible;
                if (ViewCategoryToggle != null) ViewCategoryToggle.IsChecked = ColCategory?.Visibility == Visibility.Visible;
                if (ViewAppliesToggle != null) ViewAppliesToggle.IsChecked = ColApplies?.Visibility == Visibility.Visible;
                if (ViewSupportedToggle != null) ViewSupportedToggle.IsChecked = ColSupported?.Visibility == Visibility.Visible;

                var s = SettingsService.Instance.LoadSettings();
                bool enabled = s.SecondLanguageEnabled ?? false;
                if (ViewSecondNameToggle != null)
                {
                    ViewSecondNameToggle.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                    ViewSecondNameToggle.IsChecked = enabled && (ColSecondName?.Visibility == Visibility.Visible);
                }
            }
            catch { }
        }

        private void ApplyColumnVisibilityFromToggles()
        {
            try
            {
                if (ColId != null) ColId.Visibility = (ViewIdToggle?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColCategory != null) ColCategory.Visibility = (ViewCategoryToggle?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColApplies != null) ColApplies.Visibility = (ViewAppliesToggle?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColSupported != null) ColSupported.Visibility = (ViewSupportedToggle?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

                var s = SettingsService.Instance.LoadSettings();
                bool secondEnabled = s.SecondLanguageEnabled ?? false;
                if (ColSecondName != null)
                {
                    ColSecondName.Visibility = (secondEnabled && (ViewSecondNameToggle?.IsChecked == true)) ? Visibility.Visible : Visibility.Collapsed;
                }

                UpdateColumnMenuChecks();
            }
            catch { }
        }

        private void UpdateColumnVisibilityFromFlags()
        {
            // Backward compatibility shim: keep calling path updates via toggles
            ApplyColumnVisibilityFromToggles();
        }

        private void ApplyTheme(string pref)
        {
            if (RootGrid == null) return;
            var theme = pref switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
            RootGrid.RequestedTheme = theme;
            App.SetGlobalTheme(theme);
            try { SettingsService.Instance.UpdateTheme(pref); } catch { }
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = ThemeSelector as ComboBox;
            var item = ((cb?.SelectedItem as ComboBoxItem)?.Content?.ToString());
            var pref = item ?? "System";
            ApplyTheme(pref);
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
                try { SettingsService.Instance.UpdateScale(s); } catch { }
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

        private void ApplySecondLanguageVisibilityToViewMenu()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                bool enabled = s.SecondLanguageEnabled ?? false;
                string lang = s.SecondLanguage ?? "en-US";
                if (ViewSecondNameToggle != null)
                {
                    ViewSecondNameToggle.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                    ViewSecondNameToggle.Text = enabled ? $"2nd Language Name ({lang})" : "2nd Language Name";
                    if (!enabled) ViewSecondNameToggle.IsChecked = false;
                }
                if (ColSecondName != null)
                {
                    ColSecondName.Header = enabled ? $"2nd Language Name ({lang})" : "2nd Language Name";
                    ColSecondName.Visibility = (enabled && (ViewSecondNameToggle?.IsChecked == true)) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void ViewSecoundNameToggle_Click(object sender, RoutedEventArgs e)
        {
            SaveColumnPrefs();
            ApplyColumnVisibilityFromToggles();
        }

        private void CategorySplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (CategoryColumn != null)
                {
                    double w = CategoryColumn.ActualWidth;
                    if (w > 0) SettingsService.Instance.UpdateCategoryPaneWidth(w);
                }
            }
            catch { }
        }

        private void DetailsSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (PolicyDetailGrid != null && DetailRow != null && SplitterRow != null)
                {
                    double total = 0;
                    if (PolicyDetailGrid.RowDefinitions.Count >= 3)
                        total = PolicyDetailGrid.RowDefinitions[0].ActualHeight + SplitterRow.ActualHeight + DetailRow.ActualHeight;
                    else
                        total = PolicyDetailGrid.ActualHeight;

                    double available = Math.Max(1, total - SplitterRow.ActualHeight);
                    double ratio = Math.Clamp(DetailRow.ActualHeight / available, 0.05, 0.95);

                    _savedDetailRowHeight = new GridLength(ratio, GridUnitType.Star);
                    SettingsService.Instance.UpdateDetailPaneHeightStar(ratio);
                }
            }
            catch { }
        }

        private void ApplyPersistedLayout()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                if (s.CategoryPaneWidth.HasValue && CategoryColumn != null)
                {
                    double w = Math.Max(140, s.CategoryPaneWidth.Value);
                    CategoryColumn.Width = new GridLength(w);
                }

                // Apply detail pane height (ratio or legacy star)
                if (s.DetailPaneHeightStar.HasValue && PolicyDetailGrid != null)
                {
                    double v = s.DetailPaneHeightStar.Value;
                    if (v > 0 && v <= 1.0)
                    {
                        double r = Math.Clamp(v, 0.05, 0.95);
                        var topRow = PolicyDetailGrid.RowDefinitions[0];
                        topRow.Height = new GridLength(1.0 - r, GridUnitType.Star);
                        SplitterRow.Height = new GridLength(8);
                        DetailRow.Height = new GridLength(r, GridUnitType.Star);
                        _savedDetailRowHeight = DetailRow.Height;
                    }
                    else if (v > 1.0)
                    {
                        DetailRow.Height = new GridLength(v, GridUnitType.Star);
                        _savedDetailRowHeight = DetailRow.Height;
                    }
                }

                // Restore sorting
                _sortColumn = s.SortColumn;
                if (string.Equals(s.SortDirection, "Asc", StringComparison.OrdinalIgnoreCase))
                    _sortDirection = DataGridSortDirection.Ascending;
                else if (string.Equals(s.SortDirection, "Desc", StringComparison.OrdinalIgnoreCase))
                    _sortDirection = DataGridSortDirection.Descending;
                else
                    _sortDirection = null;

                if (!string.IsNullOrEmpty(_sortColumn) && PolicyList != null)
                {
                    foreach (var col in PolicyList.Columns) col.SortDirection = null;
                    if (_sortDirection != null)
                    {
                        if (_sortColumn == nameof(PolicyListRow.DisplayName)) ColName.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.ShortId)) ColId.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.CategoryName)) ColCategory.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.AppliesText)) ColApplies.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.SupportedText)) ColSupported.SortDirection = _sortDirection;
                    }
                }
            }
            catch { }
        }
    }
}
