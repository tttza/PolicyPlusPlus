using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System.Reflection;

namespace PolicyPlus.WinUI3.Dialogs
{
    public sealed partial class AboutDialog : ContentDialog
    {
        public AboutDialog()
        {
            this.InitializeComponent();
            LoadInfo();
        }

        private void LoadInfo()
        {
            try
            {
                // Use BuildInfo.Version so it matches the main window display (git describe output)
                string ver = BuildInfo.Version;
                var asm = typeof(App).Assembly;
                string? commit = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                                     .FirstOrDefault(a => a.Key == "CommitId")?.Value;
                AppTitle.Text = "PolicyPlusMod";
                VersionText.Text = "Version: " + ver;
                CommitText.Text = string.IsNullOrEmpty(commit) ? string.Empty : "Commit: " + commit;
            }
            catch { }
        }
    }
}
