using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Text;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PolicyPlus.WinUI3.Dialogs
{
    public sealed partial class ImportRegDialog : ContentDialog
    {
        public RegFile? ParsedReg { get; private set; }
        public string? RootPrefix { get; private set; }
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
                RegPath.Text = file.Path;
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var root = ((RootSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "HKEY_LOCAL_MACHINE");
                RootPrefix = root + "\\";
                ParsedReg = RegFile.Load(RegPath.Text, RootPrefix);
                var sb = new StringBuilder();
                foreach (var k in ParsedReg.Keys)
                {
                    sb.AppendLine("[" + k.Name + "]");
                    foreach (var v in k.Values)
                        sb.AppendLine($"{v.Name} = {v.Kind} {v.Data}");
                    sb.AppendLine();
                }
                PreviewBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                PreviewBox.Text = "Failed to parse: " + ex.Message;
                ParsedReg = null;
            }
        }

        private void ImportRegDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ParsedReg is null)
            {
                args.Cancel = true;
            }
        }
    }
}
