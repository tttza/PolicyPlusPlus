using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.IO;
using System.Threading.Tasks;
using PolicyPlusCore.IO;
using PolicyPlusCore.Core;
using System;
using PolicyPlusPlus.Dialogs;
using PolicyPlusPlus.Services;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private string? _customPolCompPath;
        private string? _customPolUserPath;
        private bool _useCustomPol;

        private bool _customPolEnableComp;
        private bool _customPolEnableUser;

        private FrameworkElement? RootElement => this.Content as FrameworkElement;
        private ToggleMenuFlyoutItem? GetToggleUseCustomPolMenu() => (RootElement?.FindName("ToggleUseCustomPolMenu") as ToggleMenuFlyoutItem);
        private TextBlock? GetLoaderInfo() => (RootElement?.FindName("SourceStatusText") as TextBlock);
        private AutoSuggestBox? GetSearchBox() => (RootElement?.FindName("SearchBox") as AutoSuggestBox);

        private async void BtnCustomPolSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new CustomPolSettingsDialog();
                if (Content is FrameworkElement fe) dlg.XamlRoot = fe.XamlRoot;
                dlg.Initialize(_customPolCompPath, _customPolUserPath, _customPolEnableComp, _customPolEnableUser);
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    _customPolCompPath = dlg.ComputerPolPath;
                    _customPolUserPath = dlg.UserPolPath;
                    _customPolEnableComp = dlg.EnableComputer;
                    _customPolEnableUser = dlg.EnableUser;
                    if (dlg.ActivateAfter)
                    {
                        _useCustomPol = _customPolEnableComp || _customPolEnableUser;
                        var toggle = GetToggleUseCustomPolMenu(); if (toggle != null) toggle.IsChecked = _useCustomPol;
                        if (_useCustomPol) SwitchToCustomPol();
                    }
                }
            }
            catch (Exception ex) { ShowInfo("Custom .pol settings failed: " + ex.Message, InfoBarSeverity.Error); }
        }

        private async void BtnLoadCustomPol_Click(object sender, RoutedEventArgs e) => await PickBothPolAsync();
        private async void BtnPickCustomCompPol_Click(object sender, RoutedEventArgs e) => await PickSinglePolAsync(isUser: false);
        private async void BtnPickCustomUserPol_Click(object sender, RoutedEventArgs e) => await PickSinglePolAsync(isUser: true);

        private void ToggleUseCustomPolMenu_Click(object sender, RoutedEventArgs e)
        {
            _useCustomPol = (sender as ToggleMenuFlyoutItem)?.IsChecked == true;
            if (_useCustomPol)
            {
                // If both scopes disabled, force configuration instead of silently doing nothing
                if (!_customPolEnableComp && !_customPolEnableUser)
                {
                    _ = DispatcherQueue.TryEnqueue(async () =>
                    {
                        var dlg = new CustomPolSettingsDialog();
                        if (Content is FrameworkElement fe) dlg.XamlRoot = fe.XamlRoot;
                        dlg.Initialize(_customPolCompPath, _customPolUserPath, _customPolEnableComp, _customPolEnableUser);
                        var res = await dlg.ShowAsync();
                        if (res == ContentDialogResult.Primary)
                        {
                            _customPolCompPath = dlg.ComputerPolPath;
                            _customPolUserPath = dlg.UserPolPath;
                            _customPolEnableComp = dlg.EnableComputer;
                            _customPolEnableUser = dlg.EnableUser;
                            _useCustomPol = _customPolEnableComp || _customPolEnableUser;
                            var toggle2 = GetToggleUseCustomPolMenu(); if (toggle2 != null) toggle2.IsChecked = _useCustomPol;
                            if (_useCustomPol) { SwitchToCustomPol(); return; }
                        }
                        // User cancelled or still not configured
                        _useCustomPol = false; var tgl = GetToggleUseCustomPolMenu(); if (tgl != null) tgl.IsChecked = false; ShowInfo("Custom POL not configured.", InfoBarSeverity.Warning);
                    });
                    return;
                }
                if (string.IsNullOrEmpty(_customPolCompPath) && _customPolEnableComp)
                {
                    ShowInfo("Computer custom POL path not set.", InfoBarSeverity.Warning);
                }
                if (string.IsNullOrEmpty(_customPolUserPath) && _customPolEnableUser)
                {
                    ShowInfo("User custom POL path not set.", InfoBarSeverity.Warning);
                }
                if ((_customPolEnableComp && string.IsNullOrEmpty(_customPolCompPath)) || (_customPolEnableUser && string.IsNullOrEmpty(_customPolUserPath)))
                {
                    // Open settings to let user fill paths
                    _ = DispatcherQueue.TryEnqueue(async () =>
                    {
                        var dlg = new CustomPolSettingsDialog();
                        if (Content is FrameworkElement fe) dlg.XamlRoot = fe.XamlRoot;
                        dlg.Initialize(_customPolCompPath, _customPolUserPath, _customPolEnableComp, _customPolEnableUser);
                        var res = await dlg.ShowAsync();
                        if (res == ContentDialogResult.Primary)
                        {
                            _customPolCompPath = dlg.ComputerPolPath;
                            _customPolUserPath = dlg.UserPolPath;
                            _customPolEnableComp = dlg.EnableComputer;
                            _customPolEnableUser = dlg.EnableUser;
                            _useCustomPol = _customPolEnableComp || _customPolEnableUser;
                            var toggle3 = GetToggleUseCustomPolMenu(); if (toggle3 != null) toggle3.IsChecked = _useCustomPol;
                            if (_useCustomPol) SwitchToCustomPol(); else ShowInfo("Custom POL not configured.", InfoBarSeverity.Warning);
                        }
                        else
                        {
                            _useCustomPol = false; var tgl = GetToggleUseCustomPolMenu(); if (tgl != null) tgl.IsChecked = false; ShowInfo("Custom POL cancelled.", InfoBarSeverity.Informational);
                        }
                    });
                    return;
                }
                if (string.IsNullOrEmpty(_customPolCompPath) || string.IsNullOrEmpty(_customPolUserPath))
                {
                    // Mixed enable state handled in SwitchToCustomPol; proceed
                    SwitchToCustomPol();
                }
                else
                {
                    SwitchToCustomPol();
                }
            }
            else
            {
                // Persist disabling so it does not auto-enable on next launch
                _customPolEnableComp = false;
                _customPolEnableUser = false;
                try { SettingsService.Instance.UpdateCustomPolSettings(false, false, _customPolCompPath, _customPolUserPath); } catch { }
                if (_useTempPol) PolicySourceManager.Instance.SwitchToTempPol(); else PolicySourceManager.Instance.EnsureLocalGpo();
                _compSource = PolicySourceManager.Instance.CompSource; // keep local cache in sync
                _userSource = PolicySourceManager.Instance.UserSource;
                UpdateLoaderInfo();
                ShowActiveSourceInfo();
                RefreshVisibleRows();
            }
        }

        private async Task PickBothPolAsync()
        {
            var comp = await PickFileAsync("Computer"); if (comp == null) return; _customPolCompPath = comp;
            var user = await PickFileAsync("User"); if (user == null) return; _customPolUserPath = user;
            EnsurePolFileExists(_customPolCompPath); EnsurePolFileExists(_customPolUserPath);
            _useCustomPol = true; var toggle = GetToggleUseCustomPolMenu(); if (toggle != null) toggle.IsChecked = true;
            SwitchToCustomPol(notify:false); // custom message below
            ShowInfo("Custom .pol loaded.");
            ShowActiveSourceInfo();
        }

        private async Task PickSinglePolAsync(bool isUser)
        {
            var picked = await PickFileAsync(isUser ? "User" : "Computer");
            if (picked == null) return;
            if (isUser) _customPolUserPath = picked; else _customPolCompPath = picked;
            EnsurePolFileExists(picked);
            ShowInfo((isUser ? "User" : "Computer") + " .pol set.");
            if (_useCustomPol && !string.IsNullOrEmpty(_customPolCompPath) && !string.IsNullOrEmpty(_customPolUserPath)) { SwitchToCustomPol(); ShowActiveSourceInfo(); }
        }

        private async Task<string?> PickFileAsync(string label)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add(".pol");
                var file = await picker.PickSingleFileAsync();
                if (file == null) { ShowInfo(label + " .pol not selected.", InfoBarSeverity.Warning); return null; }
                return file.Path;
            }
            catch (Exception ex)
            {
                ShowInfo("Failed to pick .pol: " + ex.Message, InfoBarSeverity.Error);
                return null;
            }
        }

        private static void EnsurePolFileExists(string path)
        { try { if (!File.Exists(path)) new PolFile().Save(path); } catch { } }

        private void SwitchToCustomPol(bool notify = true)
        {
            if (!_customPolEnableComp && !_customPolEnableUser) return;
            try
            {
                string? compPath = _customPolEnableComp ? _customPolCompPath : null;
                string? userPath = _customPolEnableUser ? _customPolUserPath : null;
                if (compPath == null && userPath == null) return;
                if (compPath != null && !File.Exists(compPath)) { try { new PolFile().Save(compPath); } catch { } }
                if (userPath != null && !File.Exists(userPath)) { try { new PolFile().Save(userPath); } catch { } }
                if (compPath != null && userPath != null)
                {
                    PolicySourceManager.Instance.SwitchToCustomPol(compPath, userPath);
                }
                else if (compPath != null)
                {
                    var tempUser = Path.Combine(Path.GetTempPath(), "PolicyPlus", "_empty_user.pol");
                    try { if (!File.Exists(tempUser)) new PolFile().Save(tempUser); } catch { }
                    PolicySourceManager.Instance.SwitchToCustomPol(compPath, tempUser);
                }
                else if (userPath != null)
                {
                    var tempComp = Path.Combine(Path.GetTempPath(), "PolicyPlus", "_empty_machine.pol");
                    try { if (!File.Exists(tempComp)) new PolFile().Save(tempComp); } catch { }
                    PolicySourceManager.Instance.SwitchToCustomPol(tempComp, userPath);
                }
                _compSource = PolicySourceManager.Instance.CompSource;
                _userSource = PolicySourceManager.Instance.UserSource;
                UpdateLoaderInfo();
                RefreshVisibleRows();
                var sb = GetSearchBox(); RebindConsideringAsync(sb?.Text ?? string.Empty);
                if (notify) ShowActiveSourceInfo();
            }
            catch { ShowInfo("Unable to switch to custom .pol", InfoBarSeverity.Error); }
        }

        private void UpdateLoaderInfo()
        {
            var loaderInfo = GetLoaderInfo(); if (loaderInfo != null)
            {
                var mode = PolicySourceManager.Instance.Mode;
                loaderInfo.Text = mode switch
                {
                    PolicySourceManager.PolicySourceMode.CustomPol => "Custom POL",
                    PolicySourceManager.PolicySourceMode.TempPol => "Temp POL",
                    _ => PolicySourceManager.Instance.CompSource is not null ? "Local GPO" : "(No Source)"
                };
            }
        }

        private void SaveCustomPolIfActive()
        {
            if (PolicySourceManager.Instance.Mode != PolicySourceManager.PolicySourceMode.CustomPol) return;
            try
            {
                if (PolicySourceManager.Instance.CompSource is PolFile c && !string.IsNullOrEmpty(PolicySourceManager.Instance.CustomCompPath)) c.Save(PolicySourceManager.Instance.CustomCompPath);
                if (PolicySourceManager.Instance.UserSource is PolFile u && !string.IsNullOrEmpty(PolicySourceManager.Instance.CustomUserPath)) u.Save(PolicySourceManager.Instance.CustomUserPath);
            }
            catch (Exception ex) { ShowInfo("Custom .pol save failed: " + ex.Message, InfoBarSeverity.Error); }
        }

        private void AfterAppliedChangesCustomPol() => SaveCustomPolIfActive();

        private void LoadCustomPolSettings()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                _customPolEnableComp = s.CustomPolEnableComputer ?? false;
                _customPolEnableUser = s.CustomPolEnableUser ?? false;
                _customPolCompPath = _customPolEnableComp ? s.CustomPolCompPath : null;
                _customPolUserPath = _customPolEnableUser ? s.CustomPolUserPath : null;
                _useCustomPol = _customPolEnableComp || _customPolEnableUser;
                var toggle = GetToggleUseCustomPolMenu(); if (toggle != null) toggle.IsChecked = _useCustomPol;
                if (_useCustomPol && (!string.IsNullOrEmpty(_customPolCompPath) || !string.IsNullOrEmpty(_customPolUserPath)))
                {
                    SwitchToCustomPol(notify:false);
                }
                // Ensure status text reflects active source (including custom POL file names) after initialization
                ShowActiveSourceInfo();
            }
            catch { }
        }
    }
}
