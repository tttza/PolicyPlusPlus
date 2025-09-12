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
        private CustomPolSettings? _customPol; // unified state loaded once

        private FrameworkElement? RootElement => this.Content as FrameworkElement;
        private ToggleMenuFlyoutItem? GetToggleUseCustomPolMenu() => (RootElement?.FindName("ToggleUseCustomPolMenu") as ToggleMenuFlyoutItem);
        private TextBlock? GetLoaderInfo() => (RootElement?.FindName("SourceStatusText") as TextBlock);
        private AutoSuggestBox? GetSearchBox() => (RootElement?.FindName("SearchBox") as AutoSuggestBox);

        private async void BtnCustomPolSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureCustomPolLoaded();
                var dlg = new CustomPolSettingsDialog();
                if (Content is FrameworkElement fe) dlg.XamlRoot = fe.XamlRoot;
                dlg.Initialize(_customPol?.ComputerPath, _customPol?.UserPath, _customPol?.EnableComputer ?? false, _customPol?.EnableUser ?? false);
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    _customPol ??= new CustomPolSettings();
                    _customPol.ComputerPath = dlg.ComputerPolPath;
                    _customPol.UserPath = dlg.UserPolPath;
                    _customPol.EnableComputer = dlg.EnableComputer;
                    _customPol.EnableUser = dlg.EnableUser;
                    if (dlg.ActivateAfter)
                    {
                        _customPol.Active = _customPol.EnableComputer || _customPol.EnableUser;
                        var toggle = GetToggleUseCustomPolMenu(); if (toggle != null) toggle.IsChecked = _customPol.Active;
                        PersistCustomPol();
                        if (_customPol.Active) SwitchToCustomPol();
                    }
                    else
                    {
                        PersistCustomPol();
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
            EnsureCustomPolLoaded();
            bool desiredActive = (sender as ToggleMenuFlyoutItem)?.IsChecked == true;
            _customPol!.Active = desiredActive;
            if (_customPol.Active)
            {
                if (!_customPol.EnableComputer && !_customPol.EnableUser)
                {
                    _ = DispatcherQueue.TryEnqueue(async () =>
                    {
                        var dlg = new CustomPolSettingsDialog();
                        if (Content is FrameworkElement fe) dlg.XamlRoot = fe.XamlRoot;
                        dlg.Initialize(_customPol.ComputerPath, _customPol.UserPath, _customPol.EnableComputer, _customPol.EnableUser);
                        var res = await dlg.ShowAsync();
                        if (res == ContentDialogResult.Primary)
                        {
                            _customPol.ComputerPath = dlg.ComputerPolPath;
                            _customPol.UserPath = dlg.UserPolPath;
                            _customPol.EnableComputer = dlg.EnableComputer;
                            _customPol.EnableUser = dlg.EnableUser;
                            _customPol.Active = _customPol.EnableComputer || _customPol.EnableUser;
                            var toggle2 = GetToggleUseCustomPolMenu(); if (toggle2 != null) toggle2.IsChecked = _customPol.Active;
                            PersistCustomPol();
                            if (_customPol.Active) { SwitchToCustomPol(); return; }
                        }
                        _customPol.Active = false; var tgl = GetToggleUseCustomPolMenu(); if (tgl != null) tgl.IsChecked = false; PersistCustomPol(); ShowInfo("Custom POL not configured.", InfoBarSeverity.Warning);
                    });
                    return;
                }
                if ((_customPol.EnableComputer && string.IsNullOrEmpty(_customPol.ComputerPath)) || (_customPol.EnableUser && string.IsNullOrEmpty(_customPol.UserPath)))
                {
                    _ = DispatcherQueue.TryEnqueue(async () =>
                    {
                        var dlg = new CustomPolSettingsDialog();
                        if (Content is FrameworkElement fe) dlg.XamlRoot = fe.XamlRoot;
                        dlg.Initialize(_customPol.ComputerPath, _customPol.UserPath, _customPol.EnableComputer, _customPol.EnableUser);
                        var res = await dlg.ShowAsync();
                        if (res == ContentDialogResult.Primary)
                        {
                            _customPol.ComputerPath = dlg.ComputerPolPath;
                            _customPol.UserPath = dlg.UserPolPath;
                            _customPol.EnableComputer = dlg.EnableComputer;
                            _customPol.EnableUser = dlg.EnableUser;
                            _customPol.Active = _customPol.EnableComputer || _customPol.EnableUser;
                            var toggle3 = GetToggleUseCustomPolMenu(); if (toggle3 != null) toggle3.IsChecked = _customPol.Active;
                            PersistCustomPol();
                            if (_customPol.Active) SwitchToCustomPol(); else ShowInfo("Custom POL not configured.", InfoBarSeverity.Warning);
                        }
                        else
                        {
                            _customPol.Active = false; var tgl = GetToggleUseCustomPolMenu(); if (tgl != null) tgl.IsChecked = false; PersistCustomPol(); ShowInfo("Custom POL cancelled.", InfoBarSeverity.Informational);
                        }
                    });
                    return;
                }
                SwitchToCustomPol();
            }
            else
            {
                _customPol.EnableComputer = false; // disable both scopes when deactivating
                _customPol.EnableUser = false;
                PersistCustomPol();
                if (_useTempPol) PolicySourceManager.Instance.SwitchToTempPol(); else PolicySourceManager.Instance.EnsureLocalGpo();
                _compSource = PolicySourceManager.Instance.CompSource;
                _userSource = PolicySourceManager.Instance.UserSource;
                UpdateSourceStatusUnified();
                RefreshVisibleRows();
            }
        }

        private async Task PickBothPolAsync()
        {
            EnsureCustomPolLoaded();
            var comp = await PickFileAsync("Computer"); if (comp == null) return; _customPol!.ComputerPath = comp; _customPol.EnableComputer = true;
            var user = await PickFileAsync("User"); if (user == null) return; _customPol.UserPath = user; _customPol.EnableUser = true;
            EnsurePolFileExists(_customPol.ComputerPath); EnsurePolFileExists(_customPol.UserPath);
            _customPol.Active = true; PersistCustomPol(); var toggle = GetToggleUseCustomPolMenu(); if (toggle != null) toggle.IsChecked = true;
            SwitchToCustomPol(notify:false);
            ShowInfo("Custom .pol loaded.");
            UpdateSourceStatusUnified();
        }

        private async Task PickSinglePolAsync(bool isUser)
        {
            EnsureCustomPolLoaded();
            var picked = await PickFileAsync(isUser ? "User" : "Computer");
            if (picked == null) return;
            if (isUser) { _customPol!.UserPath = picked; _customPol.EnableUser = true; }
            else { _customPol!.ComputerPath = picked; _customPol.EnableComputer = true; }
            EnsurePolFileExists(picked); PersistCustomPol();
            ShowInfo((isUser ? "User" : "Computer") + " .pol set.");
            if (_customPol.Active && (!string.IsNullOrEmpty(_customPol.ComputerPath) || !string.IsNullOrEmpty(_customPol.UserPath))) { SwitchToCustomPol(); UpdateSourceStatusUnified(); }
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

        private static void EnsurePolFileExists(string? path)
        { try { if (!string.IsNullOrEmpty(path) && !File.Exists(path)) new PolFile().Save(path); } catch { } }

        private void SwitchToCustomPol(bool notify = true)
        {
            EnsureCustomPolLoaded();
            if (!_customPol!.EnableComputer && !_customPol.EnableUser) return;
            try
            {
                string? compPath = _customPol.EnableComputer ? _customPol.ComputerPath : null;
                string? userPath = _customPol.EnableUser ? _customPol.UserPath : null;
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
                UpdateSourceStatusUnified();
                RefreshVisibleRows();
                var sb = GetSearchBox(); RebindConsideringAsync(sb?.Text ?? string.Empty);
                if (notify) UpdateSourceStatusUnified();
            }
            catch { ShowInfo("Unable to switch to custom .pol", InfoBarSeverity.Error); }
        }

        private void UpdateSourceStatusUnified()
        {
            var loaderInfo = GetLoaderInfo(); if (loaderInfo != null)
            {
                loaderInfo.Text = SourceStatusFormatter.FormatStatus();
            }
            ShowInfo(SourceStatusFormatter.FormatStatus(), InfoBarSeverity.Informational);
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
                _customPol = SettingsService.Instance.LoadSettings().CustomPol;
                if (_customPol == null)
                {
                    _customPol = new CustomPolSettings();
                }
                var toggle = GetToggleUseCustomPolMenu(); if (toggle != null) toggle.IsChecked = _customPol.Active;
                if (_customPol.Active && (_customPol.EnableComputer || _customPol.EnableUser))
                {
                    SwitchToCustomPol(notify:false);
                }
                UpdateSourceStatusUnified();
            }
            catch { }
        }

        private void EnsureCustomPolLoaded()
        {
            if (_customPol == null)
            {
                try { _customPol = SettingsService.Instance.LoadSettings().CustomPol ?? new CustomPolSettings(); } catch { _customPol = new CustomPolSettings(); }
            }
        }

        private void PersistCustomPol()
        {
            try
            {
                if (_customPol != null)
                {
                    SettingsService.Instance.UpdateCustomPol(_customPol);
                }
            }
            catch { }
        }
    }

    internal static class SourceStatusFormatter
    {
        public static string FormatStatus()
        {
            var mgr = PolicySourceManager.Instance;
            return mgr.Mode switch
            {
                PolicySourceManager.PolicySourceMode.CustomPol => $"Custom POL (Comp: {System.IO.Path.GetFileName(mgr.CustomCompPath ?? "-")}, User: {System.IO.Path.GetFileName(mgr.CustomUserPath ?? "-")})",
                PolicySourceManager.PolicySourceMode.TempPol => "Temp POL",
                _ => "Local GPO"
            };
        }
    }
}
