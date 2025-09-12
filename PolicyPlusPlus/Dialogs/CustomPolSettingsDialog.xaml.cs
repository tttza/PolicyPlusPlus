using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Services;
using System; // Await extensions
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PolicyPlusPlus.Dialogs
{
    public sealed partial class CustomPolSettingsDialog : ContentDialog
    {
        public string? ComputerPolPath { get; private set; }
        public string? UserPolPath { get; private set; }
        public bool ActivateAfter => ActivateCheck.IsChecked == true;
        public bool EnableComputer => (EnableComputerCheck?.IsChecked ?? false);
        public bool EnableUser => (EnableUserCheck?.IsChecked ?? false);

        public CustomPolSettingsDialog()
        {
            InitializeComponent();
        }

        public void Initialize(string? currentComp, string? currentUser, bool enableComp = true, bool enableUser = true)
        {
            ComputerPolPath = currentComp;
            UserPolPath = currentUser;
            if (EnableComputerCheck != null) EnableComputerCheck.IsChecked = enableComp;
            if (EnableUserCheck != null) EnableUserCheck.IsChecked = enableUser;
            if (ComputerPathBox != null) ComputerPathBox.Text = string.IsNullOrWhiteSpace(ComputerPolPath) ? "(not set)" : ComputerPolPath;
            if (UserPathBox != null) UserPathBox.Text = string.IsNullOrWhiteSpace(UserPolPath) ? "(not set)" : UserPolPath;
        }

        private async void BrowseComp_Click(object sender, RoutedEventArgs e) => await BrowseAsync(isUser: false);
        private async void BrowseUser_Click(object sender, RoutedEventArgs e) => await BrowseAsync(isUser: true);
        private async void NewComp_Click(object sender, RoutedEventArgs e) => await CreateNewAsync(isUser: false);
        private async void NewUser_Click(object sender, RoutedEventArgs e) => await CreateNewAsync(isUser: true);
        private void ResetComp_Click(object sender, RoutedEventArgs e) { ComputerPolPath = null; if (ComputerPathBox != null) ComputerPathBox.Text = "(not set)"; }
        private void ResetUser_Click(object sender, RoutedEventArgs e) { UserPolPath = null; if (UserPathBox != null) UserPathBox.Text = "(not set)"; }

        private async Task CreateNewAsync(bool isUser)
        {
            try
            {
                var picker = new FileSavePicker();
                var mainWin = App.Window;
                if (mainWin != null)
                {
                    var hwnd = WindowNative.GetWindowHandle(mainWin);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }
                picker.FileTypeChoices.Add("Policy file", new System.Collections.Generic.List<string> { ".pol" });
                picker.SuggestedFileName = isUser ? "user" : "machine";
                var file = await picker.PickSaveFileAsync();
                if (file == null) return;
                try { new PolFile().Save(file.Path); } catch { }
                if (isUser) { UserPolPath = file.Path; if (UserPathBox != null) UserPathBox.Text = file.Path; }
                else { ComputerPolPath = file.Path; if (ComputerPathBox != null) ComputerPathBox.Text = file.Path; }
            }
            catch { }
        }

        private async Task BrowseAsync(bool isUser)
        {
            var path = await PickAsync();
            if (string.IsNullOrEmpty(path)) return;
            if (isUser) { UserPolPath = path; if (UserPathBox != null) UserPathBox.Text = path; }
            else { ComputerPolPath = path; if (ComputerPathBox != null) ComputerPathBox.Text = path; }
        }

        private async Task<string?> PickAsync()
        {
            try
            {
                var picker = new FileOpenPicker();
                var mainWin = App.Window;
                if (mainWin != null)
                {
                    var hwnd = WindowNative.GetWindowHandle(mainWin);
                    InitializeWithWindow.Initialize(picker, hwnd);
                }
                picker.FileTypeFilter.Add(".pol");
                var file = await picker.PickSingleFileAsync();
                return file?.Path;
            }
            catch { return null; }
        }

        private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Case: both disabled -> allow saving disabled state (no paths required)
            if (!EnableComputer && !EnableUser)
            {
                SettingsService.Instance.UpdateCustomPolSettings(false, false, null, null);
                return;
            }

            // If a scope is enabled but has no path, auto create an empty POL in temp folder.
            if (EnableComputer && string.IsNullOrEmpty(ComputerPolPath))
            {
                ComputerPolPath = AutoCreateTempPol(false);
                if (ComputerPathBox != null) ComputerPathBox.Text = ComputerPolPath ?? "(not set)";
            }
            if (EnableUser && string.IsNullOrEmpty(UserPolPath))
            {
                UserPolPath = AutoCreateTempPol(true);
                if (UserPathBox != null) UserPathBox.Text = UserPolPath ?? "(not set)";
            }

            // Validate again after auto-create
            if (EnableComputer && string.IsNullOrEmpty(ComputerPolPath)) { args.Cancel = true; return; }
            if (EnableUser && string.IsNullOrEmpty(UserPolPath)) { args.Cancel = true; return; }

            try
            {
                if (EnableComputer && ComputerPolPath != null) EnsurePolFileExists(ComputerPolPath);
                if (EnableUser && UserPolPath != null) EnsurePolFileExists(UserPolPath);
                SettingsService.Instance.UpdateCustomPolSettings(EnableComputer, EnableUser, ComputerPolPath, UserPolPath);
            }
            catch { args.Cancel = true; }
        }

        private static string? AutoCreateTempPol(bool isUser)
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "PolicyPlus", "CustomPol");
                Directory.CreateDirectory(dir);
                var name = isUser ? "user" : "machine";
                string path = Path.Combine(dir, name + ".pol");
                if (!File.Exists(path)) new PolFile().Save(path);
                return path;
            }
            catch { return null; }
        }

        private static void EnsurePolFileExists(string path)
        {
            try { if (!File.Exists(path)) new PolFile().Save(path); } catch { }
        }
    }
}
