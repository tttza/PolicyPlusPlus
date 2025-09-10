using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using PolicyPlus.Core.IO;
using PolicyPlus.Core.Utilities;

using System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PolicyPlus.WinUI3.Dialogs
{
    public sealed partial class ImportRegDialog : ContentDialog
    {
        public RegFile? ParsedReg { get; private set; }
        public ImportRegDialog()
        {
            InitializeComponent();
            this.PrimaryButtonClick += ImportRegDialog_PrimaryButtonClick;
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Window));
            picker.FileTypeFilter.Add(".reg");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                RegPath.Text = file.Path;
                TryLoad(file.Path);
            }
        }

        private void ImportRegDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ParsedReg is null)
            {
                if (!TryLoad(RegPath.Text))
                { args.Cancel = true; return; }
            }
        }

        private bool TryLoad(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                ParsedReg = RegFile.Load(path, "");
                PreviewBox.Text = RegPreviewBuilder.BuildPreview(ParsedReg, maxPerHive: 500);
                return true;
            }
            catch
            {
                ParsedReg = null; PreviewBox.Text = string.Empty; return false;
            }
        }
    }
}
