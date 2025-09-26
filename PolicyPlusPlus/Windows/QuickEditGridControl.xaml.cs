using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlusPlus.Logging;
using PolicyPlusPlus.ViewModels;

namespace PolicyPlusPlus.Windows
{
    public sealed partial class QuickEditGridControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ObservableCollection<QuickEditRow> Rows { get; } = new();
        internal QuickEditWindow? ParentQuickEditWindow { get; set; }
        public QuickEditColumns Columns { get; } = new();
        public QuickEditGridControl Root => this;
        private bool _measured;

        // Align with XAML resources; allows QuickEditWindow to query actual separator width.
        public double SeparatorWidth { get; set; } = 4;
        public double RightSpacerWidth { get; set; } = 8;

        // Whether to auto-size option columns to content on first load.
        public bool AutoAdjustOptionColumns { get; set; } = false;

        public QuickEditGridControl()
        {
            this.InitializeComponent();
            this.Loaded += QuickEditGridControl_Loaded;
            // Ensure we receive Tab even if inner controls mark it handled
            AddHandler(UIElement.KeyDownEvent, (KeyEventHandler)OnAnyKeyDown, true);
        }

        private void OnAnyKeyDown(object sender, KeyRoutedEventArgs e) =>
            RootGrid_KeyDown(sender, e);

