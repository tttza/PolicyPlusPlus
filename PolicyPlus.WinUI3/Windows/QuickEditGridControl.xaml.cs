using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using PolicyPlus.WinUI3.ViewModels;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;

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

        private bool _measured;

        public QuickEditGridControl()
        {
            this.InitializeComponent();
            this.Loaded += QuickEditGridControl_Loaded;
        }

        private void QuickEditGridControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_measured) return;
            _measured = true;
            try { AdjustOptionColumnsToContent(); } catch { }
        }

        private double MeasureChildWidth(FrameworkElement fe)
        {
            if (fe == null) return 0;
            if (double.IsNaN(fe.Width) || fe.Width == 0)
            {
                fe.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, fe.ActualHeight > 0 ? fe.ActualHeight : double.PositiveInfinity));
            }
            double w = fe.ActualWidth;
            if (w <= 1) w = fe.DesiredSize.Width;
            return w;
        }

        private void AdjustOptionColumnsToContent()
        {
            double userMax = 0; double compMax = 0;
            try
            {
                var rootScroll = this.Content as FrameworkElement;
                if (rootScroll == null) return;
                var stack = new Stack<DependencyObject>();
                stack.Push(rootScroll);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    int count = VisualTreeHelper.GetChildrenCount(cur);
                    for (int i = 0; i < count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(cur, i);
                        stack.Push(child);
                        if (child is ScrollViewer sv && sv.Content is StackPanel sp && sp.Orientation == Orientation.Horizontal)
                        {
                            double width = MeasureChildWidth(sp);
                            // Heuristic: user options column before computer options in grid; determine based on parent grid column
                            if (sp.Parent is ScrollViewer sv2 && sv2.Parent is Grid g)
                            {
                                int col = Grid.GetColumn(sv2);
                                if (col == 6) // user options column index in template grid
                                    userMax = Math.Max(userMax, width);
                                else if (col == 10) // computer options column index
                                    compMax = Math.Max(compMax, width);
                            }
                        }
                    }
                }
            }
            catch { }
            // Add minimal padding
            userMax += 16; compMax += 16;
            // Clamp to visual MaxWidth (300) and enforce a modest minimum (260)
            if (userMax < 260) userMax = 260; if (userMax > 300) userMax = 300;
            if (compMax < 260) compMax = 260; if (compMax > 300) compMax = 300;
            Columns.UserOptions = new GridLength(userMax);
            Columns.ComputerOptions = new GridLength(compMax);
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

        // Updated initial widths (will be overridden for options after load)
        private GridLength _name = new GridLength(340); public GridLength Name { get => _name; set { if (_name.Value != value.Value) { _name = value; OnChanged(nameof(Name)); } } }
        private GridLength _id = new GridLength(160); public GridLength Id { get => _id; set { if (_id.Value != value.Value) { _id = value; OnChanged(nameof(Id)); } } }
        private GridLength _userState = new GridLength(170); public GridLength UserState { get => _userState; set { if (_userState.Value != value.Value) { _userState = value; OnChanged(nameof(UserState)); } } }
        private GridLength _userOptions = new GridLength(300); public GridLength UserOptions { get => _userOptions; set { double v = Math.Min(300, value.Value); if (_userOptions.Value != v) { _userOptions = new GridLength(v); OnChanged(nameof(UserOptions)); } } }
        private GridLength _computerState = new GridLength(170); public GridLength ComputerState { get => _computerState; set { if (_computerState.Value != value.Value) { _computerState = value; OnChanged(nameof(ComputerState)); } } }
        private GridLength _computerOptions = new GridLength(300); public GridLength ComputerOptions { get => _computerOptions; set { double v = Math.Min(300, value.Value); if (_computerOptions.Value != v) { _computerOptions = new GridLength(v); OnChanged(nameof(ComputerOptions)); } } }

        public void Adjust(string id, double delta)
        {
            switch (id)
            {
                case "Name": Name = New(Name, delta, 300); break;
                case "Id": Id = New(Id, delta, 140); break;
                case "UserState": UserState = New(UserState, delta, 160); break;
                case "UserOptions": UserOptions = New(UserOptions, delta, 420); break;
                case "ComputerState": ComputerState = New(ComputerState, delta, 160); break;
                case "ComputerOptions": ComputerOptions = New(ComputerOptions, delta, 420); break;
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
