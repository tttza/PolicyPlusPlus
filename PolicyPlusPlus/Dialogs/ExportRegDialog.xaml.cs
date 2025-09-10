using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using PolicyPlus.Core.IO;

using System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PolicyPlusPlus.Dialogs
{
    public sealed partial class ExportRegDialog : ContentDialog
    {
        private PolFile _source = null!;
        public ExportRegDialog()
        {
            this.InitializeComponent();
            this.PrimaryButtonClick += ExportRegDialog_PrimaryButtonClick;
        }

        public void Initialize(PolFile pol, bool isUser)
        {
            _source = pol;
            TextReg.Text = string.Empty;
            TextBranch.Text = string.Empty;
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window));
            picker.FileTypeChoices.Add("Registry scripts", new System.Collections.Generic.List<string> { ".reg" });
            picker.SuggestedFileName = "export";
            var file = await picker.PickSaveFileAsync();
            if (file != null)
                TextReg.Text = file.Path;
        }

        private void ExportRegDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(TextReg.Text))
            {
                args.Cancel = true;
                return;
            }
            try
            {
                var reg = new RegFile();
                reg.SetPrefix(string.Empty);
                reg.SetSourceBranch(TextBranch.Text ?? string.Empty);
                _source.Apply(reg);
                reg.Save(TextReg.Text);
            }
            catch
            {
                args.Cancel = true;
            }
        }
    }
}