        private void QuickEditGridControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_measured)
                return;
            _measured = true;
            try
            {
                if (AutoAdjustOptionColumns)
                    AdjustOptionColumnsToContent();
            }
            catch (Exception ex)
            {
                Log.Debug("QuickEditGrid", $"Loaded adjustment failed: {ex.Message}");
            }
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != global::Windows.System.VirtualKey.Tab)
                return;
            var focus = FocusManager.GetFocusedElement() as FrameworkElement;
            if (focus == null)
                return;
            var sequence = BuildGlobalFocusSequence();
            if (sequence.Count == 0)
                return;
            int currentIndex = sequence.IndexOf(focus);
            if (currentIndex < 0)
                return;
            bool shift =
                (
                    global::Windows
                        .UI.Core.CoreWindow.GetForCurrentThread()
                        .GetKeyState(global::Windows.System.VirtualKey.Shift)
                    & global::Windows.UI.Core.CoreVirtualKeyStates.Down
                ) != 0;
            e.Handled = true;
            int nextIndex = shift ? currentIndex - 1 : currentIndex + 1;
            if (nextIndex < 0)
            {
                e.Handled = false;
                return;
            }
            if (nextIndex >= sequence.Count)
            {
                e.Handled = false;
                return;
            }
            var target = sequence[nextIndex];
            target.Focus(FocusState.Programmatic);
            try
            {
                target.StartBringIntoView();
            }
            catch (Exception ex)
            {
                Log.Debug("QuickEditGrid", $"StartBringIntoView failed: {ex.Message}");
            }
        }

        private List<FrameworkElement> BuildGlobalFocusSequence()
        {
            var list = new List<FrameworkElement>();
            var seen = new HashSet<FrameworkElement>();
            try
            {
                foreach (var fe in FindDescendants(this).OfType<FrameworkElement>())
                {
                    var tag = fe.Tag as string;
                    if (tag == null)
                        continue;
                    QuickEditRow? row = fe.DataContext as QuickEditRow;
                    if (row == null && fe.DataContext is OptionElementVM)
                    {
                        // climb to find row container
                        DependencyObject? cur = fe.Parent;
                        int guard = 0;
                        while (cur != null && guard < 50)
                        {
                            if (cur is FrameworkElement rfe && rfe.DataContext is QuickEditRow qr)
                            {
                                row = qr;
                                break;
                            }
                            cur = VisualTreeHelper.GetParent(cur);
                            guard++;
                        }
                    }
                    if (row == null)
                        continue;
                    bool add = false;
                    switch (tag)
                    {
                        case "UserStateCombo":
                            add = true;
                            break;
                        case "UserOption":
                            if (
                                row.UserState == QuickEditState.Enabled
                                || row.UserEnabledForOptions
                            )
                                add = true;
                            break;
                        case "ComputerStateCombo":
                            add = true;
                            break;
                        case "ComputerOption":
                            if (
                                row.ComputerState == QuickEditState.Enabled
                                || row.ComputerEnabledForOptions
                            )
                                add = true;
                            break;
                    }
                    if (
                        add
                        && fe.Visibility == Visibility.Visible
                        && (fe as Control)?.IsEnabled != false
                    )
                    {
                        if (seen.Add(fe))
                            list.Add(fe);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("QuickEditGrid", $"BuildGlobalFocusSequence failed: {ex.Message}");
            }
            return list;
        }

        private static IEnumerable<DependencyObject> FindDescendants(DependencyObject root)
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int count = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    queue.Enqueue(child);
                    yield return child;
                }
            }
        }

        private void NameText_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if (
                    (sender as FrameworkElement)?.DataContext is QuickEditRow row
                    && row.Policy != null
                )
                {
                    ParentQuickEditWindow?.OpenEditForPolicy(row.Policy.UniqueID);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("QuickEditGrid", $"NameText_DoubleTapped failed: {ex.Message}");
            }
        }

        private double MeasureChildWidth(FrameworkElement fe)
        {
            if (fe == null)
                return 0;
            if (double.IsNaN(fe.Width) || fe.Width == 0)
            {
                fe.Measure(
                    new global::Windows.Foundation.Size(
                        double.PositiveInfinity,
                        fe.ActualHeight > 0 ? fe.ActualHeight : double.PositiveInfinity
                    )
                );
            }
            double w = fe.ActualWidth;
            if (w <= 1)
                w = fe.DesiredSize.Width;
            return w;
        }

        private void AdjustOptionColumnsToContent()
        {
            double userMax = 0;
            double compMax = 0;
            try
            {
                var root = this.Content as FrameworkElement;
                if (root == null)
                    return;
                var stack = new Stack<DependencyObject>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    int count = VisualTreeHelper.GetChildrenCount(cur);
                    for (int i = 0; i < count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(cur, i);
                        stack.Push(child);
                        if (
                            child is ItemsControl ic
                            && ic.ItemsSource != null
                            && ic.Parent is ScrollViewer sv
                            && sv.Parent is Grid
                        )
                        {
                            var panel = ic.ItemsPanelRoot as FrameworkElement;
                            if (panel != null)
                            {
                                double width = MeasureChildWidth(panel);
                                int col = Grid.GetColumn(sv);
                                if (col == 6)
                                    userMax = Math.Max(userMax, width);
                                else if (col == 10)
                                    compMax = Math.Max(compMax, width);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(
                    "QuickEditGrid",
                    $"AdjustOptionColumnsToContent scan failed: {ex.Message}"
                );
            }
            userMax += 16;
            compMax += 16;
            if (userMax < 260)
                userMax = 260;
            if (userMax > 600)
                userMax = 600;
            if (compMax < 260)
                compMax = 260;
            if (compMax > 600)
                compMax = 600;
            Columns.UserOptions = new GridLength(userMax);
            Columns.ComputerOptions = new GridLength(compMax);
        }

        private static string BuildKey(QuickEditRow row, OptionElementVM elem, string suffix) =>
            $"{row.Policy.UniqueID}:{elem.Id}:{suffix}";

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
                    var r = row!;
                    var key = BuildKey(r, elem, "UserList");
                    if (ListEditorWindow.TryActivateExisting(key))
                        return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    object initial = elem.ProvidesNames
                        ? elem.UserNamedListItems.ToList()
                        : elem.UserListItems.ToList();
                    win.Initialize(
                        r.Policy.DisplayName + " (User list)",
                        userProvidesNames: elem.ProvidesNames,
                        initial
                    );
                    win.Finished += (s, ok) =>
                    {
                        if (!ok)
                            return;
                        if (
                            elem.ProvidesNames
                            && win.Result is List<KeyValuePair<string, string>> named
                        )
                            elem.ReplaceNamedList(true, named);
                        else if (!elem.ProvidesNames && win.Result is List<string> simple)
                            elem.ReplaceList(true, simple);
                    };
                    ParentQuickEditWindow?.RegisterChild(win);
                    win.Activate();
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
                    if (ListEditorWindow.TryActivateExisting(key))
                        return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    object initial = elem.ProvidesNames
                        ? elem.ComputerNamedListItems.ToList()
                        : elem.ComputerListItems.ToList();
                    win.Initialize(
                        r.Policy.DisplayName + " (Computer list)",
                        userProvidesNames: elem.ProvidesNames,
                        initial
                    );
                    win.Finished += (s, ok) =>
                    {
                        if (!ok)
                            return;
                        if (
                            elem.ProvidesNames
                            && win.Result is List<KeyValuePair<string, string>> named
                        )
                            elem.ReplaceNamedList(false, named);
                        else if (!elem.ProvidesNames && win.Result is List<string> simple)
                            elem.ReplaceList(false, simple);
                    };
                    ParentQuickEditWindow?.RegisterChild(win);
                    win.Activate();
                }
            }
        }

        private void UserMultiDynamic_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is OptionElementVM elem)
            {
                if (FindParentRow(sender, out var row) && row != null && elem.IsMultiText)
                {
                    var r = row;
                    var key = BuildKey(r, elem, "UserMulti");
                    if (ListEditorWindow.TryActivateExisting(key))
                        return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    win.Initialize(
                        r.Policy.DisplayName + " (User multi-text)",
                        userProvidesNames: false,
                        elem.UserMultiTextItems.ToList()
                    );
                    win.Finished += (s, ok) =>
                    {
                        if (ok && win.Result is List<string> list)
                        {
                            elem.ReplaceMultiText(true, list);
                        }
                    };
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
                    var r = row!;
                    var key = BuildKey(r, elem, "ComputerMulti");
                    if (ListEditorWindow.TryActivateExisting(key))
                        return;
                    var win = new ListEditorWindow();
                    ListEditorWindow.Register(key, win);
                    win.Initialize(
                        r.Policy.DisplayName + " (Computer multi-text)",
                        userProvidesNames: false,
                        elem.ComputerMultiTextItems.ToList()
                    );
                    win.Finished += (s, ok) =>
                    {
                        if (ok && win.Result is List<string> list)
                        {
                            elem.ReplaceMultiText(false, list);
                        }
                    };
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
                    if (cur is FrameworkElement fe && fe.DataContext is QuickEditRow r)
                    {
                        row = r;
                        return true;
                    }
                    cur = VisualTreeHelper.GetParent(cur);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("QuickEditGrid", $"FindParentRow failed: {ex.Message}");
            }
            return false;
        }

        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb th && th.Tag is string id)
            {
                double delta = e.HorizontalChange;
                if (delta == 0)
                    return;
                Columns.Adjust(id, delta);
            }
        }
    }

    public sealed class QuickEditColumns : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private GridLength _name = new GridLength(340);
        public GridLength Name
        {
            get => _name;
            set
            {
                if (_name.Value != value.Value)
                {
                    _name = value;
                    OnChanged(nameof(Name));
                }
            }
        }
        private GridLength _id = new GridLength(160);
        public GridLength Id
        {
            get => _id;
            set
            {
                if (_id.Value != value.Value)
                {
                    _id = value;
                    OnChanged(nameof(Id));
                }
            }
        }
        private GridLength _userState = new GridLength(170);
        public GridLength UserState
        {
            get => _userState;
            set
            {
                if (_userState.Value != value.Value)
                {
                    _userState = value;
                    OnChanged(nameof(UserState));
                }
            }
        }
        private GridLength _userOptions = new GridLength(220);
        public GridLength UserOptions
        {
            get => _userOptions;
            set
            {
                double v = Math.Min(600, value.Value);
                if (_userOptions.Value != v)
                {
                    _userOptions = new GridLength(v);
                    OnChanged(nameof(UserOptions));
                }
            }
        }
        private GridLength _computerState = new GridLength(170);
        public GridLength ComputerState
        {
            get => _computerState;
            set
            {
                if (_computerState.Value != value.Value)
                {
                    _computerState = value;
                    OnChanged(nameof(ComputerState));
                }
            }
        }
        private GridLength _computerOptions = new GridLength(220);
        public GridLength ComputerOptions
        {
            get => _computerOptions;
            set
            {
                double v = Math.Min(600, value.Value);
                if (_computerOptions.Value != v)
                {
                    _computerOptions = new GridLength(v);
                    OnChanged(nameof(ComputerOptions));
                }
            }
        }

        public void Adjust(string id, double delta)
        {
            switch (id)
            {
                case "Name":
                    Name = New(Name, delta, 300);
                    break;
                case "Id":
                    Id = New(Id, delta, 140);
                    break;
                case "UserState":
                    UserState = New(UserState, delta, 150);
                    break;
                case "UserOptions":
                    UserOptions = New(UserOptions, delta, 220);
                    break;
                case "ComputerState":
                    ComputerState = New(ComputerState, delta, 150);
                    break;
                case "ComputerOptions":
                    ComputerOptions = New(ComputerOptions, delta, 220);
                    break;
            }
        }

        private static GridLength New(GridLength g, double delta, double min)
        {
            double v = g.Value + delta;
            if (v < min)
                v = min;
            return new GridLength(v);
        }
    }
}
