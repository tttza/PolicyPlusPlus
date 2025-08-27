using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PolicyPlus.WinUI3.Dialogs
{
    public sealed partial class LanguageOptionsDialog : ContentDialog
    {
        public string? SelectedLanguage { get; private set; }
        public LanguageOptionsDialog()
        {
            InitializeComponent();
            this.PrimaryButtonClick += LanguageOptionsDialog_PrimaryButtonClick;
        }

        public void Initialize(string admxFolderPath, string? currentLanguage)
        {
            LanguageSelector.Items.Clear();
            if (!Directory.Exists(admxFolderPath)) return;
            var dirs = Directory.GetDirectories(admxFolderPath)
                                .Select(d => new DirectoryInfo(d))
                                .Where(di => di.EnumerateFiles("*.adml", SearchOption.TopDirectoryOnly).Any())
                                .OrderBy(di => di.Name, System.StringComparer.InvariantCultureIgnoreCase)
                                .ToList();
            ComboBoxItem? toSelect = null;
            foreach (var di in dirs)
            {
                string code = di.Name; // e.g., en-US
                string display;
                try { display = CultureInfo.GetCultureInfo(code).DisplayName; }
                catch { display = code; }
                var item = new ComboBoxItem { Content = display, Tag = code };
                LanguageSelector.Items.Add(item);
                if (!string.IsNullOrEmpty(currentLanguage) && code.Equals(currentLanguage, System.StringComparison.OrdinalIgnoreCase))
                    toSelect = item;
            }
            if (toSelect != null)
                LanguageSelector.SelectedItem = toSelect;
            else if (LanguageSelector.Items.Count > 0)
                LanguageSelector.SelectedIndex = 0;
        }

        private void LanguageOptionsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var sel = LanguageSelector.SelectedItem as ComboBoxItem;
            SelectedLanguage = sel?.Tag?.ToString();
        }
    }
}
