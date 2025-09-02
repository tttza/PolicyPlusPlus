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
using PolicyPlus.WinUI3.Utils;
using System.Threading.Tasks;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            BtnSave_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
                if (p != null)
                    await OpenEditDialogForPolicyAsync(p, ensureFront: true);
            }
            catch { }
        }

        private void ContextViewFormatted_Click(object sender, RoutedEventArgs e)
        { BtnViewFormatted_Click(sender, e); }

        private PolicyPlusPolicy? GetContextMenuPolicy(object sender)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.Tag is PolicyPlusPolicy p1) return p1;
                if (fe.DataContext is Models.PolicyListRow row && row.Policy != null) return row.Policy;
            }
            return null;
        }

        private void ContextCopyName_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p != null)
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(p.DisplayName);
                Clipboard.SetContent(dp);
            }
        }

        private void ContextCopyId_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p != null)
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(p.UniqueID);
                Clipboard.SetContent(dp);
            }
        }

        private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p == null) return;
            var sb = new StringBuilder();
            var c = p.Category;
            var stack = new Stack<string>();
            while (c != null) { stack.Push(c.DisplayName); c = c.Parent; }
            sb.AppendLine("Administrative Templates");
            foreach (var name in stack) sb.AppendLine("+ " + name);
            sb.AppendLine("+ " + p.DisplayName);
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(sb.ToString());
            Clipboard.SetContent(dp);
        }

        private void ContextRevealInTree_Click(object sender, RoutedEventArgs e)
        {
            var p = GetContextMenuPolicy(sender) ?? (PolicyList?.SelectedItem as Models.PolicyListRow)?.Policy;
            if (p == null || CategoryTree == null) return;
            _selectedCategory = p.Category;
            SelectCategoryInTree(_selectedCategory);
            UpdateSearchPlaceholder();
            ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
        }

        private void ContextCopyRegExport_Click(object sender, RoutedEventArgs e)
        { ShowInfo("Copy .reg export not implemented in this build."); }

        private void BtnPendingChanges_Click(object sender, RoutedEventArgs e)
        {
            var win = new PendingChangesWindow();
            win.Activate();
            try { WindowHelpers.BringToFront(win); } catch { }
        }

        private void ToggleHideEmptyMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                _hideEmptyCategories = t.IsChecked;
                try { _config?.SetValue("HideEmptyCategories", _hideEmptyCategories ? 1 : 0); } catch { }
                BuildCategoryTree();
                ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
            }
        }

        private void ToggleTempPolMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem t)
            {
                ChkUseTempPol.IsChecked = t.IsChecked;
                ChkUseTempPol_Checked(ChkUseTempPol, new RoutedEventArgs());
            }
        }
    }
}
