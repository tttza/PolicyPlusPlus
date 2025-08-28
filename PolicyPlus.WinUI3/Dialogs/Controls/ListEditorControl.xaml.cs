using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace PolicyPlus.WinUI3.Dialogs.Controls
{
    public sealed partial class ListEditorControl : UserControl
    {
        private bool _userProvidesNames;
        public object? Result { get; private set; }
        public string? CountText { get; private set; }
        public event EventHandler<bool>? Finished; // bool: true=OK, false=Cancel

        public ListEditorControl()
        {
            this.InitializeComponent();
        }

        public void Initialize(string label, bool userProvidesNames, object? initial)
        {
            HeaderText.Text = label;
            _userProvidesNames = userProvidesNames;
            ListItems.Items.Clear();
            if (userProvidesNames)
            {
                if (initial is List<KeyValuePair<string, string>> kvp)
                {
                    foreach (var p in kvp)
                        ListItems.Items.Add(new TextBox { Text = $"{p.Key}={p.Value}" });
                }
            }
            else
            {
                if (initial is List<string> list)
                    foreach (var s in list) ListItems.Items.Add(new TextBox { Text = s });
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            ListItems.Items.Add(new TextBox { Text = string.Empty });
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (ListItems.SelectedItem != null)
                ListItems.Items.Remove(ListItems.SelectedItem);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (_userProvidesNames)
            {
                var list = new List<KeyValuePair<string, string>>();
                foreach (var item in ListItems.Items)
                {
                    var tb = item as TextBox;
                    if (tb == null) continue;
                    var text = tb.Text ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) continue;
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
                    var tb = item as TextBox;
                    if (tb == null) continue;
                    var s = tb.Text ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(s))
                        list.Add(s);
                }
                Result = list;
                CountText = $"Edit... ({list.Count})";
            }
            Finished?.Invoke(this, true);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Finished?.Invoke(this, false);
        }
    }
}
