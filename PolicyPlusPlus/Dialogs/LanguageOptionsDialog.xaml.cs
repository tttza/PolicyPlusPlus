using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.IO;
using System.Linq;
using PolicyPlusPlus.Services;

namespace PolicyPlusPlus.Dialogs
{
    public sealed partial class LanguageOptionsDialog : ContentDialog
    {
        public string? SelectedLanguage { get; private set; }
        public bool SecondLanguageEnabled { get; private set; }
        public string? SelectedSecondLanguage { get; private set; }

        public LanguageOptionsDialog()
        {
            InitializeComponent();
            this.PrimaryButtonClick += LanguageOptionsDialog_PrimaryButtonClick;
            SecondLangEnabled.Checked += SecondLangEnabled_Checked;
            SecondLangEnabled.Unchecked += SecondLangEnabled_Checked;
        }

        private void SecondLangEnabled_Checked(object sender, RoutedEventArgs e)
        {
            SecondLanguageSelector.IsEnabled = SecondLangEnabled.IsChecked == true;
        }

        public void Initialize(string admxFolderPath, string? currentLanguage)
        {
            LanguageSelector.Items.Clear();
            SecondLanguageSelector.Items.Clear();
            if (!Directory.Exists(admxFolderPath)) return;
            var dirs = Directory.GetDirectories(admxFolderPath)
                                .Select(d => new DirectoryInfo(d))
                                .Where(di => di.EnumerateFiles("*.adml", SearchOption.TopDirectoryOnly).Any())
                                .OrderBy(di => di.Name, System.StringComparer.InvariantCultureIgnoreCase)
                                .ToList();
            ComboBoxItem? toSelect = null;
            ComboBoxItem? secondDefault = null;
            foreach (var di in dirs)
            {
                string code = di.Name; // e.g., en-US
                string display;
                try { display = CultureInfo.GetCultureInfo(code).DisplayName; }
                catch { display = code; }
                var item = new ComboBoxItem { Content = display, Tag = code };
                LanguageSelector.Items.Add(item);
                var item2 = new ComboBoxItem { Content = display, Tag = code };
                SecondLanguageSelector.Items.Add(item2);
                if (!string.IsNullOrEmpty(currentLanguage) && code.Equals(currentLanguage, System.StringComparison.OrdinalIgnoreCase))
                    toSelect = item;
                if (code.Equals("en-US", System.StringComparison.OrdinalIgnoreCase))
                    secondDefault = item2;
            }
            if (toSelect != null) LanguageSelector.SelectedItem = toSelect; else if (LanguageSelector.Items.Count > 0) LanguageSelector.SelectedIndex = 0;
            if (secondDefault != null) SecondLanguageSelector.SelectedItem = secondDefault; else if (SecondLanguageSelector.Items.Count > 0) SecondLanguageSelector.SelectedIndex = 0;

            // Load saved second language state
            var s = SettingsService.Instance.LoadSettings();
            SecondLangEnabled.IsChecked = s.SecondLanguageEnabled ?? false;
            SecondLanguageSelector.IsEnabled = SecondLangEnabled.IsChecked == true;
            if (!string.IsNullOrEmpty(s.SecondLanguage))
            {
                foreach (ComboBoxItem cbi in SecondLanguageSelector.Items)
                {
                    if ((cbi.Tag?.ToString() ?? "").Equals(s.SecondLanguage, System.StringComparison.OrdinalIgnoreCase))
                    { SecondLanguageSelector.SelectedItem = cbi; break; }
                }
            }
        }

        private void LanguageOptionsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var sel = LanguageSelector.SelectedItem as ComboBoxItem;
            SelectedLanguage = sel?.Tag?.ToString();
            SecondLanguageEnabled = SecondLangEnabled.IsChecked == true;
            var sel2 = SecondLanguageSelector.SelectedItem as ComboBoxItem;
            SelectedSecondLanguage = sel2?.Tag?.ToString();
        }
    }
}
