using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.ViewModels;
using PolicyPlusPlus.Windows;
using Windows.ApplicationModel.DataTransfer;
using Windows.System; // for VirtualKey

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private void SaveAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            BtnSave_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private void FindAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                _suppressInitialSearchBoxFocus = false; // explicit user intent to focus search
                SearchBox?.Focus(FocusState.Programmatic);
            }
            catch { }
            args.Handled = true;
        }

        private void OpenPendingAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnPendingChanges_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void LoadAdmxAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnLoadAdmxFolder_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void LoadLocalGpoAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnLoadLocalGpo_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void ExportRegAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnExportReg_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void ImportRegAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnImportReg_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void ImportPolAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnImportPol_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void OpenHistoryAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                BtnHistoryChanges_Click(this, new RoutedEventArgs());
            }
            catch { }
            args.Handled = true;
        }

        private void ToggleDetailsAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                if (ViewDetailsToggle != null)
                {
                    bool current = ViewDetailsToggle.IsChecked == true;
                    ViewDetailsToggle.IsChecked = !current;
                    ViewDetailsToggle_Click(ViewDetailsToggle, new RoutedEventArgs());
                }
            }
            catch { }
            args.Handled = true;
        }

        private void RefreshAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
            }
            catch { }
            args.Handled = true;
        }

        private void ToggleBookmarkAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                if (PolicyList?.SelectedItem is Models.PolicyListRow row && row.Policy != null)
                {
                    BookmarkService.Instance.Toggle(row.Policy.UniqueID);
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
            catch { }
            args.Handled = true;
        }

        private async void EditSelectedAccelerator_Invoked(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                if (PolicyList?.SelectedItem is Models.PolicyListRow row && row.Policy != null)
                {
                    await OpenEditDialogForPolicyAsync(row.Policy, ensureFront: true);
                    args.Handled = true;
                    return;
                }
            }
            catch { }
            args.Handled = true;
        }

        private void BtnLoadLocalGpo_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                PolicySourceManager.Instance.Switch(PolicySourceDescriptor.LocalGpo());
                UpdateSourceStatusUnified();
                RefreshVisibleRows();
                ShowInfo("Local GPO loaded.");
            }
            catch
            {
                ShowInfo("Failed to load Local GPO", InfoBarSeverity.Error);
            }
        }

        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p =
                    GetContextMenuPolicy(sender)
                    ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
                if (p != null)
                    await OpenEditDialogForPolicyAsync(p, ensureFront: true);
            }
            catch { }
        }

        private void ContextViewFormatted_Click(object sender, RoutedEventArgs e) =>
            BtnViewFormatted_Click(sender, e);

        private void ContextBookmarkToggle_Click(object sender, RoutedEventArgs e)
        {
            var p =
                GetContextMenuPolicy(sender)
                ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p == null)
                return;
            try
            {
                BookmarkService.Instance.Toggle(p.UniqueID);
            }
            catch { }
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
        }

        private PolicyPlusPolicy? GetContextMenuPolicy(object sender)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.Tag is PolicyPlusPolicy p1)
                    return p1;
                if (fe.DataContext is Models.PolicyListRow row && row.Policy != null)
                    return row.Policy;
            }
            return null;
        }

        private void ContextCopyName_Click(object sender, RoutedEventArgs e)
        {
            var p =
                GetContextMenuPolicy(sender)
                ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p != null)
            {
                var text = p.DisplayName ?? string.Empty;
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(text);
                Clipboard.SetContent(dp);
            }
        }

        private void ContextCopyId_Click(object sender, RoutedEventArgs e)
        {
            var p =
                GetContextMenuPolicy(sender)
                ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p != null)
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(p.UniqueID);
                Clipboard.SetContent(dp);
            }
        }

        private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
        {
            var p =
                GetContextMenuPolicy(sender)
                ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p == null)
                return;
            var sb = new StringBuilder();
            var c = p.Category;
            var stack = new Stack<string>();
            while (c != null)
            {
                stack.Push(c.DisplayName ?? string.Empty);
                c = c.Parent;
            }
            sb.AppendLine("Administrative Templates");
            foreach (var name in stack)
                sb.AppendLine("+ " + name);
            sb.AppendLine("+ " + (p.DisplayName ?? string.Empty));
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(sb.ToString());
            Clipboard.SetContent(dp);
        }

        private void ContextRevealInTree_Click(object sender, RoutedEventArgs e)
        {
            var p =
                GetContextMenuPolicy(sender)
                ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p == null || CategoryTree == null)
                return;
            _selectedCategory = p.Category;
            SelectCategoryInTree(_selectedCategory);
            UpdateSearchPlaceholder();
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void ContextCopyRegExport_Click(object sender, RoutedEventArgs e)
        {
            var p =
                GetContextMenuPolicy(sender)
                ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p == null)
                return;
            var section = p.RawPolicy.Section switch
            {
                AdmxPolicySection.User => AdmxPolicySection.User,
                AdmxPolicySection.Machine => AdmxPolicySection.Machine,
                _ => (
                    _appliesFilter == AdmxPolicySection.User
                        ? AdmxPolicySection.User
                        : AdmxPolicySection.Machine
                ),
            };
            var ctx = PolicySourceAccessor.Acquire();
            var src = section == AdmxPolicySection.User ? ctx.User : ctx.Comp;
            var text = RegistryViewFormatter.BuildRegExport(p, src, section) ?? string.Empty;
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            ShowInfo("Copied .reg export to clipboard.");
        }

        private void BtnPendingChanges_Click(object sender, RoutedEventArgs e)
        {
            var win = new PendingChangesWindow();
            win.Activate();
            try
            {
                WindowHelpers.BringToFront(win);
            }
            catch { }
        }

        private void BtnHistoryChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new PendingChangesWindow();
                win.Activate();
                try
                {
                    WindowHelpers.BringToFront(win);
                }
                catch { }
                try
                {
                    win.SelectHistoryTab();
                }
                catch { }
            }
            catch { }
        }

        private void ToggleHideEmptyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                _hideEmptyCategories = t.IsChecked;
                try
                {
                    SettingsService.Instance.UpdateHideEmptyCategories(_hideEmptyCategories);
                }
                catch { }
                BuildCategoryTree();
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            }
        }

        private void ToggleTempPolMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleMenuFlyoutItem t)
                return;
            try
            {
                if (t.IsChecked == true)
                {
                    PolicySourceManager.Instance.Switch(PolicySourceDescriptor.TempPol());
                }
                else if (PolicySourceManager.Instance.Mode == PolicySourceMode.TempPol)
                {
                    PolicySourceManager.Instance.Switch(PolicySourceDescriptor.LocalGpo());
                }
                UpdateSourceStatusUnified();
                RefreshVisibleRows();
            }
            catch
            {
                ShowInfo("Temp POL toggle failed", InfoBarSeverity.Error);
            }
        }

        private void BookmarkToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is PolicyPlusPolicy p)
            {
                try
                {
                    BookmarkService.Instance.Toggle(p.UniqueID);
                }
                catch { }
                if (_bookmarksOnly)
                {
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
        }

        private void ChkBookmarksOnly_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressBookmarksOnlyChanged)
                return;
            _bookmarksOnly = (sender as CheckBox)?.IsChecked == true;
            try
            {
                SettingsService.Instance.UpdateBookmarksOnly(_bookmarksOnly);
            }
            catch { }
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
        }

        private void BookmarkFilterMenu_Click(object sender, RoutedEventArgs e)
        {
            _bookmarksOnly = !_bookmarksOnly;
            try
            {
                if (ChkBookmarksOnly != null)
                {
                    _suppressBookmarksOnlyChanged = true;
                    ChkBookmarksOnly.IsChecked = _bookmarksOnly;
                    _suppressBookmarksOnlyChanged = false;
                }
                SettingsService.Instance.UpdateBookmarksOnly(_bookmarksOnly);
            }
            catch { }
            RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
        }

        private void BookmarkManageMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new ManageBookmarksWindow();
                win.Activate();
                try
                {
                    WindowHelpers.BringToFront(win);
                }
                catch { }
            }
            catch
            {
                ShowInfo("Could not open bookmark manager.");
            }
        }

        private void OpenQuickEditBookmarks()
        {
            try
            {
                if (_bundle == null)
                    return;
                var bookmarkIds = BookmarkService.Instance.ActiveIds;
                if (bookmarkIds == null || !bookmarkIds.Any())
                {
                    ShowInfo("Add at least one bookmark to use Quick Edit.");
                    return;
                }
                var set = new HashSet<string>(bookmarkIds, System.StringComparer.OrdinalIgnoreCase);
                var policies = _allPolicies.Where(p => set.Contains(p.UniqueID)).ToList();
                if (policies.Count == 0)
                    return;
                var ctx = PolicySourceAccessor.Acquire();
                var win = new Windows.QuickEditWindow();
                win.Initialize(_bundle, ctx.Comp, ctx.User, policies);
                win.Activate();
                try
                {
                    Utils.WindowHelpers.BringToFront(win);
                }
                catch { }
            }
            catch { }
        }

        private void ContextQuickEdit_Click(object sender, RoutedEventArgs e)
        {
            OpenQuickEditBookmarks();
        }

        private void BtnQuickEdit_Click(object sender, RoutedEventArgs e)
        {
            OpenQuickEditBookmarks();
        }

        private void BookmarkToggle_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void ViewBookmarkToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ColBookmark != null && sender is ToggleMenuFlyoutItem t)
                {
                    ColBookmark.Visibility = t.IsChecked
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    var flag = RootGrid?.FindName("ColBookmarkFlag") as CheckBox;
                    if (flag != null)
                        flag.IsChecked = t.IsChecked;
                }
            }
            catch { }
        }

        private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new LogViewerWindow();
                win.Activate();
                WindowHelpers.BringToFront(win);
            }
            catch { }
        }

        private async void PolicyList_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                try
                {
                    if (PolicyList?.SelectedItem is Models.PolicyListRow row && row.Policy != null)
                    {
                        await OpenEditDialogForPolicyAsync(row.Policy, ensureFront: true);
                        e.Handled = true;
                    }
                }
                catch { }
            }
            else if (e.Key == VirtualKey.Escape)
            {
                if (FocusSearchBoxForRefinement("ListEscape"))
                    e.Handled = true;
            }
        }

        private void PolicyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                UpdateDetailsFromSelection();
            }
            catch { }
        }
    }
}
