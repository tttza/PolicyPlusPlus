using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
using Microsoft.UI.Xaml.Media;
using PolicyPlus.WinUI3.Utils;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed class ListEditorWindow : Window
    {
        private bool _userProvidesNames;
        public object? Result { get; private set; }
        public string? CountText { get; private set; }
        public event EventHandler<bool>? Finished; // true=OK

        private TextBlock HeaderText = new TextBlock { FontWeight = FontWeights.SemiBold };
        private ListView ListItems = new ListView { SelectionMode = ListViewSelectionMode.None };

        public ListEditorWindow()
        {
            this.Title = "Edit list";

            var root = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
            root.Children.Add(HeaderText);
            root.Children.Add(ListItems);

            var buttonsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var addBtn = new Button { Content = "Add" };
            addBtn.Click += (s, e) => AddRow(string.Empty);
            var okBtn = new Button { Content = "OK" };
            okBtn.Click += Ok_Click;
            var cancelBtn = new Button { Content = "Cancel" };
            cancelBtn.Click += Cancel_Click;

            buttonsRow.Children.Add(addBtn);
            var tail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            tail.Children.Add(okBtn);
            tail.Children.Add(cancelBtn);
            buttonsRow.Children.Add(tail);

            root.Children.Add(buttonsRow);
            this.Content = root;

            WindowHelpers.Resize(this, 560, 480);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
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
                    foreach (var p in kvp)
                    {
                        AddRow($"{p.Key}={p.Value}");
                        any = true;
                    }
                }
                else if (initial is Dictionary<string, string> dict)
                {
                    foreach (var kv in dict)
                    {
                        AddRow($"{kv.Key}={kv.Value}");
                        any = true;
                    }
                }
            }
            else
            {
                if (initial is List<string> list)
                {
                    foreach (var s in list)
                    {
                        AddRow(s);
                        any = true;
                    }
                }
            }
            if (!any)
            {
                // Keep one box by default
                AddRow(string.Empty);
            }
        }

        private void AddRow(string text)
        {
            // Row: [ TextBox (stretch) ][ x icon ]
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tb = new TextBox { Text = text };
            Grid.SetColumn(tb, 0);
            grid.Children.Add(tb);

            var removeBtn = new Button
            {
                Content = new SymbolIcon(Symbol.Delete),
                MinWidth = 36,
                MinHeight = 36,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            ToolTipService.SetToolTip(removeBtn, "Remove");
            removeBtn.Tag = grid;
            removeBtn.Click += RemoveRow_Click;
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(removeBtn);

            ListItems.Items.Add(grid);
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is Grid row)
            {
                ListItems.Items.Remove(row);
                if (ListItems.Items.Count == 0)
                {
                    // Always keep one box available
                    AddRow(string.Empty);
                }
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
                    if (string.IsNullOrWhiteSpace(text)) continue; // empty elements are removed
                    var idx = text.IndexOf('=');
                    if (idx >= 0)
                    {
                        var key = text.Substring(0, idx).Trim();
                        var val = text.Substring(idx + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(key))
                            list.Add(new KeyValuePair<string, string>(key, val));
                    }
                    else
                    {
                        list.Add(new KeyValuePair<string, string>(text.Trim(), string.Empty));
                    }
                }
                Result = list;
                CountText = $"Edit... ({list.Count})";
            }
            else
            {
                var list = new List<string>();
                foreach (var item in ListItems.Items)
                {
                    if (item is not Grid row) continue;
                    if (row.Children.Count == 0 || row.Children[0] is not TextBox tb) continue;
                    var s = tb.Text ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(s)) // empty elements are removed
                        list.Add(s);
                }
                Result = list;
                CountText = $"Edit... ({list.Count})";
            }
            Finished?.Invoke(this, true);
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
