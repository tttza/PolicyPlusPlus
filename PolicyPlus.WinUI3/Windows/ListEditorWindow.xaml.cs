using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using PolicyPlus.WinUI3.Utils;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input; // added for InputKeyboardSource
using Windows.System; // VirtualKey
using Windows.UI.Core; // CoreVirtualKeyStates

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class ListEditorWindow : Window
    {
        private static readonly Dictionary<string, ListEditorWindow> _openEditors = new();
        public static bool TryActivateExisting(string key)
        {
            if (_openEditors.TryGetValue(key, out var w))
            {
                WindowHelpers.BringToFront(w); w.Activate();
                var timer = w.DispatcherQueue.CreateTimer();
                timer.Interval = TimeSpan.FromMilliseconds(180);
                timer.IsRepeating = false;
                timer.Tick += (s, e) => { try { WindowHelpers.BringToFront(w); w.Activate(); } catch { } }; timer.Start();
                return true;
            }
            return false;
        }
        public static void Register(string key, ListEditorWindow w)
        {
            _openEditors[key] = w;
            w.Closed += (s, e) => { _openEditors.Remove(key); };
        }

        private bool _userProvidesNames;
        public object? Result { get; private set; }
        public string? CountText { get; private set; }
        public event EventHandler<bool>? Finished; // true=OK

        public ListEditorWindow()
        {
            InitializeComponent();
            this.Title = "Edit list";

            // Centralized common window init
            ChildWindowCommon.Initialize(this, 560, 480, ApplyThemeResources);

            AddBtn.Click += (s, e) => { var tb = AddListRow(string.Empty, true); tb?.Focus(FocusState.Programmatic); };
            OkBtn.Click += Ok_Click;
            CancelBtn.Click += Cancel_Click;
        }

        private void Accel_Add(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { var tb = AddListRow(string.Empty, true); tb?.Focus(FocusState.Programmatic); } catch { } args.Handled = true; }
        private void Accel_Ok(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { Ok_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }
        private void Accel_Close(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { Close(); } catch { } args.Handled = true; }

        private void ApplyThemeResources()
        {
            try { if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme; } catch { }
        }

        public void BringToFront() => WindowHelpers.BringToFront(this);

        public void Initialize(string label, bool userProvidesNames, object? initial)
        {
            HeaderText.Text = label;
            _userProvidesNames = userProvidesNames;
            ListItems.Items.Clear();
            bool any = false;
            if (userProvidesNames)
            {
                if (initial is List<KeyValuePair<string, string>> kvp)
                {
                    foreach (var p in kvp) { AddListRow($"{p.Key}={p.Value}"); any = true; }
                }
                else if (initial is Dictionary<string, string> dict)
                {
                    foreach (var kv in dict) { AddListRow($"{kv.Key}={kv.Value}"); any = true; }
                }
            }
            else if (initial is List<string> list)
            {
                foreach (var s in list) { AddListRow(s); any = true; }
            }
            if (!any) AddListRow(string.Empty);
            EnsureTrailingBlankRow();
        }

        private void EnsureTrailingBlankRow()
        {
            try
            {
                if (ListItems.Items.Count == 0) { AddListRow(string.Empty); return; }
                if (ListItems.Items[ListItems.Items.Count - 1] is Grid g && g.Children.Count > 0 && g.Children[0] is TextBox tb)
                {
                    if (!string.IsNullOrWhiteSpace(tb.Text)) AddListRow(string.Empty);
                }
            }
            catch { }
        }

        private TextBox? AddListRow(string text, bool focus = false)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tb = new TextBox { Text = text };
            Grid.SetColumn(tb, 0);
            tb.KeyDown += TextBox_KeyDown;
            tb.TextChanged += TextBox_TextChanged;
            grid.Children.Add(tb);

            var removeBtn = new Button { Content = new SymbolIcon(Symbol.Delete), MinWidth = 36, MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Right, IsTabStop = false };
            ToolTipService.SetToolTip(removeBtn, "Remove");
            removeBtn.Tag = grid; removeBtn.Click += RemoveRow_Click;
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(removeBtn);

            ListItems.Items.Add(grid);
            if (focus) tb.Loaded += (s, e) => tb.Focus(FocusState.Programmatic);
            return tb;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox tb && tb.Parent is Grid row)
                {
                    int index = ListItems.Items.IndexOf(row);
                    if (index == ListItems.Items.Count - 1 && !string.IsNullOrWhiteSpace(tb.Text))
                    {
                        AddListRow(string.Empty);
                    }
                }
            }
            catch { }
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                var shiftDown = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                if (!shiftDown)
                {
                    e.Handled = true;
                    if (sender is TextBox tb && tb.Parent is Grid row)
                    {
                        int index = ListItems.Items.IndexOf(row);
                        bool wasLast = index == ListItems.Items.Count - 1;
                        if (wasLast && !string.IsNullOrWhiteSpace(tb.Text))
                        {
                            AddListRow(string.Empty);
                        }
                        int nextIndex = Math.Min(index + 1, ListItems.Items.Count - 1);
                        if (ListItems.Items[nextIndex] is Grid nextRow)
                        {
                            foreach (var child in nextRow.Children)
                            {
                                if (child is TextBox nextTb)
                                {
                                    nextTb.Focus(FocusState.Programmatic);
                                    nextTb.SelectAll();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is Grid row)
            {
                ListItems.Items.Remove(row);
                if (ListItems.Items.Count == 0) AddListRow(string.Empty); else EnsureTrailingBlankRow();
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (_userProvidesNames)
            {
                var list = new List<KeyValuePair<string, string>>();
                foreach (var item in ListItems.Items)
                {
                    if (item is not Grid row) continue;
                    if (row.Children.Count == 0 || row.Children[0] is not TextBox tb) continue;
                    var text = tb.Text ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var idx = text.IndexOf('=');
                    if (idx >= 0)
                    {
                        var key = text.Substring(0, idx).Trim();
                        var val = text.Substring(idx + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(key)) list.Add(new KeyValuePair<string, string>(key, val));
                    }
                    else
                    { list.Add(new KeyValuePair<string, string>(text.Trim(), string.Empty)); }
                }
                Result = list; CountText = $"Edit... ({list.Count})";
            }
            else
            {
                var list = new List<string>();
                foreach (var item in ListItems.Items)
                {
                    if (item is not Grid row) continue;
                    if (row.Children.Count == 0 || row.Children[0] is not TextBox tb) continue;
                    var s = tb.Text ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
                Result = list; CountText = $"Edit... ({list.Count})";
            }
            Finished?.Invoke(this, true);
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { this.Close(); }
    }
}
