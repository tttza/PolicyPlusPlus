using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Dialogs;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.ViewModels; // view model
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private CustomPolViewModel? CustomPolVM =>
            (this.Content as FrameworkElement)?.DataContext as CustomPolViewModel;

        private FrameworkElement? RootElement => this.Content as FrameworkElement;

        private ToggleMenuFlyoutItem? GetToggleUseCustomPolMenu() =>
            (RootElement?.FindName("ToggleUseCustomPolMenu") as ToggleMenuFlyoutItem);

        private TextBlock? GetLoaderInfo() =>
            (RootElement?.FindName("SourceStatusText") as TextBlock);

        private AutoSuggestBox? GetSearchBox() =>
            (RootElement?.FindName("SearchBox") as AutoSuggestBox);

        private bool _customVmHooked;
        private bool _customPolConfigDialogOpen; // prevents duplicate dialogs

        // Tracks the mode in effect before switching to a custom POL so we can restore correctly (LocalGpo vs TempPol)
        private PolicySourceMode _previousNonCustomMode = PolicySourceMode.LocalGpo;

        private static bool IsCustomPolConfigured(CustomPolViewModel vm) =>
            (vm.EnableComputer && !string.IsNullOrEmpty(vm.ComputerPath))
            || (vm.EnableUser && !string.IsNullOrEmpty(vm.UserPath));

        private void TryHookCustomPolVm()
        {
            if (_customVmHooked)
                return;
            var vm = CustomPolVM;
            if (vm == null)
                return;
            vm.StateChanged += (_, __) => OnCustomPolStateChanged(vm);
            _customVmHooked = true;
            if (vm.Active && (vm.EnableComputer || vm.EnableUser))
            {
                if (IsCustomPolConfigured(vm))
                    SwitchToCustomPol(false);
                else
                    PromptConfigureCustomPol(vm);
            }
            SyncToggleFromVm();
        }

        private void SyncToggleFromVm()
        {
            try
            {
                var t = GetToggleUseCustomPolMenu();
                if (t != null && CustomPolVM != null)
                    t.IsChecked = CustomPolVM.Active;
            }
            catch { }
        }

        private void OnCustomPolStateChanged(CustomPolViewModel vm)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    SyncToggleFromVm();
                    // Persist snapshot so next launch reflects current toggle/paths/activation.
                    try
                    {
                        SettingsService.Instance.UpdateCustomPol(vm.Snapshot());
                    }
                    catch { }
                    if (!vm.Active)
                    {
                        // Restore previous non-custom mode.
                        RestorePreviousMode();
                        RefreshVisibleRows();
                        return;
                    }
                    // Active but not configured -> prompt
                    if (vm.Active && !IsCustomPolConfigured(vm))
                    {
                        PromptConfigureCustomPol(vm);
                        return;
                    }
                    if (vm.Active && (vm.EnableComputer || vm.EnableUser))
                    {
                        SwitchToCustomPol(false);
                    }
                }
                catch { }
            });
        }

        private void PromptConfigureCustomPol(CustomPolViewModel vm)
        {
            if (_customPolConfigDialogOpen)
                return;
            _customPolConfigDialogOpen = true;
            _ = DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var dlg = new CustomPolSettingsDialog();
                    if (Content is FrameworkElement fe)
                        dlg.XamlRoot = fe.XamlRoot;
                    dlg.Initialize(vm.ComputerPath, vm.UserPath, vm.EnableComputer, vm.EnableUser);
                    var res = await dlg.ShowAsync();
                    if (res == ContentDialogResult.Primary)
                    {
                        vm.EnableComputer = dlg.EnableComputer;
                        vm.EnableUser = dlg.EnableUser;
                        vm.ComputerPath = dlg.ComputerPolPath;
                        vm.UserPath = dlg.UserPolPath;
                        vm.Active = vm.EnableComputer || vm.EnableUser; // may trigger state changed again
                        if (vm.Active && IsCustomPolConfigured(vm))
                        {
                            SwitchToCustomPol();
                        }
                        else
                        {
                            vm.Active = false;
                            var t = GetToggleUseCustomPolMenu();
                            if (t != null)
                                t.IsChecked = false;
                            ShowInfo("Custom POL not configured.", InfoBarSeverity.Warning);
                        }
                    }
                    else
                    {
                        vm.Active = false;
                        var t = GetToggleUseCustomPolMenu();
                        if (t != null)
                            t.IsChecked = false;
                        ShowInfo("Custom POL not configured.", InfoBarSeverity.Warning);
                    }
                }
                catch
                {
                    vm.Active = false;
                }
                finally
                {
                    _customPolConfigDialogOpen = false;
                }
            });
        }

        private void RestorePreviousMode()
        {
            try
            {
                if (_previousNonCustomMode == PolicySourceMode.TempPol)
                    PolicySourceManager.Instance.Switch(PolicySourceDescriptor.TempPol());
                else
                    PolicySourceManager.Instance.Switch(PolicySourceDescriptor.LocalGpo());
            }
            catch { }
        }

        private async void BtnCustomPolSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TryHookCustomPolVm();
                var vm =
                    CustomPolVM
                    ?? new CustomPolViewModel(
                        SettingsService.Instance,
                        (_settingsCache ?? SettingsService.Instance.LoadSettings()).CustomPol
                    );
                var dlg = new CustomPolSettingsDialog();
                if (Content is FrameworkElement fe)
                    dlg.XamlRoot = fe.XamlRoot;
                dlg.Initialize(vm.ComputerPath, vm.UserPath, vm.EnableComputer, vm.EnableUser);
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    vm.EnableComputer = dlg.EnableComputer;
                    vm.EnableUser = dlg.EnableUser;
                    vm.ComputerPath = dlg.ComputerPolPath;
                    vm.UserPath = dlg.UserPolPath;
                    if (dlg.ActivateAfter)
                    {
                        vm.Active = (vm.EnableComputer || vm.EnableUser);
                        if (vm.Active)
                            SwitchToCustomPol();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Custom .pol settings failed: " + ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void BtnLoadCustomPol_Click(object sender, RoutedEventArgs e) =>
            await PickBothPolAsync();

        private async void BtnPickCustomCompPol_Click(object sender, RoutedEventArgs e) =>
            await PickSinglePolAsync(isUser: false);

        private async void BtnPickCustomUserPol_Click(object sender, RoutedEventArgs e) =>
            await PickSinglePolAsync(isUser: true);

        private void ToggleUseCustomPolMenu_Click(object sender, RoutedEventArgs e)
        {
            TryHookCustomPolVm();
            var vm = CustomPolVM;
            if (vm == null)
                return;
            bool desired = (sender as ToggleMenuFlyoutItem)?.IsChecked == true;

            if (!desired)
            {
                if (vm.Active)
                    vm.Active = false;
                else
                {
                    RestorePreviousMode();
                    RefreshVisibleRows();
                }
                return;
            }

            if (!IsCustomPolConfigured(vm))
            {
                PromptConfigureCustomPol(vm);
                return;
            }

            if (!vm.Active)
                vm.Active = true;
            else
                SwitchToCustomPol();
        }

        private async Task PickBothPolAsync()
        {
            TryHookCustomPolVm();
            var vm = CustomPolVM;
            if (vm == null)
                return;
            var comp = await PickFileAsync("Computer");
            if (comp == null)
                return;
            vm.ComputerPath = comp;
            vm.EnableComputer = true;
            var user = await PickFileAsync("User");
            if (user == null)
                return;
            vm.UserPath = user;
            vm.EnableUser = true;
            EnsurePolFileExists(vm.ComputerPath);
            EnsurePolFileExists(vm.UserPath);
            vm.Active = true;
            SwitchToCustomPol(false);
            ShowInfo("Custom .pol loaded.");
        }

        private async Task PickSinglePolAsync(bool isUser)
        {
            TryHookCustomPolVm();
            var vm = CustomPolVM;
            if (vm == null)
                return;
            var picked = await PickFileAsync(isUser ? "User" : "Computer");
            if (picked == null)
                return;
            if (isUser)
            {
                vm.UserPath = picked;
                vm.EnableUser = true;
            }
            else
            {
                vm.ComputerPath = picked;
                vm.EnableComputer = true;
            }
            EnsurePolFileExists(picked);
            ShowInfo((isUser ? "User" : "Computer") + " .pol set.");
            if (vm.Active && (vm.ComputerPath != null || vm.UserPath != null))
            {
                SwitchToCustomPol();
            }
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
                if (file == null)
                {
                    ShowInfo(label + " .pol not selected.", InfoBarSeverity.Warning);
                    return null;
                }
                return file.Path;
            }
            catch (Exception ex)
            {
                ShowInfo("Failed to pick .pol: " + ex.Message, InfoBarSeverity.Error);
                return null;
            }
        }

        private static void EnsurePolFileExists(string? path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && !File.Exists(path))
                    new PolFile().Save(path);
            }
            catch { }
        }

        private void SwitchToCustomPol(bool notify = true)
        {
            var vm = CustomPolVM;
            if (vm == null)
                return;
            if (!vm.EnableComputer && !vm.EnableUser)
                return;
            try
            {
                if (PolicySourceManager.Instance.Mode != PolicySourceMode.CustomPol)
                    _previousNonCustomMode = PolicySourceManager.Instance.Mode;

                string? compPath = vm.EnableComputer ? vm.ComputerPath : null;
                string? userPath = vm.EnableUser ? vm.UserPath : null;
                if (
                    !PolicySourceManager.Instance.SwitchCustomPolFlexible(
                        compPath,
                        userPath,
                        allowSingle: true
                    )
                )
                    return;
                var sb = GetSearchBox();
                RebindConsideringAsync(sb?.Text ?? string.Empty, showBaselineOnEmpty: false);
                if (notify)
                    UpdateSourceStatusUnified();
            }
            catch
            {
                ShowInfo("Unable to switch to custom .pol", InfoBarSeverity.Error);
            }
        }

        private void SaveCustomPolIfActive()
        {
            if (PolicySourceManager.Instance.Mode != PolicySourceMode.CustomPol)
                return;
            try
            {
                if (
                    PolicySourceManager.Instance.CompSource is PolFile c
                    && !string.IsNullOrEmpty(PolicySourceManager.Instance.CustomCompPath)
                )
                    c.Save(PolicySourceManager.Instance.CustomCompPath);
                if (
                    PolicySourceManager.Instance.UserSource is PolFile u
                    && !string.IsNullOrEmpty(PolicySourceManager.Instance.CustomUserPath)
                )
                    u.Save(PolicySourceManager.Instance.CustomUserPath);
            }
            catch (Exception ex)
            {
                ShowInfo("Custom .pol save failed: " + ex.Message, InfoBarSeverity.Error);
            }
        }

        private void AfterAppliedChangesCustomPol() => SaveCustomPolIfActive();

        private void LoadCustomPolSettings()
        {
            TryHookCustomPolVm();
            UpdateSourceStatusUnified();
        }
    }

    internal static class SourceStatusFormatter
    {
        public static string FormatStatus()
        {
            var mgr = PolicySourceManager.Instance;
            return mgr.Mode switch
            {
                PolicySourceMode.CustomPol =>
                    $"Custom POL (Comp: {System.IO.Path.GetFileName(mgr.CustomCompPath ?? "-")}, User: {System.IO.Path.GetFileName(mgr.CustomUserPath ?? "-")})",
                PolicySourceMode.TempPol => "Temp POL",
                _ => "Local GPO",
            };
        }
    }
}
