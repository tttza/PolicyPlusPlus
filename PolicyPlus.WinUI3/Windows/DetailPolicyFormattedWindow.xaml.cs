using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using PolicyPlus.WinUI3.Utils;
using Windows.ApplicationModel.DataTransfer;

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

            WindowHelpers.ResizeForDisplayScale(this, 600, 520);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);

            try { ScaleHelper.Attach(this, ScaleHost, RootShell); } catch { }
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
        {
            _showRegFile = !_showRegFile;
            RegBox.Text = _showRegFile ? _regFileCache : _regFormattedCache;
        }

        private void CopyToClipboard(string text)
        {
            try
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(text ?? string.Empty);
                Clipboard.SetContent(dp);
            }
            catch { }
        }

        public void Initialize(PolicyPlusPolicy policy, AdmxBundle bundle, IPolicySource compSource, IPolicySource userSource, AdmxPolicySection section)
        {
            _policy = policy; _bundle = bundle; _compSource = compSource; _userSource = userSource; _currentSection = section;

            NameBox.Text = policy.DisplayName;
            IdBox.Text = policy.UniqueID;
            DefinedInBox.Text = System.IO.Path.GetFileName(policy.RawPolicy.DefinedIn?.SourceFile ?? string.Empty);

            // Build path panel
            PathBox.Text = BuildPathText();

            // Build registry panel (formatted + .reg)
            var src = _currentSection == AdmxPolicySection.User ? _userSource : _compSource;
            _regFormattedCache = BuildRegistryFormatted(src);
            _regFileCache = BuildRegExport(src);
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
            if (_policy.Category != null)
            {
                foreach (var name in GetCategoryChain(_policy.Category))
                    sb.AppendLine("  + " + name);
            }
            sb.Append("  + ").Append(_policy.DisplayName);
            return sb.ToString();
        }

        private static IEnumerable<string> GetCategoryChain(PolicyPlusCategory cat)
        {
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null) { stack.Push(cur.DisplayName); cur = cur.Parent; }
            return stack;
        }

        private string BuildRegistryFormatted(IPolicySource src)
        {
            var sb = new StringBuilder();
            var values = PolicyProcessing.GetReferencedRegistryValues(_policy);
            if (values.Count == 0)
            {
                return "(no referenced registry values)";
            }
            foreach (var kv in values)
            {
                var hive = RootForSection(_currentSection);
                sb.AppendLine($"[{hive}\\{kv.Key}]");
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    var data = src.GetValue(kv.Key, kv.Value);
                    var (typeName, dataText) = GetTypeAndDataText(data);
                    sb.AppendLine($"  Name: {kv.Value}");
                    sb.AppendLine($"  Type: {typeName}");
                    foreach (var line in SplitMultiline(dataText))
                        sb.AppendLine($"  Data: {line}");
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private string BuildRegExport(IPolicySource src)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            var values = PolicyProcessing.GetReferencedRegistryValues(_policy);
            foreach (var kv in values)
            {
                sb.AppendLine();
                var hive = RootForSection(_currentSection);
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

        private static string RootForSection(AdmxPolicySection section)
        {
            return section == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
        }

        private static (string typeName, string dataText) GetTypeAndDataText(object? data)
        {
            if (data is null) return ("(not set)", "");
            switch (data)
            {
                case uint u: return ("REG_DWORD", $"0x{u:x8} ({u})");
                case ulong uq: return ("REG_QWORD", $"0x{uq:x16} ({uq})");
                case string s: return ("REG_SZ", s);
                case string[] arr: return ("REG_MULTI_SZ", string.Join(" | ", arr));
                case byte[] bin: return ("REG_BINARY", string.Join(" ", bin.Select(b => b.ToString("x2"))));
                default: return ("(unknown)", Convert.ToString(data) ?? string.Empty);
            }
        }

        private static IEnumerable<string> SplitMultiline(string s)
        {
            if (string.IsNullOrEmpty(s)) { yield return string.Empty; yield break; }
            var parts = s.Replace("\r\n", "\n").Split('\n');
            foreach (var p in parts) yield return p;
        }

        private static string FormatRegValue(string name, object data)
        {
            if (data is uint u) return $"\"{name}\"=dword:{u:x8}";
            if (data is string s) return $"\"{name}\"=\"{EscapeRegString(s)}\"";
            if (data is string[] arr) return $"\"{name}\"=hex(7):{EncodeMultiString(arr)}";
            if (data is byte[] bin) return $"\"{name}\"=hex:{string.Join(",", bin.Select(b => b.ToString("x2")))}";
            if (data is ulong qu)
            {
                var b = BitConverter.GetBytes(qu);
                return $"\"{name}\"=hex(b):{string.Join(",", b.Select(x => x.ToString("x2")))}";
            }
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
    }
}
