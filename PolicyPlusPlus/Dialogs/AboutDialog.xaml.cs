using System.Linq;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace PolicyPlusPlus.Dialogs
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
                    .FirstOrDefault(a => a.Key == "CommitId")
                    ?.Value;
                AppTitle.Text = "Policy++";
                VersionText.Text = "Version: " + ver;
                CommitText.Text = string.IsNullOrEmpty(commit) ? string.Empty : "Commit: " + commit;
            }
            catch { }
        }
    }
}
