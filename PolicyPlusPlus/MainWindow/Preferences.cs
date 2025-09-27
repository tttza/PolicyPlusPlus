using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PolicyPlusPlus.Models;
using PolicyPlusPlus.Services;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private static readonly string[] DefaultColumnOrder = new[]
        {
            "Bookmark",
            "CatIcon",
            "UserIcon",
            "ComputerIcon",
            "Name",
            "SecondName",
            "Id",
            "Category",
            "TopCategory",
            "CategoryPath",
            "Applies",
            "Supported",
        };

        private double? GetCurrentDetailRatio()
        {
            try
            {
                if (PolicyDetailGrid == null || DetailRow == null || SplitterRow == null)
                    return null;
                if (PolicyDetailGrid.RowDefinitions.Count < 3)
                    return null;
                var top = PolicyDetailGrid.RowDefinitions[0];
                if (
                    top.Height.IsStar
                    && DetailRow.Height.IsStar
                    && top.Height.Value > 0
                    && DetailRow.Height.Value > 0
                )
                {
                    return Math.Clamp(
                        DetailRow.Height.Value / (top.Height.Value + DetailRow.Height.Value),
                        0.05,
                        0.95
                    );
                }
            }
            catch { }
            return null;
        }

        private void ApplyRatio(double ratio)
        {
            if (PolicyDetailGrid == null || DetailRow == null || SplitterRow == null)
                return;
            ratio = Math.Clamp(ratio, 0.05, 0.95);
            if (PolicyDetailGrid.RowDefinitions.Count >= 3)
            {
                var topRow = PolicyDetailGrid.RowDefinitions[0];
                topRow.Height = new GridLength(1.0 - ratio, GridUnitType.Star);
                DetailRow.Height = new GridLength(ratio, GridUnitType.Star);
                SplitterRow.Height = new GridLength(8);
            }
        }

        private void HideDetailsPane()
        {
            try
            {
                var ratio = GetCurrentDetailRatio();
                if (ratio.HasValue)
                    _savedDetailRowHeight = new GridLength(ratio.Value, GridUnitType.Star);
                if (SplitterRow?.Height.Value > 0)
                    _savedSplitterRowHeight = SplitterRow.Height;
                if (PolicyDetailGrid?.RowDefinitions.Count >= 3)
                {
                    var topRow = PolicyDetailGrid.RowDefinitions[0];
                    topRow.Height = new GridLength(1.0, GridUnitType.Star);
                }
                if (DetailRow != null)
                    DetailRow.Height = new GridLength(0);
                if (SplitterRow != null)
                    SplitterRow.Height = new GridLength(0);
                if (DetailsPane != null)
                    DetailsPane.Visibility = Visibility.Collapsed;
                if (DetailsSplitter != null)
                    DetailsSplitter.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private void ShowDetailsPane()
        {
            try
            {
                if (DetailsPane != null)
                    DetailsPane.Visibility = Visibility.Visible;
                if (DetailsSplitter != null)
                    DetailsSplitter.Visibility = Visibility.Visible;
                // Lazy-load saved ratio/star height if not yet cached
                if (!_savedDetailRowHeight.HasValue)
                {
                    try
                    {
                        var s = SettingsService.Instance.LoadSettings();
                        if (s.DetailPaneHeightStar.HasValue)
                        {
                            double v = s.DetailPaneHeightStar.Value;
                            if (v > 0 && v <= 1.0)
                                _savedDetailRowHeight = new GridLength(v, GridUnitType.Star);
                            else if (v > 1.0)
                                _savedDetailRowHeight = new GridLength(v, GridUnitType.Star);
                        }
                    }
                    catch { }
                }
                double ratio;
                if (
                    _savedDetailRowHeight.HasValue
                    && _savedDetailRowHeight.Value.IsStar
                    && _savedDetailRowHeight.Value.Value > 0
                    && _savedDetailRowHeight.Value.Value <= 1.0
                )
                {
                    ratio = _savedDetailRowHeight.Value.Value;
                }
                else if (
                    _savedDetailRowHeight.HasValue
                    && _savedDetailRowHeight.Value.IsStar
                    && _savedDetailRowHeight.Value.Value > 1.0
                )
                {
                    // Legacy star value: convert to ratio using XAML original top (8*) approx mapping; best-effort
                    double legacyBottom = _savedDetailRowHeight.Value.Value;
                    ratio = legacyBottom / (8.0 + legacyBottom);
                }
                else
                {
                    ratio = 0.33; // default
                }
                ApplyRatio(ratio);
            }
            catch { }
        }

        private void ApplyDetailsPaneVisibility()
        {
            try
            {
                if (
                    DetailRow == null
                    || SplitterRow == null
                    || PolicyDetailGrid == null
                    || DetailsPane == null
                    || DetailsSplitter == null
                )
                    return;
                if (_showDetails)
                    ShowDetailsPane();
                else
                    HideDetailsPane();
                PolicyDetailGrid.UpdateLayout();
            }
            catch { }
        }

        // Legacy method removed: logic folded into ShowDetailsPane()

        private void LoadColumnPrefs()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                var cols = s.Columns ?? new ColumnsOptions();
                if (ViewIdToggle != null)
                    ViewIdToggle.IsChecked = cols.ShowId;
                if (ViewCategoryToggle != null)
                    ViewCategoryToggle.IsChecked = cols.ShowCategory;
                if (ViewTopCategoryToggle != null)
                    ViewTopCategoryToggle.IsChecked = cols.ShowTopCategory;
                if (ViewCategoryPathToggle != null)
                    ViewCategoryPathToggle.IsChecked = cols.ShowCategoryPath;
                if (ViewAppliesToggle != null)
                    ViewAppliesToggle.IsChecked = cols.ShowApplies;
                if (ViewSupportedToggle != null)
                    ViewSupportedToggle.IsChecked = cols.ShowSupported;
                if (ViewUserStateToggle != null)
                    ViewUserStateToggle.IsChecked = cols.ShowUserState;
                if (ViewComputerStateToggle != null)
                    ViewComputerStateToggle.IsChecked = cols.ShowComputerState;
                if (RootGrid?.FindName("ViewBookmarkToggle") is ToggleMenuFlyoutItem vbt)
                    vbt.IsChecked = cols.ShowBookmark;
                if (ViewSecondNameToggle != null)
                {
                    // Preserve user preference even if second language currently disabled so it can auto-show once enabled.
                    ViewSecondNameToggle.IsChecked = cols.ShowSecondName;
                }
                ApplyColumnVisibilityFromToggles();
            }
            catch { }
        }

        private void HookColumnLayoutEvents()
        {
            try
            {
                if (PolicyList == null)
                    return;
                PolicyList.ColumnReordered -= PolicyList_ColumnReordered;
                PolicyList.ColumnReordered += PolicyList_ColumnReordered;
                PolicyList.LayoutUpdated -= PolicyList_LayoutUpdated;
                PolicyList.LayoutUpdated += PolicyList_LayoutUpdated;
            }
            catch { }
        }

        private void PolicyList_LayoutUpdated(object? sender, object e)
        {
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
                if (PolicyList == null)
                    return;
                var existing = SettingsService
                    .Instance.LoadColumnLayout()
                    .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
                var list = new List<ColumnState>();
                foreach (var col in PolicyList.Columns)
                {
                    string key = GetKeyForColumn(col);
                    if (string.IsNullOrEmpty(key))
                        continue;
                    if (col.ActualWidth <= 1)
                        continue; // skip not fully measured
                    int index = col.DisplayIndex;
                    if (!includeOrder && existing.TryGetValue(key, out var prev))
                        index = prev.Index;
                    list.Add(
                        new ColumnState
                        {
                            Key = key,
                            Index = index,
                            Width = col.ActualWidth,
                            Visible = col.Visibility == Visibility.Visible,
                        }
                    );
                }
                if (list.Count > 0)
                    SettingsService.Instance.UpdateColumnLayout(list);
            }
            catch { }
        }

        private string GetKeyForColumn(DataGridColumn col)
        {
            if (col == ColBookmark)
                return "Bookmark";
            if (col == ColCatIcon)
                return "CatIcon";
            if (col == ColUserIcon)
                return "UserIcon";
            if (col == ColComputerIcon)
                return "ComputerIcon";
            if (col == ColName)
                return "Name";
            if (col == ColSecondName)
                return "SecondName";
            if (col == ColId)
                return "Id";
            if (col == ColCategory)
                return "Category";
            if (col == ColTopCategory)
                return "TopCategory";
            if (col == ColCategoryPath)
                return "CategoryPath";
            if (col == ColApplies)
                return "Applies";
            if (col == ColSupported)
                return "Supported";
            return string.Empty;
        }

        private DataGridColumn? GetColumnByKey(string key)
        {
            return key switch
            {
                "Bookmark" => ColBookmark,
                "CatIcon" => ColCatIcon,
                "UserIcon" => ColUserIcon,
                "ComputerIcon" => ColComputerIcon,
                "Name" => ColName,
                "SecondName" => ColSecondName,
                "Id" => ColId,
                "Category" => ColCategory,
                "TopCategory" => ColTopCategory,
                "CategoryPath" => ColCategoryPath,
                "Applies" => ColApplies,
                "Supported" => ColSupported,
                _ => null,
            };
        }

        private void ApplySavedColumnLayout()
        {
            try
            {
                var states = SettingsService.Instance.LoadColumnLayout();
                if (PolicyList == null)
                    return;

                // No persisted layout -> apply default order (Bookmark first) and exit
                if (states == null || states.Count == 0)
                {
                    int defIndex = 0;
                    foreach (var key in DefaultColumnOrder)
                    {
                        var col = GetColumnByKey(key);
                        if (col == null)
                            continue;
                        try
                        {
                            col.DisplayIndex = defIndex++;
                        }
                        catch { }
                    }
                    UpdateColumnMenuChecks();
                    return;
                }

                // Apply visibility for toggleable columns (skip Bookmark which has its own toggle)
                foreach (var st in states)
                {
                    var col = GetColumnByKey(st.Key);
                    if (col == null)
                        continue;
                    if (
                        col == ColId
                        || col == ColCategory
                        || col == ColTopCategory
                        || col == ColCategoryPath
                        || col == ColApplies
                        || col == ColSupported
                        || col == ColSecondName
                    )
                        col.Visibility = st.Visible ? Visibility.Visible : Visibility.Collapsed;
                }

                // Apply widths
                foreach (var st in states)
                {
                    var col = GetColumnByKey(st.Key);
                    if (col == null)
                        continue;
                    if (st.Width > 0)
                    {
                        try
                        {
                            col.Width = new DataGridLength(st.Width);
                        }
                        catch { }
                    }
                }

                // Start with saved order
                var orderedKeys = states
                    .Where(s => !string.IsNullOrEmpty(s.Key))
                    .OrderBy(s => s.Index)
                    .Select(s => s.Key)
                    .ToList();

                // Append any missing columns using default order (predictable) instead of current visual order
                foreach (var key in DefaultColumnOrder)
                {
                    if (!orderedKeys.Contains(key))
                        orderedKeys.Add(key);
                }

                int i = 0;
                foreach (var key in orderedKeys)
                {
                    var col = GetColumnByKey(key);
                    if (col == null)
                        continue;
                    try
                    {
                        col.DisplayIndex = i++;
                    }
                    catch { }
                }

                UpdateColumnMenuChecks();
            }
            catch { }
        }

        private void SaveColumnPrefs()
        {
            try
            {
                var cols = new ColumnsOptions
                {
                    ShowId = ViewIdToggle?.IsChecked == true,
                    ShowCategory = ViewCategoryToggle?.IsChecked == true,
                    ShowTopCategory = ViewTopCategoryToggle?.IsChecked == true,
                    ShowCategoryPath = ViewCategoryPathToggle?.IsChecked == true,
                    ShowApplies = ViewAppliesToggle?.IsChecked == true,
                    ShowSupported = ViewSupportedToggle?.IsChecked == true,
                    ShowUserState = ViewUserStateToggle?.IsChecked == true,
                    ShowComputerState = ViewComputerStateToggle?.IsChecked == true,
                    ShowBookmark =
                        !(RootGrid?.FindName("ViewBookmarkToggle") is ToggleMenuFlyoutItem vbt)
                        || vbt.IsChecked == true,
                    ShowSecondName = ViewSecondNameToggle?.IsChecked == true,
                };
                SettingsService.Instance.UpdateColumns(cols);
            }
            catch { }
        }

        private void UpdateColumnMenuChecks()
        {
            try
            {
                // Keep menu toggles in sync with actual visibility
                if (ViewIdToggle != null)
                    ViewIdToggle.IsChecked = ColId?.Visibility == Visibility.Visible;
                if (ViewCategoryToggle != null)
                    ViewCategoryToggle.IsChecked = ColCategory?.Visibility == Visibility.Visible;
                if (ViewTopCategoryToggle != null)
                    ViewTopCategoryToggle.IsChecked =
                        ColTopCategory?.Visibility == Visibility.Visible;
                if (ViewCategoryPathToggle != null)
                    ViewCategoryPathToggle.IsChecked =
                        ColCategoryPath?.Visibility == Visibility.Visible;
                if (ViewAppliesToggle != null)
                    ViewAppliesToggle.IsChecked = ColApplies?.Visibility == Visibility.Visible;
                if (ViewSupportedToggle != null)
                    ViewSupportedToggle.IsChecked = ColSupported?.Visibility == Visibility.Visible;
                if (ViewUserStateToggle != null)
                    ViewUserStateToggle.IsChecked = ColUserIcon?.Visibility == Visibility.Visible;
                if (ViewComputerStateToggle != null)
                    ViewComputerStateToggle.IsChecked =
                        ColComputerIcon?.Visibility == Visibility.Visible;

                var s = SettingsService.Instance.LoadSettings();
                bool enabled = s.SecondLanguageEnabled ?? false;
                if (ViewSecondNameToggle != null)
                {
                    ViewSecondNameToggle.Visibility = enabled
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    if (enabled)
                    {
                        // When enabled sync checkbox with actual visibility; when disabled retain prior preference.
                        ViewSecondNameToggle.IsChecked =
                            ColSecondName?.Visibility == Visibility.Visible;
                    }
                }
            }
            catch { }
        }

        private void ApplyColumnVisibilityFromToggles()
        {
            try
            {
                if (ColId != null)
                    ColId.Visibility =
                        ViewIdToggle?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                if (ColCategory != null)
                    ColCategory.Visibility =
                        ViewCategoryToggle?.IsChecked == true
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                if (ColTopCategory != null)
                    ColTopCategory.Visibility =
                        ViewTopCategoryToggle?.IsChecked == true
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                if (ColCategoryPath != null)
                    ColCategoryPath.Visibility =
                        ViewCategoryPathToggle?.IsChecked == true
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                if (ColApplies != null)
                    ColApplies.Visibility =
                        ViewAppliesToggle?.IsChecked == true
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                if (ColSupported != null)
                    ColSupported.Visibility =
                        ViewSupportedToggle?.IsChecked == true
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                if (ColUserIcon != null)
                    ColUserIcon.Visibility =
                        ViewUserStateToggle?.IsChecked == true
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                if (ColComputerIcon != null)
                    ColComputerIcon.Visibility =
                        ViewComputerStateToggle?.IsChecked == true
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                if (ColBookmark != null)
                {
                    bool showBookmark = true;
                    if (
                        RootGrid?.FindName("ViewBookmarkToggle") is ToggleMenuFlyoutItem vbt
                        && vbt.IsChecked == false
                    )
                        showBookmark = false;
                    ColBookmark.Visibility = showBookmark
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                if (ColSecondName != null)
                {
                    var s = SettingsService.Instance.LoadSettings();
                    bool secondEnabled = s.SecondLanguageEnabled ?? false;
                    ColSecondName.Visibility =
                        (secondEnabled && (ViewSecondNameToggle?.IsChecked == true))
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void UpdateColumnVisibilityFromFlags()
        {
            // Backward compatibility shim: keep calling path updates via toggles
            ApplyColumnVisibilityFromToggles();
        }

        private ToggleMenuFlyoutItem? FindMenu(string name) =>
            RootGrid?.FindName(name) as ToggleMenuFlyoutItem;

        private void ApplyTheme(string pref)
        {
            if (RootGrid == null)
                return;
            var theme = pref switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            RootGrid.RequestedTheme = theme;
            App.SetGlobalTheme(theme);
            try
            {
                SettingsService.Instance.UpdateTheme(pref);
            }
            catch { }

            // Sync menu checks
            try
            {
                var system = FindMenu("ThemeSystemMenu");
                var dark = FindMenu("ThemeDarkMenu");
                var light = FindMenu("ThemeLightMenu");
                if (system != null)
                    system.IsChecked = pref.Equals("System", StringComparison.OrdinalIgnoreCase);
                if (dark != null)
                    dark.IsChecked = pref.Equals("Dark", StringComparison.OrdinalIgnoreCase);
                if (light != null)
                    light.IsChecked = pref.Equals("Light", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        private void ThemeMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ToggleMenuFlyoutItem clicked)
                {
                    // Uncheck all theme items first
                    foreach (
                        var id in new[] { "ThemeSystemMenu", "ThemeDarkMenu", "ThemeLightMenu" }
                    )
                    {
                        var mi = FindMenu(id);
                        if (mi != null)
                            mi.IsChecked = false;
                    }
                    clicked.IsChecked = true;
                    var pref = clicked.Text;
                    ApplyTheme(pref);
                }
            }
            catch { }
        }

        private void SetScaleFromString(string s, bool updateSelector, bool save)
        {
            double scale = 1.0;
            if (
                !string.IsNullOrEmpty(s)
                && s.EndsWith("%")
                && double.TryParse(s.TrimEnd('%'), out var pct)
            )
                scale = Math.Max(0.5, pct / 100.0);
            App.SetGlobalScale(scale);
            if (save)
            {
                try
                {
                    SettingsService.Instance.UpdateScale(s);
                }
                catch { }
            }
            try
            {
                foreach (
                    var id in new[]
                    {
                        "Scale50Menu",
                        "Scale67Menu",
                        "Scale75Menu",
                        "Scale80Menu",
                        "Scale90Menu",
                        "Scale100Menu",
                        "Scale110Menu",
                        "Scale125Menu",
                        "Scale150Menu",
                        "Scale175Menu",
                        "Scale200Menu",
                    }
                )
                {
                    var mi = FindMenu(id);
                    if (mi != null)
                        mi.IsChecked = string.Equals(
                            mi.Text,
                            s,
                            StringComparison.OrdinalIgnoreCase
                        );
                }
            }
            catch { }
        }

        private void ScaleMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is ToggleMenuFlyoutItem clicked)
                {
                    // Uncheck all scale items
                    foreach (
                        var id in new[]
                        {
                            "Scale50Menu",
                            "Scale67Menu",
                            "Scale75Menu",
                            "Scale80Menu",
                            "Scale90Menu",
                            "Scale100Menu",
                            "Scale110Menu",
                            "Scale125Menu",
                            "Scale150Menu",
                            "Scale175Menu",
                            "Scale200Menu",
                        }
                    )
                    {
                        var mi = FindMenu(id);
                        if (mi != null)
                            mi.IsChecked = false;
                    }
                    clicked.IsChecked = true;
                    var scaleText = clicked.Text; // e.g. "125%"
                    SetScaleFromString(scaleText, updateSelector: false, save: true);
                }
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
                    ViewSecondNameToggle.Visibility = enabled
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    ViewSecondNameToggle.Text = enabled
                        ? $"2nd Language Name ({lang})"
                        : "2nd Language Name";
                    // Do not clear IsChecked when disabled; keep stored preference for future enable.
                }
                if (ColSecondName != null)
                {
                    ColSecondName.Header = enabled
                        ? $"2nd Language Name ({lang})"
                        : "2nd Language Name";
                    ColSecondName.Visibility =
                        (enabled && (ViewSecondNameToggle?.IsChecked == true))
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void ViewSecoundNameToggle_Click(object sender, RoutedEventArgs e)
        {
            SaveColumnPrefs();
            ApplyColumnVisibilityFromToggles();
            SaveColumnLayout(includeOrder: false);
        }

        private void CategorySplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (CategoryColumn != null)
                {
                    double w = CategoryColumn.ActualWidth;
                    if (w > 0)
                        SettingsService.Instance.UpdateCategoryPaneWidth(w);
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
                        total =
                            PolicyDetailGrid.RowDefinitions[0].ActualHeight
                            + SplitterRow.ActualHeight
                            + DetailRow.ActualHeight;
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

                // Detail pane sizing handled lazily in ShowDetailsPane(); avoid early conflicting application here.

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
                    foreach (var col in PolicyList.Columns)
                        col.SortDirection = null;
                    if (_sortDirection != null)
                    {
                        if (_sortColumn == nameof(PolicyListRow.DisplayName))
                            ColName.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.ShortId))
                            ColId.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.CategoryName))
                            ColCategory.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.AppliesText))
                            ColApplies.SortDirection = _sortDirection;
                        else if (_sortColumn == nameof(PolicyListRow.SupportedText))
                            ColSupported.SortDirection = _sortDirection;
                    }
                }
            }
            catch { }
        }
    }
}
