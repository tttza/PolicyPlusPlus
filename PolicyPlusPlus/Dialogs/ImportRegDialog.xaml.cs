using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PolicyPlusCore.IO;
using PolicyPlusCore.Utilities;
using PolicyPlusPlus.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using StorageFileWinRT = Windows.Storage.StorageFile;

namespace PolicyPlusPlus.Dialogs
{
    public sealed partial class ImportRegDialog : ContentDialog
    {
        public RegFile? ParsedReg { get; private set; }
        private RegFile? _originalReg; // Unmodified source snapshot

        public ImportRegDialog()
        {
            InitializeComponent();
            this.PrimaryButtonClick += ImportRegDialog_PrimaryButtonClick;
            if (OnlyPoliciesSwitch != null)
            {
                OnlyPoliciesSwitch.Toggled += (_, __) =>
                {
                    RebuildParsedFromOriginal();
                    RefreshPreview();
                };
            }
        }

        private void Dialog_DragOver(object sender, DragEventArgs e)
        {
            // Accept a single .reg file.
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            else if (e.DataView.Contains(StandardDataFormats.Text))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void Dialog_Drop(object sender, DragEventArgs e)
        {
            try
            {
                string? candidatePath = null;
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    var file = items.FirstOrDefault() as StorageFileWinRT;
                    if (file != null)
                    {
                        candidatePath = file.Path;
                    }
                }
                else if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    var text = await e.DataView.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) && File.Exists(text.Trim()))
                        candidatePath = text.Trim();
                }

                if (string.IsNullOrWhiteSpace(candidatePath))
                    return;

                if (!candidatePath.EndsWith(".reg", StringComparison.OrdinalIgnoreCase))
                    return; // Only accept .reg

                RegPath.Text = candidatePath;
                TryLoad(candidatePath);
            }
            catch
            {
                // Swallow; UI should remain stable. User can still browse manually.
            }
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

        private void ImportRegDialog_PrimaryButtonClick(
            ContentDialog sender,
            ContentDialogButtonClickEventArgs args
        )
        {
            if (ParsedReg is null)
            {
                if (!TryLoad(RegPath.Text))
                {
                    args.Cancel = true;
                    return;
                }
            }
            else
            {
                // Ensure latest toggle state is reflected (rebuild just before Apply)
                RebuildParsedFromOriginal();
            }
        }

        private bool TryLoad(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            try
            {
                _originalReg = RegFile.Load(path, "");
                RebuildParsedFromOriginal();
                RefreshPreview();
                return true;
            }
            catch
            {
                _originalReg = null;
                ParsedReg = null;
                PreviewBox.Text = string.Empty;
                return false;
            }
        }

        private void RebuildParsedFromOriginal()
        {
            if (_originalReg == null)
            {
                ParsedReg = null;
                return;
            }
            try
            {
                ParsedReg = RegImportHelper.Clone(_originalReg);
                if (OnlyPoliciesSwitch != null && OnlyPoliciesSwitch.IsOn)
                {
                    RegImportHelper.FilterToPolicyKeysInPlace(ParsedReg);
                }
            }
            catch
            {
                ParsedReg = null;
            }
        }

        private void RefreshPreview()
        {
            if (ParsedReg == null)
            {
                PreviewBox.Text = string.Empty;
                return;
            }
            try
            {
                PreviewBox.Text = RegPreviewBuilder.BuildPreview(ParsedReg, maxPerHive: 500);
            }
            catch
            {
                PreviewBox.Text = string.Empty;
            }
        }
    }
}
