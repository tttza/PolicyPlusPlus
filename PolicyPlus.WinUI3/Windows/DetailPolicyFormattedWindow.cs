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
    public sealed partial class DetailPolicyFormattedWindow : Window
    {
        private PolicyPlusPolicy _policy = null!;
        private AdmxBundle _bundle = null!;
        private IPolicySource _compSource = null!;
        private IPolicySource _userSource = null!;
        private AdmxPolicySection _currentSection;

        private string _regFormattedCache = string.Empty;
        private string _regFileCache = string.Empty;
        private bool _showRegFile = false;

        public DetailPolicyFormattedWindow()
        {
            this.Title = "Policy Details - Formatted";
            InitializeComponent();

            CopyPathBtn.Click += (s, e) => CopyToClipboard(PathBox.Text);
            CopyRegBtn.Click += (s, e) => CopyToClipboard(RegBox.Text);
            ToggleViewBtn.Click += ToggleViewBtn_Click;
            CloseBtn.Click += (s, e) => this.Close();

            ApplyThemeResources();
            App.ThemeChanged += (s, e) => ApplyThemeResources();

            TryResize(600, 520);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);
        }

        private void ApplyThemeResources()
        {
            if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme;
            var inputBg = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"]; var inputStroke = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"]; var inputFg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            NameBox.Background = inputBg; NameBox.BorderBrush = inputStroke; NameBox.Foreground = inputFg;
            IdBox.Background = inputBg; IdBox.BorderBrush = inputStroke; IdBox.Foreground = inputFg;
            DefinedInBox.Background = inputBg; DefinedInBox.BorderBrush = inputStroke; DefinedInBox.Foreground = inputFg;
            PathBox.Background = inputBg; PathBox.BorderBrush = inputStroke; PathBox.Foreground = inputFg;
            RegBox.Background = inputBg; RegBox.BorderBrush = inputStroke; RegBox.Foreground = inputFg;
        }

        private void ToggleViewBtn_Click(object sender, RoutedEventArgs e)
        { _showRegFile = !_showRegFile; RegBox.Text = _showRegFile ? _regFileCache : _regFormattedCache; }

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
                    sb.AppendLine($"型: {typeName}");
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
                case uint u: return ("REG_DWORD", $"0x{u:x8} ({u})");
                case ulong uq: return ("REG_QWORD", $"0x{uq:x16} ({uq})");
                case string s: return ("REG_SZ", s);
                case string[] arr: return ("REG_MULTI_SZ", string.Join("; ", arr));
                case byte[] bin: return ("REG_BINARY", string.Join(" ", bin.Select(b => b.ToString("x2"))));
                default: return ("(unknown)", Convert.ToString(data) ?? string.Empty);
            }
        }

        private static IEnumerable<string> GetCategoryChain(PolicyPlusCategory? cat)
        {
            if (cat == null) yield break;
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null) { stack.Push(cur.DisplayName); cur = cur.Parent; }
            foreach (var s in stack) yield return s;
        }
    }
}
