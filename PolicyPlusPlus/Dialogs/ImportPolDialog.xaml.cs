using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Logging; // logging
using System;
using System.Text;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PolicyPlusPlus.Dialogs
{
    public sealed partial class ImportPolDialog : ContentDialog
    {
        public PolFile? Pol { get; private set; }
        public ImportPolDialog()
        {
            InitializeComponent();
            this.PrimaryButtonClick += ImportPolDialog_PrimaryButtonClick;
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window));
            picker.FileTypeFilter.Add(".pol");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
                PolPath.Text = file.Path;
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Pol = PolFile.Load(PolPath.Text);
                var sb = new StringBuilder();
                foreach (var key in Pol.GetKeyNames(string.Empty))
                {
                    sb.AppendLine("[" + key + "]");
                    foreach (var value in Pol.GetValueNames(key, false))
                    {
                        sb.AppendLine($"{value}");
                    }
                }
                PreviewBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                Log.Warn("ImportPol", $"Preview failed for path '{PolPath?.Text}'", ex);
                PreviewBox.Text = "Failed to load: " + ex.Message;
                Pol = null;
            }
        }

        private void ImportPolDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Pol is null)
                args.Cancel = true;
        }
    }
}
