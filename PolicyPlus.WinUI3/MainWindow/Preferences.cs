using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

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
                if (_config == null) return;
                bool Get(string k, bool defVal) { try { return Convert.ToInt32(_config.GetValue(k, defVal ? 1 : 0)) != 0; } catch { return defVal; } }
                var id = GetFlag("ColIdFlag"); if (id != null) id.IsChecked = Get("Columns.ShowId", true);
                var cat = GetFlag("ColCategoryFlag"); if (cat != null) cat.IsChecked = Get("Columns.ShowCategory", false);
                var app = GetFlag("ColAppliesFlag"); if (app != null) app.IsChecked = Get("Columns.ShowApplies", false);
                var sup = GetFlag("ColSupportedFlag"); if (sup != null) sup.IsChecked = Get("Columns.ShowSupported", false);
                var us = GetFlag("ColUserStateFlag"); if (us != null) us.IsChecked = Get("Columns.ShowUserState", true);
                var cs = GetFlag("ColCompStateFlag"); if (cs != null) cs.IsChecked = Get("Columns.ShowComputerState", true);
            }
            catch { }
        }

        private void SaveColumnPrefs()
        {
            try
            {
                if (_config == null) return;
                void Set(string k, bool v) { try { _config.SetValue(k, v ? 1 : 0); } catch { } }
                var id = GetFlag("ColIdFlag"); if (id != null) Set("Columns.ShowId", id.IsChecked == true);
                var cat = GetFlag("ColCategoryFlag"); if (cat != null) Set("Columns.ShowCategory", cat.IsChecked == true);
                var app = GetFlag("ColAppliesFlag"); if (app != null) Set("Columns.ShowApplies", app.IsChecked == true);
                var sup = GetFlag("ColSupportedFlag"); if (sup != null) Set("Columns.ShowSupported", sup.IsChecked == true);
                var us = GetFlag("ColUserStateFlag"); if (us != null) Set("Columns.ShowUserState", us.IsChecked == true);
                var cs = GetFlag("ColCompStateFlag"); if (cs != null) Set("Columns.ShowComputerState", cs.IsChecked == true);
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
            }
            catch { }
        }

        private void ApplyTheme(string pref)
        {
            if (RootGrid == null) return;
            var theme = pref switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
            RootGrid.RequestedTheme = theme;
            App.SetGlobalTheme(theme);
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = ThemeSelector as ComboBox;
            var item = ((cb?.SelectedItem as ComboBoxItem)?.Content?.ToString());
            var pref = item ?? "System";
            ApplyTheme(pref);
            try { _config?.SetValue("Theme", pref); } catch { }
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
                try { _config?.SetValue("UIScale", s); } catch { }
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
    }
}
