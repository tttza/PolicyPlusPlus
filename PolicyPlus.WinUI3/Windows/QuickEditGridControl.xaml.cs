using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using PolicyPlus.WinUI3.ViewModels;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.ComponentModel;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class QuickEditGridControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ObservableCollection<QuickEditRow> Rows { get; } = new();
        internal QuickEditWindow? ParentQuickEditWindow { get; set; }

        // Column width model (shared header/body)
        public QuickEditColumns Columns { get; } = new();
        public QuickEditGridControl Root => this; // for x:Bind in item template

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

        // Resize handling
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb th && th.Tag is string id)
            {
                double delta = e.HorizontalChange;
                if (delta == 0) return;
                Columns.Adjust(id, delta);
            }
        }
    }

    public sealed class QuickEditColumns : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Initial widths roughly matching former star layout
        private GridLength _name = new GridLength(300); public GridLength Name { get => _name; set { if (_name.Value != value.Value) { _name = value; OnChanged(nameof(Name)); } } }
        private GridLength _id = new GridLength(220); public GridLength Id { get => _id; set { if (_id.Value != value.Value) { _id = value; OnChanged(nameof(Id)); } } }
        private GridLength _userState = new GridLength(200); public GridLength UserState { get => _userState; set { if (_userState.Value != value.Value) { _userState = value; OnChanged(nameof(UserState)); } } }
        private GridLength _userOptions = new GridLength(600); public GridLength UserOptions { get => _userOptions; set { if (_userOptions.Value != value.Value) { _userOptions = value; OnChanged(nameof(UserOptions)); } } }
        private GridLength _computerState = new GridLength(200); public GridLength ComputerState { get => _computerState; set { if (_computerState.Value != value.Value) { _computerState = value; OnChanged(nameof(ComputerState)); } } }
        private GridLength _computerOptions = new GridLength(600); public GridLength ComputerOptions { get => _computerOptions; set { if (_computerOptions.Value != value.Value) { _computerOptions = value; OnChanged(nameof(ComputerOptions)); } } }

        private const double Min = 60;

        public void Adjust(string id, double delta)
        {
            switch (id)
            {
                case "Name": Name = New(Name, delta, 240); break;
                case "Id": Id = New(Id, delta, 220); break;
                case "UserState": UserState = New(UserState, delta, 160); break;
                case "UserOptions": UserOptions = New(UserOptions, delta, 300); break;
                case "ComputerState": ComputerState = New(ComputerState, delta, 160); break;
                case "ComputerOptions": ComputerOptions = New(ComputerOptions, delta, 300); break;
            }
        }
        private static GridLength New(GridLength g, double delta, double min)
        {
            double v = g.Value + delta;
            if (v < min) v = min;
            return new GridLength(v);
        }
    }
}
