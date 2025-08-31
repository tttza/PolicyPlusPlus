using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using PolicyPlus.WinUI3.Utils;

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

            ApplyThemeResources();
            App.ThemeChanged += (s, e) => ApplyThemeResources();

            AddBtn.Click += (s, e) => AddRow(string.Empty);
            OkBtn.Click += Ok_Click;
            CancelBtn.Click += Cancel_Click;

            // adapt initial size by monitor scale
            WindowHelpers.ResizeForDisplayScale(this, 560, 480);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);

            TryAttachScale();
            this.Activated += (s, e) => TryAttachScale();
        }

        private void TryAttachScale()
        {
            try
            {
                if (Content is FrameworkElement fe)
                {
                    var host = fe.FindName("ScaleHost") as FrameworkElement;
                    var root = fe.FindName("RootShell") as FrameworkElement;
                    if (host != null && root != null)
                    {
                        ScaleHelper.Attach(this, host, root);
                      }
                }
            }
            catch { }
        }

        private void ApplyThemeResources()
        {
            if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme;
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
                    foreach (var p in kvp) { AddRow($"{p.Key}={p.Value}"); any = true; }
                }
                else if (initial is Dictionary<string, string> dict)
                {
                    foreach (var kv in dict) { AddRow($"{kv.Key}={kv.Value}"); any = true; }
                }
            }
            else if (initial is List<string> list)
            {
                foreach (var s in list) { AddRow(s); any = true; }
            }
            if (!any) AddRow(string.Empty);
        }

        private void AddRow(string text)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tb = new TextBox { Text = text };
            Grid.SetColumn(tb, 0);
            grid.Children.Add(tb);

            var removeBtn = new Button { Content = new SymbolIcon(Symbol.Delete), MinWidth = 36, MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Right };
            ToolTipService.SetToolTip(removeBtn, "Remove");
            removeBtn.Tag = grid; removeBtn.Click += RemoveRow_Click;
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(removeBtn);

            ListItems.Items.Add(grid);
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is Grid row)
            {
                ListItems.Items.Remove(row);
                if (ListItems.Items.Count == 0) AddRow(string.Empty);
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
