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

        private static string? TryResolvePrefix(string requested, DirectoryInfo[] dirs)
        {
            if (string.IsNullOrWhiteSpace(requested))
                return null;
            // exact
            var exact = dirs.FirstOrDefault(d => d.Name.Equals(requested, System.StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact.Name;
            // prefix before '-'
            var prefix = requested.Split('-')[0];
            var prefMatch = dirs.FirstOrDefault(d => d.Name.StartsWith(prefix + "-", System.StringComparison.OrdinalIgnoreCase));
            if (prefMatch != null)
                return prefMatch.Name;
            // startswith requested (e.g. user saved short, directories long, or vice versa)
            var starts = dirs.FirstOrDefault(d => d.Name.StartsWith(requested, System.StringComparison.OrdinalIgnoreCase));
            if (starts != null)
                return starts.Name;
            return null;
        }

        public void Initialize(string admxFolderPath, string? currentLanguage)
        {
            LanguageSelector.Items.Clear();
            SecondLanguageSelector.Items.Clear();
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
                string code = di.Name; // e.g., en-US
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
                LanguageSelector.Items.Add(item);
                var item2 = new ComboBoxItem { Content = display, Tag = code };
                SecondLanguageSelector.Items.Add(item2);
                if (!string.IsNullOrEmpty(currentLanguage) && code.Equals(currentLanguage, System.StringComparison.OrdinalIgnoreCase))
                    toSelect = item;
                if (code.Equals("en-US", System.StringComparison.OrdinalIgnoreCase))
                    secondDefault = item2;
            }

            // Attempt prefix resolution if direct exact not found
            if (toSelect == null && !string.IsNullOrEmpty(currentLanguage))
            {
                var resolved = TryResolvePrefix(currentLanguage, dirs);
                if (!string.IsNullOrEmpty(resolved))
                {
                    foreach (ComboBoxItem cbi in LanguageSelector.Items)
                    {
                        if ((cbi.Tag?.ToString() ?? "").Equals(resolved, System.StringComparison.OrdinalIgnoreCase))
                        {
                            toSelect = cbi;
                            break;
                        }
                    }
                }
            }

            // If still not found, add placeholder so user sees their saved (now-missing) code instead of silently switching
            if (toSelect == null && !string.IsNullOrEmpty(currentLanguage))
            {
                var placeholder = new ComboBoxItem
                {
                    Content = currentLanguage + " (missing)",
                    Tag = currentLanguage,
                };
                LanguageSelector.Items.Insert(0, placeholder);
                toSelect = placeholder;
            }

            if (toSelect != null)
                LanguageSelector.SelectedItem = toSelect;
            else if (LanguageSelector.Items.Count > 0)
                LanguageSelector.SelectedIndex = 0;

            if (secondDefault != null)
                SecondLanguageSelector.SelectedItem = secondDefault;
            else if (SecondLanguageSelector.Items.Count > 0)
                SecondLanguageSelector.SelectedIndex = 0;

            // Load saved second language state
            var s = SettingsService.Instance.LoadSettings();
            SecondLangEnabled.IsChecked = s.SecondLanguageEnabled ?? false;
            SecondLanguageSelector.IsEnabled = SecondLangEnabled.IsChecked == true;
            if (!string.IsNullOrEmpty(s.SecondLanguage))
            {
                ComboBoxItem? secondSelect = null;
                foreach (ComboBoxItem cbi in SecondLanguageSelector.Items)
                {
                    if ((cbi.Tag?.ToString() ?? "").Equals(s.SecondLanguage, System.StringComparison.OrdinalIgnoreCase))
                    {
                        secondSelect = cbi;
                        break;
                    }
                }
                if (secondSelect == null)
                {
                    // Try prefix resolution for second language as well
                    var secondResolved = TryResolvePrefix(s.SecondLanguage, dirs);
                    if (!string.IsNullOrEmpty(secondResolved))
                    {
                        foreach (ComboBoxItem cbi in SecondLanguageSelector.Items)
                        {
                            if ((cbi.Tag?.ToString() ?? "").Equals(secondResolved, System.StringComparison.OrdinalIgnoreCase))
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
                        SecondLanguageSelector.Items.Insert(0, missingSecond);
                        secondSelect = missingSecond;
                    }
                }
                SecondLanguageSelector.SelectedItem = secondSelect;
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
