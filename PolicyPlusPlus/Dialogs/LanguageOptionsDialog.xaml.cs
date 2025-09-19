using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PolicyPlusPlus.Services;

namespace PolicyPlusPlus.Dialogs
{
    public sealed partial class LanguageOptionsDialog : ContentDialog
    {
        // Accessors (avoid relying on generated field visibility)
        private ComboBox PrimaryLanguageSelectorControl =>
            (ComboBox)FindName("PrimaryLanguageSelector");
        private CheckBox PrimaryFallbackEnabledControl =>
            (CheckBox)FindName("PrimaryFallbackEnabled");
        private CheckBox SecondLanguageEnabledChkControl =>
            (CheckBox)FindName("SecondLanguageEnabledChk");
        private ComboBox SecondLanguageSelectorBoxControl =>
            (ComboBox)FindName("SecondLanguageSelectorBox");

        public string? SelectedLanguage { get; private set; }
        public bool SecondLanguageEnabled { get; private set; }
        public string? SelectedSecondLanguage { get; private set; }
        public bool PrimaryFallbackEnabledValue { get; private set; }

        public LanguageOptionsDialog()
        {
            InitializeComponent();
            this.PrimaryButtonClick += LanguageOptionsDialog_PrimaryButtonClick;
            SecondLanguageEnabledChkControl.Checked += SecondLangChanged;
            SecondLanguageEnabledChkControl.Unchecked += SecondLangChanged;
        }

        private void SecondLangChanged(object sender, RoutedEventArgs e)
        {
            SecondLanguageSelectorBoxControl.IsEnabled =
                SecondLanguageEnabledChkControl.IsChecked == true;
        }

        private static string? TryResolvePrefix(string requested, DirectoryInfo[] dirs)
        {
            if (string.IsNullOrWhiteSpace(requested))
                return null;
            var exact = dirs.FirstOrDefault(d =>
                d.Name.Equals(requested, System.StringComparison.OrdinalIgnoreCase)
            );
            if (exact != null)
                return exact.Name;
            var prefix = requested.Split('-')[0];
            var prefMatch = dirs.FirstOrDefault(d =>
                d.Name.StartsWith(prefix + "-", System.StringComparison.OrdinalIgnoreCase)
            );
            if (prefMatch != null)
                return prefMatch.Name;
            var starts = dirs.FirstOrDefault(d =>
                d.Name.StartsWith(requested, System.StringComparison.OrdinalIgnoreCase)
            );
            if (starts != null)
                return starts.Name;
            return null;
        }

