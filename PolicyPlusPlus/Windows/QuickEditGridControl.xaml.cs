using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using PolicyPlusPlus.ViewModels;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using Microsoft.UI.Xaml.Input;

namespace PolicyPlusPlus.Windows
{
    public sealed partial class QuickEditGridControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ObservableCollection<QuickEditRow> Rows { get; } = new();
        internal QuickEditWindow? ParentQuickEditWindow { get; set; }
        public QuickEditColumns Columns { get; } = new();
        public QuickEditGridControl Root => this;
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

        private void NameText_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if ((sender as FrameworkElement)?.DataContext is QuickEditRow row && row.Policy != null)
                {
                    ParentQuickEditWindow?.OpenEditForPolicy(row.Policy.UniqueID);
                }
            }
            catch { }
        }

        private double MeasureChildWidth(FrameworkElement fe)
        {
            if (fe == null) return 0;
            if (double.IsNaN(fe.Width) || fe.Width == 0)
            { fe.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, fe.ActualHeight > 0 ? fe.ActualHeight : double.PositiveInfinity)); }
            double w = fe.ActualWidth; if (w <= 1) w = fe.DesiredSize.Width; return w;
        }

        private void AdjustOptionColumnsToContent()
        {
            double userMax = 0; double compMax = 0;
            try
            {
                var root = this.Content as FrameworkElement; if (root == null) return;
                var stack = new Stack<DependencyObject>(); stack.Push(root);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    int count = VisualTreeHelper.GetChildrenCount(cur);
                    for (int i = 0; i < count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(cur, i);
                        stack.Push(child);
                        if (child is ItemsControl ic && ic.ItemsSource != null && ic.Parent is ScrollViewer sv && sv.Parent is Grid g)
                        {
                            var panel = ic.ItemsPanelRoot as FrameworkElement; if (panel != null)
                            {
                                double width = MeasureChildWidth(panel);
                                int col = Grid.GetColumn(sv);
                                if (col == 6) userMax = Math.Max(userMax, width);
                                else if (col == 10) compMax = Math.Max(compMax, width);
                            }
                        }
                    }
                }
            }
            catch { }
            userMax += 16; compMax += 16;
            if (userMax < 260) userMax = 260; if (userMax > 600) userMax = 600;
            if (compMax < 260) compMax = 260; if (compMax > 600) compMax = 600;
            Columns.UserOptions = new GridLength(userMax);
            Columns.ComputerOptions = new GridLength(compMax);
        }

        private static string BuildKey(QuickEditRow row, OptionElementVM elem, string suffix) => $"{row.Policy.UniqueID}:{elem.Id}:{suffix}";

        // Legacy single-element handlers kept (could be removed later)
        private void UserList_Click(object sender, RoutedEventArgs e) { }
        private void ComputerList_Click(object sender, RoutedEventArgs e) { }
        private void UserMulti_Click(object sender, RoutedEventArgs e) { }
        private void ComputerMulti_Click(object sender, RoutedEventArgs e) { }

        private void UserListDynamic_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is OptionElementVM elem)
            {
                if (FindParentRow(sender, out var row) && row != null && elem.IsList)
                {
                    var r = row!; // assert non-null
                    var key = BuildKey(r, elem, "UserList");
                    if (ListEditorWindow.TryActivateExisting(key)) return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    object initial = elem.ProvidesNames ? elem.UserNamedListItems.ToList() : elem.UserListItems.ToList();
                    win.Initialize(r.Policy.DisplayName + " (User list)", userProvidesNames: elem.ProvidesNames, initial);
                    win.Finished += (s, ok) =>
                    {
                        if (!ok) return;
                        if (elem.ProvidesNames && win.Result is List<KeyValuePair<string, string>> named)
                            elem.ReplaceNamedList(true, named);
                        else if (!elem.ProvidesNames && win.Result is List<string> simple)
                            elem.ReplaceList(true, simple);
                    };
                    ParentQuickEditWindow?.RegisterChild(win); win.Activate();
                }
            }
        }
        private void ComputerListDynamic_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is OptionElementVM elem)
            {
                if (FindParentRow(sender, out var row) && row != null && elem.IsList)
                {
                    var r = row!;
                    var key = BuildKey(r, elem, "ComputerList");
                    if (ListEditorWindow.TryActivateExisting(key)) return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    object initial = elem.ProvidesNames ? elem.ComputerNamedListItems.ToList() : elem.ComputerListItems.ToList();
                    win.Initialize(r.Policy.DisplayName + " (Computer list)", userProvidesNames: elem.ProvidesNames, initial);
                    win.Finished += (s, ok) =>
                    {
                        if (!ok) return;
                        if (elem.ProvidesNames && win.Result is List<KeyValuePair<string, string>> named)
                            elem.ReplaceNamedList(false, named);
                        else if (!elem.ProvidesNames && win.Result is List<string> simple)
                            elem.ReplaceList(false, simple);
                    };
                    ParentQuickEditWindow?.RegisterChild(win); win.Activate();
                }
            }
        }
        private void UserMultiDynamic_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is OptionElementVM elem)
            {
                if (FindParentRow(sender, out var row) && row != null && elem.IsMultiText)
                {
                    var r = row; // remove null-forgiving operator; already checked
                    var key = BuildKey(r, elem, "UserMulti");
                    if (ListEditorWindow.TryActivateExisting(key)) return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    win.Initialize(r.Policy.DisplayName + " (User multi-text)", userProvidesNames: false, elem.UserMultiTextItems.ToList());
                    win.Finished += (s, ok) => { if (ok && win.Result is List<string> list) { elem.ReplaceMultiText(true, list); } };
                    ParentQuickEditWindow?.RegisterChild(win);
                    win.Activate();
                }
            }
        }
        private void ComputerMultiDynamic_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is OptionElementVM elem)
            {
                if (FindParentRow(sender, out var row) && row != null && elem.IsMultiText)
                {
                    var r = row!; // ensure non-null
                    var key = BuildKey(r, elem, "ComputerMulti");
                    if (ListEditorWindow.TryActivateExisting(key)) return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    win.Initialize(r.Policy.DisplayName + " (Computer multi-text)", userProvidesNames: false, elem.ComputerMultiTextItems.ToList());
                    win.Finished += (s, ok) => { if (ok && win.Result is List<string> list) { elem.ReplaceMultiText(false, list); } };
                    ParentQuickEditWindow?.RegisterChild(win);
                    win.Activate();
                }
            }
        }

        private bool FindParentRow(object? sender, out QuickEditRow? row)
        {
            row = null;
            try
            {
                DependencyObject? cur = sender as DependencyObject;
                while (cur != null)
                {
                    if (cur is FrameworkElement fe && fe.DataContext is QuickEditRow r) { row = r; return true; }
                    cur = VisualTreeHelper.GetParent(cur);
                }
            }
            catch { }
            return false;
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb th && th.Tag is string id)
            { double delta = e.HorizontalChange; if (delta == 0) return; Columns.Adjust(id, delta); }
        }
    }

    public sealed class QuickEditColumns : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged; private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private GridLength _name = new GridLength(340); public GridLength Name { get => _name; set { if (_name.Value != value.Value) { _name = value; OnChanged(nameof(Name)); } } }
        private GridLength _id = new GridLength(160); public GridLength Id { get => _id; set { if (_id.Value != value.Value) { _id = value; OnChanged(nameof(Id)); } } }
        private GridLength _userState = new GridLength(170); public GridLength UserState { get => _userState; set { if (_userState.Value != value.Value) { _userState = value; OnChanged(nameof(UserState)); } } }
        private GridLength _userOptions = new GridLength(420); public GridLength UserOptions { get => _userOptions; set { double v = Math.Min(600, value.Value); if (_userOptions.Value != v) { _userOptions = new GridLength(v); OnChanged(nameof(UserOptions)); } } }
        private GridLength _computerState = new GridLength(170); public GridLength ComputerState { get => _computerState; set { if (_computerState.Value != value.Value) { _computerState = value; OnChanged(nameof(ComputerState)); } } }
        private GridLength _computerOptions = new GridLength(420); public GridLength ComputerOptions { get => _computerOptions; set { double v = Math.Min(600, value.Value); if (_computerOptions.Value != v) { _computerOptions = new GridLength(v); OnChanged(nameof(ComputerOptions)); } } }
        public void Adjust(string id, double delta)
        {
            switch (id)
            {
                case "Name": Name = New(Name, delta, 300); break;
                case "Id": Id = New(Id, delta, 140); break;
                case "UserState": UserState = New(UserState, delta, 160); break;
                case "UserOptions": UserOptions = New(UserOptions, delta, 300); break;
                case "ComputerState": ComputerState = New(ComputerState, delta, 160); break;
                case "ComputerOptions": ComputerOptions = New(ComputerOptions, delta, 300); break;
            }
        }
        private static GridLength New(GridLength g, double delta, double min) { double v = g.Value + delta; if (v < min) v = min; return new GridLength(v); }
    }
}
