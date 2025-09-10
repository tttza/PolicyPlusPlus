using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using PolicyPlusPlus.Utils;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input; // added for InputKeyboardSource
using Windows.System; // VirtualKey
using Windows.UI.Core; // CoreVirtualKeyStates
using System.Linq; // for duplicate detection

namespace PolicyPlusPlus.Windows
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

            AddBtn.Click += (s, e) => { var tb = AddListRow(string.Empty, string.Empty, true); tb?.Focus(FocusState.Programmatic); };
            OkBtn.Click += Ok_Click;
            CancelBtn.Click += Cancel_Click;
        }

        private void Accel_Add(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { var tb = AddListRow(string.Empty, string.Empty, true); tb?.Focus(FocusState.Programmatic); } catch { } args.Handled = true; }
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
                    foreach (var p in kvp) { AddListRow(p.Key, p.Value); any = true; }
                }
                else if (initial is Dictionary<string, string> dict)
                {
                    foreach (var kv in dict) { AddListRow(kv.Key, kv.Value); any = true; }
                }
                else if (initial is IEnumerable<KeyValuePair<string, string>> en)
                {
                    foreach (var kv in en) { AddListRow(kv.Key, kv.Value); any = true; }
                }
            }
            else if (initial is List<string> list)
            {
                foreach (var s in list) { AddListRow(s, string.Empty); any = true; }
            }
            if (!any) AddListRow(string.Empty, string.Empty);
            EnsureTrailingBlankRow();
        }

        private void EnsureTrailingBlankRow()
        {
            try
            {
                if (ListItems.Items.Count == 0) { AddListRow(string.Empty, string.Empty); return; }
                if (ListItems.Items[ListItems.Items.Count - 1] is Grid g)
                {
                    if (_userProvidesNames)
                    {
                        var keyTb = g.Children.OfType<TextBox>().FirstOrDefault(tb => (string?)tb.Tag == "k");
                        var valTb = g.Children.OfType<TextBox>().FirstOrDefault(tb => (string?)tb.Tag == "v");
                        if (keyTb != null && valTb != null && (!string.IsNullOrWhiteSpace(keyTb.Text) || !string.IsNullOrWhiteSpace(valTb.Text)))
                            AddListRow(string.Empty, string.Empty);
                    }
                    else
                    {
                        var tb = g.Children.OfType<TextBox>().FirstOrDefault();
                        if (tb != null && !string.IsNullOrWhiteSpace(tb.Text)) AddListRow(string.Empty, string.Empty);
                    }
                }
            }
            catch { }
        }

        // Returns the first textbox (key when named, value-only when simple)
        private TextBox? AddListRow(string keyOrValue, string valueIfNamed = "", bool focus = false)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            if (_userProvidesNames)
            {
                // key, value, delete button
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var keyTb = new TextBox { Text = keyOrValue, PlaceholderText = "Key" , Tag = "k" };
                Grid.SetColumn(keyTb, 0);
                keyTb.KeyDown += TextBox_KeyDown;
                keyTb.TextChanged += TextBox_TextChanged;
                grid.Children.Add(keyTb);

                var valTb = new TextBox { Text = valueIfNamed, PlaceholderText = "Value", Tag = "v" };
                Grid.SetColumn(valTb, 1);
                valTb.KeyDown += TextBox_KeyDown;
                valTb.TextChanged += TextBox_TextChanged;
                grid.Children.Add(valTb);

                var removeBtn = new Button { Content = new SymbolIcon(Symbol.Delete), MinWidth = 36, MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Right, IsTabStop = false };
                ToolTipService.SetToolTip(removeBtn, "Remove");
                removeBtn.Tag = grid; removeBtn.Click += RemoveRow_Click;
                Grid.SetColumn(removeBtn, 2);
                grid.Children.Add(removeBtn);

                ListItems.Items.Add(grid);
                if (focus) keyTb.Loaded += (s, e) => keyTb.Focus(FocusState.Programmatic);
                return keyTb;
            }
            else
            {
                // value only + delete button
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tb = new TextBox { Text = keyOrValue };
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
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox tb && tb.Parent is Grid row)
                {
                    int index = ListItems.Items.IndexOf(row);
                    if (index == ListItems.Items.Count - 1) // last row
                    {
                        if (_userProvidesNames)
                        {
                            var keyTb = row.Children.OfType<TextBox>().FirstOrDefault(x => (string?)x.Tag == "k");
                            var valTb = row.Children.OfType<TextBox>().FirstOrDefault(x => (string?)x.Tag == "v");
                            if ((keyTb != null && !string.IsNullOrWhiteSpace(keyTb.Text)) || (valTb != null && !string.IsNullOrWhiteSpace(valTb.Text)))
                                AddListRow(string.Empty, string.Empty);
                        }
                        else if (!string.IsNullOrWhiteSpace(tb.Text))
                        {
                            AddListRow(string.Empty, string.Empty);
                        }
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
                if (!shiftDown && sender is TextBox tb && tb.Parent is Grid row)
                {
                    if (_userProvidesNames && (string?)tb.Tag == "v")
                    {
                        // Always move to next key textbox (create row if needed)
                        int index = ListItems.Items.IndexOf(row);
                        bool lastRow = index == ListItems.Items.Count - 1;
                        if (lastRow)
                        {
                            var keyTbCur = row.Children.OfType<TextBox>().FirstOrDefault(x => (string?)x.Tag == "k");
                            if ((keyTbCur != null && !string.IsNullOrWhiteSpace(keyTbCur.Text)) || !string.IsNullOrWhiteSpace(tb.Text))
                            {
                                AddListRow(string.Empty, string.Empty, false);
                            }
                        }
                        int nextIndex = Math.Min(index + 1, ListItems.Items.Count - 1);
                        if (nextIndex > index && ListItems.Items[nextIndex] is Grid nextRow)
                        {
                            var nextKey = nextRow.Children.OfType<TextBox>().FirstOrDefault(x => (string?)x.Tag == "k");
                            if (nextKey != null)
                            {
                                e.Handled = true;
                                nextKey.Focus(FocusState.Programmatic);
                                nextKey.SelectAll();
                                return;
                            }
                        }
                    }
                    else if (!_userProvidesNames)
                    {
                        // Simple list: always move to next row (create if at end and current has content)
                        int index = ListItems.Items.IndexOf(row);
                        bool lastRow = index == ListItems.Items.Count - 1;
                        if (lastRow)
                        {
                            if (!string.IsNullOrWhiteSpace(tb.Text))
                            {
                                AddListRow(string.Empty, string.Empty, false);
                            }
                        }
                        int nextIndex = Math.Min(index + 1, ListItems.Items.Count - 1);
                        if (nextIndex > index && ListItems.Items[nextIndex] is Grid nextRow)
                        {
                            var nextTb = nextRow.Children.OfType<TextBox>().FirstOrDefault();
                            if (nextTb != null)
                            {
                                e.Handled = true;
                                nextTb.Focus(FocusState.Programmatic);
                                nextTb.SelectAll();
                                return;
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
                if (ListItems.Items.Count == 0) AddListRow(string.Empty, string.Empty); else EnsureTrailingBlankRow();
            }
        }

        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (_userProvidesNames)
            {
                var list = new List<KeyValuePair<string, string>>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in ListItems.Items)
                {
                    if (item is not Grid row) continue;
                    var keyTb = row.Children.OfType<TextBox>().FirstOrDefault(x => (string?)x.Tag == "k");
                    var valTb = row.Children.OfType<TextBox>().FirstOrDefault(x => (string?)x.Tag == "v");
                    if (keyTb == null || valTb == null) continue;
                    var key = keyTb.Text?.Trim() ?? string.Empty;
                    var val = valTb.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(val)) continue; // ignore blank rows
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        await ShowValidationDialog("A key is required for all non-empty rows.");
                        keyTb.Focus(FocusState.Programmatic);
                        return;
                    }
                    if (!seen.Add(key))
                    {
                        await ShowValidationDialog($"Multiple entries are named \"{key}\". Remove or rename duplicates.");
                        keyTb.Focus(FocusState.Programmatic); keyTb.SelectAll();
                        return;
                    }
                    list.Add(new KeyValuePair<string, string>(key, val));
                }
                Result = list; CountText = $"Edit... ({list.Count})";
            }
            else
            {
                var list = new List<string>();
                foreach (var item in ListItems.Items)
                {
                    if (item is not Grid row) continue;
                    var tb = row.Children.OfType<TextBox>().FirstOrDefault();
                    if (tb == null) continue;
                    var s = tb.Text ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
                Result = list; CountText = $"Edit... ({list.Count})";
            }
            Finished?.Invoke(this, true);
            this.Close();
        }

        private async System.Threading.Tasks.Task ShowValidationDialog(string message)
        {
            try
            {
                var dlg = new ContentDialog
                {
                    Title = "Validation",
                    Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = "OK",
                    XamlRoot = (Content as FrameworkElement)?.XamlRoot
                };
                await dlg.ShowAsync();
            }
            catch { }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { this.Close(); }
    }
}
