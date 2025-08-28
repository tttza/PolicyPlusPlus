using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
using PolicyPlus.WinUI3.Utils;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed class DetailPolicyFormattedWindow : Window
    {
        private PolicyPlusPolicy _policy = null!;
        private AdmxBundle _bundle = null!;
        private IPolicySource _compSource = null!;
        private IPolicySource _userSource = null!;
        private AdmxPolicySection _currentSection;

        private TextBox NameBox = new TextBox { IsReadOnly = true };
        private TextBox IdBox = new TextBox { IsReadOnly = true };
        private TextBox DefinedInBox = new TextBox { IsReadOnly = true };
        private TextBox PathBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas") };
        private TextBox RegBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas") };
        private Button CopyPathBtn = new Button { Content = new SymbolIcon(Symbol.Copy), MinWidth = 36, MinHeight = 36, Padding = new Thickness(8) };
        private Button CopyRegBtn = new Button { Content = new SymbolIcon(Symbol.Copy), MinWidth = 36, MinHeight = 36, Padding = new Thickness(8) };
        private Button ToggleViewBtn = new Button { Content = new SymbolIcon(Symbol.Sync), MinWidth = 36, MinHeight = 36, Padding = new Thickness(8) };
        private Button CloseBtn = new Button { Content = "Close", MinWidth = 90 };

        private string _regFormattedCache = string.Empty;
        private string _regFileCache = string.Empty;
        private bool _showRegFile = false;

        public DetailPolicyFormattedWindow()
        {
            this.Title = "Policy Details - Formatted";

            var root = new Grid { RowSpacing = 6, ColumnSpacing = 8, Padding = new Thickness(10) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Clear();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            void addRow(int row, string label, Control box, FrameworkElement? sideEl = null)
            {
                var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0); root.Children.Add(lbl);
                Grid.SetRow(box, row); Grid.SetColumn(box, 1); root.Children.Add(box);
                if (sideEl != null)
                { Grid.SetRow(sideEl, row); Grid.SetColumn(sideEl, 2); root.Children.Add(sideEl); }
            }

            addRow(0, "Name", NameBox);
            addRow(1, "Unique ID", IdBox);
            addRow(2, "Defined in", DefinedInBox);

            var pathScroll = new ScrollViewer { Content = PathBox, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            ToolTipService.SetToolTip(CopyPathBtn, "Copy");
            var pathIcons = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, VerticalAlignment = VerticalAlignment.Top };
            pathIcons.Children.Add(CopyPathBtn);
            addRow(3, "Policy Path", pathScroll, pathIcons);

            var regIcons = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, VerticalAlignment = VerticalAlignment.Top };
            ToolTipService.SetToolTip(ToggleViewBtn, "Toggle Formatted/.reg");
            ToolTipService.SetToolTip(CopyRegBtn, "Copy");
            regIcons.Children.Add(ToggleViewBtn);
            regIcons.Children.Add(CopyRegBtn);
            var regScroll = new ScrollViewer { Content = RegBox, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            addRow(4, "Registry Value", regScroll, regIcons);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(CloseBtn);
            Grid.SetRow(buttons, 5); Grid.SetColumnSpan(buttons, 3); root.Children.Add(buttons);

            CopyPathBtn.Click += (s, e) => CopyToClipboard(PathBox.Text);
            CopyRegBtn.Click += (s, e) => CopyToClipboard(RegBox.Text);
            ToggleViewBtn.Click += ToggleViewBtn_Click;
            CloseBtn.Click += (s, e) => this.Close();

            this.Content = root;
            TryResize(600, 520);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);
        }

        private void ToggleViewBtn_Click(object sender, RoutedEventArgs e)
        {
            _showRegFile = !_showRegFile;
            RegBox.Text = _showRegFile ? _regFileCache : _regFormattedCache;
        }

        private static void CopyToClipboard(string text)
        {
            var data = new global::Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
            data.SetText(text ?? string.Empty);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
        }

        private void TryResize(int width, int height)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(id);
                appWindow?.Resize(new SizeInt32(width, height));
            }
            catch { }
        }

        public void Initialize(PolicyPlusPolicy policy, AdmxBundle bundle, IPolicySource compSource, IPolicySource userSource, AdmxPolicySection section)
        {
            _policy = policy;
            _bundle = bundle;
            _compSource = compSource;
            _userSource = userSource;
            _currentSection = section == AdmxPolicySection.Both ? AdmxPolicySection.Machine : section;

            NameBox.Text = policy.DisplayName;
            IdBox.Text = policy.UniqueID;
            DefinedInBox.Text = policy.RawPolicy.DefinedIn?.SourceFile ?? string.Empty;

            PathBox.Text = BuildPathText();

            _regFormattedCache = BuildRegistryTextFormatted();
            _regFileCache = BuildRegFile();
            _showRegFile = false;
            RegBox.Text = _regFormattedCache;
        }

        private string BuildPathText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(_policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "Computer Configuration",
                AdmxPolicySection.User => "User Configuration",
                _ => "Computer or User Configuration"
            });
            sb.AppendLine("+ Administrative Templates");
            var chain = GetCategoryChain(_policy.Category);
            int depth = 1; // administrative templates = depth 0, start child at 1
            foreach (var name in chain)
            {
                sb.AppendLine(new string(' ', depth * 2) + "+ " + name);
                depth++;
            }
            sb.AppendLine(new string(' ', depth * 2) + "+ " + _policy.DisplayName);
            return sb.ToString();
        }

        private string BuildRegistryTextFormatted()
        {
            var src = _currentSection == AdmxPolicySection.User ? _userSource : _compSource;
            var values = PolicyProcessing.GetReferencedRegistryValues(_policy);
            var sb = new StringBuilder();
            foreach (var kv in values)
            {
                var hive = _currentSection == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
                sb.AppendLine($"キー: {hive}\\{kv.Key}");
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    var data = src.GetValue(kv.Key, kv.Value);
                    var (typeName, dataText) = GetTypeAndDataText(data);
                    sb.AppendLine($"名前: {kv.Value}");
                    sb.AppendLine($"種類: {typeName}");
                    sb.AppendLine($"値: {dataText}");
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private string BuildRegFile()
        {
            var src = _currentSection == AdmxPolicySection.User ? _userSource : _compSource;
            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            var values = PolicyProcessing.GetReferencedRegistryValues(_policy);
            foreach (var kv in values)
            {
                sb.AppendLine();
                var hive = _currentSection == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
                sb.AppendLine($"[{hive}\\{kv.Key}]");
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    var data = src.GetValue(kv.Key, kv.Value);
                    if (data is null) continue;
                    sb.AppendLine(FormatRegValue(kv.Value, data));
                }
            }
            return sb.ToString();
        }

        private static string FormatRegValue(string name, object data)
        {
            if (data is uint u) return $"\"{name}\"=dword:{u:x8}";
            if (data is string s) return $"\"{name}\"=\"{EscapeRegString(s)}\"";
            if (data is string[] arr) return $"\"{name}\"=hex(7):{EncodeMultiString(arr)}";
            if (data is byte[] bin) return $"\"{name}\"=hex:{string.Join(",", bin.Select(b => b.ToString("x2")))}";
            if (data is ulong qu) { var b = BitConverter.GetBytes(qu); return $"\"{name}\"=hex(b):{string.Join(",", b.Select(x => x.ToString("x2")))}"; }
            return $"\"{name}\"=hex:{string.Join(",", (byte[])PolicyPlus.PolFile.ObjectToBytes(data, Microsoft.Win32.RegistryValueKind.Binary))}";
        }
        private static string EscapeRegString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string EncodeMultiString(string[] lines)
        {
            var bytes = new List<byte>();
            foreach (var line in lines)
            {
                var b = Encoding.Unicode.GetBytes(line);
                bytes.AddRange(b);
                bytes.Add(0); bytes.Add(0);
            }
            bytes.Add(0); bytes.Add(0);
            return string.Join(",", bytes.Select(b => b.ToString("x2")));
        }

        private static (string typeName, string dataText) GetTypeAndDataText(object? data)
        {
            if (data is null) return ("(not set)", "");
            switch (data)
            {
                case uint u:
                    return ("REG_DWORD", $"0x{u:x8} ({u})");
                case ulong uq:
                    return ("REG_QWORD", $"0x{uq:x16} ({uq})");
                case string s:
                    return ("REG_SZ", s);
                case string[] arr:
                    return ("REG_MULTI_SZ", string.Join("; ", arr));
                case byte[] bin:
                    return ("REG_BINARY", string.Join(" ", bin.Select(b => b.ToString("x2"))));
                default:
                    return ("(unknown)", Convert.ToString(data) ?? string.Empty);
            }
        }

        private static IEnumerable<string> GetCategoryChain(PolicyPlusCategory? cat)
        {
            if (cat == null) yield break;
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null)
            {
                stack.Push(cur.DisplayName);
                cur = cur.Parent;
            }
            foreach (var s in stack) yield return s;
        }
    }
}