        public void Initialize(string admxFolderPath, string? currentLanguage)
        {
            PrimaryLanguageSelectorControl.Items.Clear();
            SecondLanguageSelectorBoxControl.Items.Clear();
            if (!Directory.Exists(admxFolderPath))
                return;
            var dirs = Directory
                .GetDirectories(admxFolderPath)
                .Select(d => new DirectoryInfo(d))
                .Where(di => di.EnumerateFiles("*.adml", SearchOption.TopDirectoryOnly).Any())
                .OrderBy(di => di.Name, System.StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
            ComboBoxItem? toSelect = null;
            ComboBoxItem? secondDefault = null;
            foreach (var di in dirs)
            {
                string code = di.Name;
                string display;
                try
                {
                    display = CultureInfo.GetCultureInfo(code).DisplayName;
                }
                catch
                {
                    display = code;
                }
                var item = new ComboBoxItem { Content = display, Tag = code };
                PrimaryLanguageSelectorControl.Items.Add(item);
                var item2 = new ComboBoxItem { Content = display, Tag = code };
                SecondLanguageSelectorBoxControl.Items.Add(item2);
                if (
                    !string.IsNullOrEmpty(currentLanguage)
                    && code.Equals(currentLanguage, System.StringComparison.OrdinalIgnoreCase)
                )
                    toSelect = item;
                if (code.Equals("en-US", System.StringComparison.OrdinalIgnoreCase))
                    secondDefault = item2;
            }
            if (toSelect == null && !string.IsNullOrEmpty(currentLanguage))
            {
                var resolved = TryResolvePrefix(currentLanguage, dirs);
                if (!string.IsNullOrEmpty(resolved))
                {
                    foreach (ComboBoxItem cbi in PrimaryLanguageSelectorControl.Items)
                    {
                        if (
                            (cbi.Tag?.ToString() ?? "").Equals(
                                resolved,
                                System.StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            toSelect = cbi;
                            break;
                        }
                    }
                }
            }
            if (toSelect == null && !string.IsNullOrEmpty(currentLanguage))
            {
                var placeholder = new ComboBoxItem
                {
                    Content = currentLanguage + " (missing)",
                    Tag = currentLanguage,
                };
                PrimaryLanguageSelectorControl.Items.Insert(0, placeholder);
                toSelect = placeholder;
            }
            if (toSelect != null)
                PrimaryLanguageSelectorControl.SelectedItem = toSelect;
            else if (PrimaryLanguageSelectorControl.Items.Count > 0)
                PrimaryLanguageSelectorControl.SelectedIndex = 0;
            if (secondDefault != null)
                SecondLanguageSelectorBoxControl.SelectedItem = secondDefault;
            else if (SecondLanguageSelectorBoxControl.Items.Count > 0)
                SecondLanguageSelectorBoxControl.SelectedIndex = 0;

            var s = SettingsService.Instance.LoadSettings();
            SecondLanguageEnabledChkControl.IsChecked = s.SecondLanguageEnabled ?? false;
            SecondLanguageSelectorBoxControl.IsEnabled =
                SecondLanguageEnabledChkControl.IsChecked == true;
            bool fallbackPref = true;
            try
            {
                fallbackPref = s.PrimaryLanguageFallbackEnabled ?? true;
            }
            catch { }
            PrimaryFallbackEnabledControl.IsChecked = fallbackPref;

            if (!string.IsNullOrEmpty(s.SecondLanguage))
            {
                ComboBoxItem? secondSelect = null;
                foreach (ComboBoxItem cbi in SecondLanguageSelectorBoxControl.Items)
                {
                    if (
                        (cbi.Tag?.ToString() ?? "").Equals(
                            s.SecondLanguage,
                            System.StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        secondSelect = cbi;
                        break;
                    }
                }
                if (secondSelect == null)
                {
                    var secondResolved = TryResolvePrefix(s.SecondLanguage, dirs);
                    if (!string.IsNullOrEmpty(secondResolved))
                    {
                        foreach (ComboBoxItem cbi in SecondLanguageSelectorBoxControl.Items)
                        {
                            if (
                                (cbi.Tag?.ToString() ?? "").Equals(
                                    secondResolved,
                                    System.StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                secondSelect = cbi;
                                break;
                            }
                        }
                    }
                    if (secondSelect == null)
                    {
                        var missingSecond = new ComboBoxItem
                        {
                            Content = s.SecondLanguage + " (missing)",
                            Tag = s.SecondLanguage,
                        };
                        SecondLanguageSelectorBoxControl.Items.Insert(0, missingSecond);
                        secondSelect = missingSecond;
                    }
                }
                SecondLanguageSelectorBoxControl.SelectedItem = secondSelect;
            }
        }

        private void LanguageOptionsDialog_PrimaryButtonClick(
            ContentDialog sender,
            ContentDialogButtonClickEventArgs args
        )
        {
            var sel = PrimaryLanguageSelectorControl.SelectedItem as ComboBoxItem;
            SelectedLanguage = sel?.Tag?.ToString();
            SecondLanguageEnabled = SecondLanguageEnabledChkControl.IsChecked == true;
            var sel2 = SecondLanguageSelectorBoxControl.SelectedItem as ComboBoxItem;
            SelectedSecondLanguage = sel2?.Tag?.ToString();
            PrimaryFallbackEnabledValue = PrimaryFallbackEnabledControl.IsChecked == true;
            try
            {
                SettingsService.Instance.UpdatePrimaryLanguageFallback(PrimaryFallbackEnabledValue);
            }
            catch { }
        }
    }
}
