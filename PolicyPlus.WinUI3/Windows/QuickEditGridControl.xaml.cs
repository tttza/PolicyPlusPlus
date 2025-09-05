using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using PolicyPlus.WinUI3.ViewModels;
using System.Linq;
using System.Collections.Generic;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class QuickEditGridControl : UserControl
    {
        public ObservableCollection<QuickEditRow> Rows { get; } = new();
        internal QuickEditWindow? ParentQuickEditWindow { get; set; }
        public QuickEditGridControl()
        {
            this.InitializeComponent();
        }

        private static string BuildKey(QuickEditRow row, string suffix) => $"{row.Policy.UniqueID}:{suffix}";

        private void UserList_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is QuickEditRow row && row.HasListElement)
            {
                var key = BuildKey(row, "UserList");
                if (ListEditorWindow.TryActivateExisting(key)) return;
                var win = new ListEditorWindow();
                ListEditorWindow.Register(key, win);
                win.Initialize(row.Policy.DisplayName + " (User list)", userProvidesNames: false, row.UserListItems.ToList());
                win.Finished += (s, ok) => { if (ok && win.Result is List<string> list) { row.ReplaceList(true, list); } };
                ParentQuickEditWindow?.RegisterChild(win);
                win.Activate();
            }
        }
        private void ComputerList_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is QuickEditRow row && row.HasListElement)
            {
                var key = BuildKey(row, "ComputerList");
                if (ListEditorWindow.TryActivateExisting(key)) return;
                var win = new ListEditorWindow();
                ListEditorWindow.Register(key, win);
                win.Initialize(row.Policy.DisplayName + " (Computer list)", userProvidesNames: false, row.ComputerListItems.ToList());
                win.Finished += (s, ok) => { if (ok && win.Result is List<string> list) { row.ReplaceList(false, list); } };
                ParentQuickEditWindow?.RegisterChild(win);
                win.Activate();
            }
        }
        private void UserMulti_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is QuickEditRow row && row.HasMultiTextElement)
            {
                var key = BuildKey(row, "UserMulti");
                if (ListEditorWindow.TryActivateExisting(key)) return;
                var win = new ListEditorWindow();
                ListEditorWindow.Register(key, win);
                win.Initialize(row.Policy.DisplayName + " (User multi-text)", userProvidesNames: false, row.UserMultiTextItems.ToList());
                win.Finished += (s, ok) => { if (ok && win.Result is List<string> list) { row.ReplaceMultiText(true, list); } };
                ParentQuickEditWindow?.RegisterChild(win);
                win.Activate();
            }
        }
        private void ComputerMulti_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is QuickEditRow row && row.HasMultiTextElement)
            {
                var key = BuildKey(row, "ComputerMulti");
                if (ListEditorWindow.TryActivateExisting(key)) return;
                var win = new ListEditorWindow();
                ListEditorWindow.Register(key, win);
                win.Initialize(row.Policy.DisplayName + " (Computer multi-text)", userProvidesNames: false, row.ComputerMultiTextItems.ToList());
                win.Finished += (s, ok) => { if (ok && win.Result is List<string> list) { row.ReplaceMultiText(false, list); } };
                ParentQuickEditWindow?.RegisterChild(win);
                win.Activate();
            }
        }
    }
}
