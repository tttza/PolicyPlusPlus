using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using PolicyPlus.WinUI3.Services;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private void ApplyDetailsPaneVisibility()
        {
            try
            {
                if (DetailsPane == null || DetailRow == null || DetailsSplitter == null || SplitterRow == null) return;

                if (_showDetails)
                {
                    DetailsPane.Visibility = Visibility.Visible;
                    DetailsSplitter.Visibility = Visibility.Visible;
                    DetailRow.Height = _savedDetailRowHeight ?? new GridLength(2, GridUnitType.Star);
                    SplitterRow.Height = _savedSplitterRowHeight ?? new GridLength(8);
                }
                else
                {
                    if (DetailRow.Height.Value > 0 || DetailRow.Height.IsStar)
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

        private void LoadColumnPrefs()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                var cols = s.Columns;
                if (cols == null) return;
                var id = GetFlag("ColIdFlag"); if (id != null) id.IsChecked = cols.ShowId;
                var cat = GetFlag("ColCategoryFlag"); if (cat != null) cat.IsChecked = cols.ShowCategory;
                var app = GetFlag("ColAppliesFlag"); if (app != null) app.IsChecked = cols.ShowApplies;
                var sup = GetFlag("ColSupportedFlag"); if (sup != null) sup.IsChecked = cols.ShowSupported;
                var us = GetFlag("ColUserStateFlag"); if (us != null) us.IsChecked = cols.ShowUserState;
                var cs = GetFlag("ColCompStateFlag"); if (cs != null) cs.IsChecked = cols.ShowComputerState;
                if (ViewSecondNameToggle != null)
                {
                    bool enabled = s.SecondLanguageEnabled ?? false;
                    ViewSecondNameToggle.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                    ViewSecondNameToggle.IsChecked = enabled && (cols.ShowEnglishName);
                }
            }
            catch { }
        }

        private void SaveColumnPrefs()
        {
            try
            {
                var current = SettingsService.Instance.LoadSettings();
                var existing = current.Columns ?? new ColumnsOptions();

                bool showId = GetFlag("ColIdFlag")?.IsChecked ?? existing.ShowId;
                bool showCategory = GetFlag("ColCategoryFlag")?.IsChecked ?? existing.ShowCategory;
                bool showApplies = GetFlag("ColAppliesFlag")?.IsChecked ?? existing.ShowApplies;
                bool showSupported = GetFlag("ColSupportedFlag")?.IsChecked ?? existing.ShowSupported;
                bool showUserState = GetFlag("ColUserStateFlag")?.IsChecked ?? existing.ShowUserState;
                bool showComputerState = GetFlag("ColCompStateFlag")?.IsChecked ?? existing.ShowComputerState;
                bool showSecondName = ViewSecondNameToggle?.IsChecked ?? false;

                var cols = new ColumnsOptions
                {
                    ShowId = showId,
                    ShowCategory = showCategory,
                    ShowApplies = showApplies,
                    ShowSupported = showSupported,
                    ShowUserState = showUserState,
                    ShowComputerState = showComputerState,
                    ShowEnglishName = showSecondName,
                };
                SettingsService.Instance.UpdateColumns(cols);
            }
            catch { }
        }

        private void UpdateColumnMenuChecks()
        {
            try
            {
                if (ViewIdToggle != null) ViewIdToggle.IsChecked = (ColIdFlag?.IsChecked == true);
                if (ViewCategoryToggle != null) ViewCategoryToggle.IsChecked = (ColCategoryFlag?.IsChecked == true);
                if (ViewAppliesToggle != null) ViewAppliesToggle.IsChecked = (ColAppliesFlag?.IsChecked == true);
                if (ViewSupportedToggle != null) ViewSupportedToggle.IsChecked = (ColSupportedFlag?.IsChecked == true);
                if (ViewSecondNameToggle != null)
                {
                    var s = SettingsService.Instance.LoadSettings();
                    bool enabled = s.SecondLanguageEnabled ?? false;
                    ViewSecondNameToggle.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                    ViewSecondNameToggle.IsChecked = enabled && (s.Columns?.ShowEnglishName ?? false);
                }
            }
            catch { }
        }

        private void UpdateColumnVisibilityFromFlags()
        {
            try
            {
                if (ColId != null) ColId.Visibility = (ColIdFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColCategory != null) ColCategory.Visibility = (ColCategoryFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColApplies != null) ColApplies.Visibility = (ColAppliesFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
                if (ColSupported != null) ColSupported.Visibility = (ColSupportedFlag?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

                var s = SettingsService.Instance.LoadSettings();
                bool enabled = s.SecondLanguageEnabled ?? false;
                if (ColSecondName != null)
                {
                    ColSecondName.Visibility = (enabled && (ViewSecondNameToggle?.IsChecked == true)) ? Visibility.Visible : Visibility.Collapsed;
                }

                UpdateColumnMenuChecks();
            }
            catch { }
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
                    ViewSecondNameToggle.Text = enabled ? $"2nd Language column ({lang})" : "2nd Language column";
                    if (!enabled) ViewSecondNameToggle.IsChecked = false;
                }
                if (ColSecondName != null)
                {
                    ColSecondName.Header = enabled ? $"2nd Language ({lang})" : "2nd Language";
                    ColSecondName.Visibility = (enabled && (ViewSecondNameToggle?.IsChecked == true)) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void ViewEnglishToggle_Click(object sender, RoutedEventArgs e)
        {
            bool on = (sender as ToggleMenuFlyoutItem)?.IsChecked == true;
            try { Services.SettingsService.Instance.UpdateShowEnglishNames(on); } catch { }
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
        }

        private void ViewEnglishNameToggle_Click(object sender, RoutedEventArgs e)
        {
            SaveColumnPrefs();
            UpdateColumnVisibilityFromFlags();
        }
    }
}
