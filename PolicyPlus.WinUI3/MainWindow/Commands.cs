using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using PolicyPlus.WinUI3.Windows;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.ViewModels;
using PolicyPlus.WinUI3.Utils;
using PolicyPlus.Core.IO;
using PolicyPlus.Core.Core;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { BtnSave_Click(this, new RoutedEventArgs()); args.Handled = true; }

        private void FindAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { SearchBox?.Focus(FocusState.Programmatic); } catch { } args.Handled = true; }

        private void OpenPendingAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnPendingChanges_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void LoadAdmxAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnLoadAdmxFolder_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void LoadLocalGpoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnLoadLocalGpo_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void ExportRegAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnExportReg_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void ImportRegAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnImportReg_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void ImportPolAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { BtnImportPol_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }

        private void ToggleDetailsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
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

        private void RefreshAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { RebindConsideringAsync(SearchBox?.Text ?? string.Empty); } catch { } args.Handled = true; }

        private void ToggleBookmarkAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            try
            {
                if (PolicyList?.SelectedItem is Models.PolicyListRow row && row.Policy != null)
                { BookmarkService.Instance.Toggle(row.Policy.UniqueID); RebindConsideringAsync(SearchBox?.Text ?? string.Empty); }
            }
            catch { }
            args.Handled = true;
        }

        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
                if (p != null) await OpenEditDialogForPolicyAsync(p, ensureFront: true);
            }
            catch { }
        }

        private void ContextViewFormatted_Click(object sender, RoutedEventArgs e) => BtnViewFormatted_Click(sender, e);

        private void ContextBookmarkToggle_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy; if (p == null) return; try { BookmarkService.Instance.Toggle(p.UniqueID); } catch { } RebindConsideringAsync(SearchBox?.Text ?? string.Empty); }

        private PolicyPlusPolicy? GetContextMenuPolicy(object sender)
        {
            if (sender is FrameworkElement fe)
            { if (fe.Tag is PolicyPlusPolicy p1) return p1; if (fe.DataContext is Models.PolicyListRow row && row.Policy != null) return row.Policy; }
            return null;
        }

        private void ContextCopyName_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy; if (p != null) { var text = EnglishTextService.GetCompositePolicyName(p); var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy }; dp.SetText(text); Clipboard.SetContent(dp); } }

        private void ContextCopyId_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy; if (p != null) { var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy }; dp.SetText(p.UniqueID); Clipboard.SetContent(dp); } }

        private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy; if (p == null) return; var sb = new StringBuilder(); var c = p.Category; var stack = new Stack<string>(); while (c != null) { stack.Push(EnglishTextService.GetCompositeCategoryName(c)); c = c.Parent; } sb.AppendLine("Administrative Templates"); foreach (var name in stack) sb.AppendLine("+ " + name); sb.AppendLine("+ " + EnglishTextService.GetCompositePolicyName(p)); var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy }; dp.SetText(sb.ToString()); Clipboard.SetContent(dp);
        }

        private void ContextRevealInTree_Click(object sender, RoutedEventArgs e)
        { var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy; if (p == null || CategoryTree == null) return; _selectedCategory = p.Category; SelectCategoryInTree(_selectedCategory); UpdateSearchPlaceholder(); ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty); }

        private void ContextCopyRegExport_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy; if (p == null) return; var section = p.RawPolicy.Section switch { AdmxPolicySection.User => AdmxPolicySection.User, AdmxPolicySection.Machine => AdmxPolicySection.Machine, _ => (_appliesFilter == AdmxPolicySection.User ? AdmxPolicySection.User : AdmxPolicySection.Machine) }; var src = section == AdmxPolicySection.User ? _userSource : _compSource; if (src == null) { var loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, section == AdmxPolicySection.User); src = loader.OpenSource(); } var text = RegistryViewFormatter.BuildRegExport(p, src, section) ?? string.Empty; var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy }; dp.SetText(text); Clipboard.SetContent(dp); ShowInfo("Copied .reg export to clipboard.");
        }

        private void BtnPendingChanges_Click(object sender, RoutedEventArgs e)
        { var win = new PendingChangesWindow(); win.Activate(); try { WindowHelpers.BringToFront(win); } catch { } }

        private void ToggleHideEmptyMenu_Click(object sender, RoutedEventArgs e)
        { if (sender is ToggleMenuFlyoutItem t) { _hideEmptyCategories = t.IsChecked; try { SettingsService.Instance.UpdateHideEmptyCategories(_hideEmptyCategories); } catch { } BuildCategoryTree(); ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty); } }

        private void ToggleTempPolMenu_Click(object sender, RoutedEventArgs e)
        { if (sender is ToggleMenuFlyoutItem t) { ChkUseTempPol.IsChecked = t.IsChecked; ChkUseTempPol_Checked(ChkUseTempPol, new RoutedEventArgs()); } }

        private void BookmarkToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is PolicyPlusPolicy p)
            {
                try { BookmarkService.Instance.Toggle(p.UniqueID); } catch { }
                // Only rebind when the active view is restricted to bookmarks; otherwise just update icon via event.
                if (_bookmarksOnly)
                {
                    RebindConsideringAsync(SearchBox?.Text ?? string.Empty);
                }
            }
        }

        private void ChkBookmarksOnly_Checked(object sender, RoutedEventArgs e)
        { if (_suppressBookmarksOnlyChanged) return; _bookmarksOnly = (sender as CheckBox)?.IsChecked == true; try { SettingsService.Instance.UpdateBookmarksOnly(_bookmarksOnly); } catch { } RebindConsideringAsync(SearchBox?.Text ?? string.Empty); }

        // Menu: Show only bookmarks
        private void BookmarkFilterMenu_Click(object sender, RoutedEventArgs e)
        { _bookmarksOnly = !_bookmarksOnly; try { if (ChkBookmarksOnly != null) { _suppressBookmarksOnlyChanged = true; ChkBookmarksOnly.IsChecked = _bookmarksOnly; _suppressBookmarksOnlyChanged = false; } SettingsService.Instance.UpdateBookmarksOnly(_bookmarksOnly); } catch { } RebindConsideringAsync(SearchBox?.Text ?? string.Empty); }

        // Menu: Manage lists (placeholder)
        private void BookmarkManageMenu_Click(object sender, RoutedEventArgs e)
        { ShowInfo("Bookmark list management not implemented yet."); }

        // Always open Quick Edit using only bookmarked policies (ignore selection)
        private void OpenQuickEditBookmarks()
        {
            try
            {
                if (_bundle == null) return;
                var bookmarkIds = BookmarkService.Instance.ActiveIds;
                if (bookmarkIds == null || !bookmarkIds.Any()) return;
                var set = new HashSet<string>(bookmarkIds, System.StringComparer.OrdinalIgnoreCase);
                var policies = _allPolicies.Where(p => set.Contains(p.UniqueID)).ToList();
                if (policies.Count == 0) return;
                EnsureLocalSources();
                var win = new Windows.QuickEditWindow();
                win.Initialize(_bundle, _compSource, _userSource, policies);
                win.Activate();
                try { Utils.WindowHelpers.BringToFront(win); } catch { }
            }
            catch { }
        }

        private void ContextQuickEdit_Click(object sender, RoutedEventArgs e)
        { OpenQuickEditBookmarks(); }

        private void BtnQuickEdit_Click(object sender, RoutedEventArgs e)
        { OpenQuickEditBookmarks(); }

        private void BookmarkToggle_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // Swallow double-tap so row edit doesn't open when user rapidly toggles bookmark.
            e.Handled = true;
        }

        private void ViewBookmarkToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ColBookmark != null && sender is ToggleMenuFlyoutItem t)
                {
                    ColBookmark.Visibility = t.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                    var flag = RootGrid?.FindName("ColBookmarkFlag") as CheckBox; if (flag != null) flag.IsChecked = t.IsChecked;
                }
            }
            catch { }
        }
    }
}
