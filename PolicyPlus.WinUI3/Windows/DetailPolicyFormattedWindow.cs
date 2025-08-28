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

        private TextBlock TitleText = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold };
        private ComboBox SectionSelector = new ComboBox { Width = 140 };
        private ComboBox ViewSelector = new ComboBox { Width = 160 };
        private TextBlock HintText = new TextBlock { Opacity = 0.7 };
        private TextBox OutputBox = new TextBox { AcceptsReturn = true, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas") };
        private Button CopyButton = new Button { Content = "Copy" };
        private Button CloseButton = new Button { Content = "Close" };

        public DetailPolicyFormattedWindow()
        {
            this.Title = "Policy details";

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 12, 12, 6) };
            header.Children.Add(TitleText);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Controls
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 0, 12, 6) };
            SectionSelector.Items.Add(new ComboBoxItem { Content = "Computer" });
            SectionSelector.Items.Add(new ComboBoxItem { Content = "User" });
            SectionSelector.SelectionChanged += SectionSelector_SelectionChanged;
            controls.Children.Add(new TextBlock { Text = "Section:", VerticalAlignment = VerticalAlignment.Center });
            controls.Children.Add(SectionSelector);

            ViewSelector.Items.Add(new ComboBoxItem { Content = "Formatted" });
            ViewSelector.Items.Add(new ComboBoxItem { Content = ".reg" });
            ViewSelector.SelectionChanged += ViewSelector_SelectionChanged;
            controls.Children.Add(new TextBlock { Text = "View:", VerticalAlignment = VerticalAlignment.Center });
            controls.Children.Add(ViewSelector);

            CopyButton.Click += CopyButton_Click;
            CloseButton.Click += (s, e) => this.Close();
            controls.Children.Add(CopyButton);
            controls.Children.Add(CloseButton);
            Grid.SetRow(controls, 1);
            root.Children.Add(controls);

            // Output
            var outGrid = new Grid { Margin = new Thickness(12, 0, 12, 12) };
            outGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            HintText.Margin = new Thickness(0, 0, 0, 4);
            Grid.SetRow(HintText, 0);
            outGrid.Children.Add(HintText);
            Grid.SetRow(OutputBox, 1);
            outGrid.Children.Add(OutputBox);
            Grid.SetRow(outGrid, 2);
            root.Children.Add(outGrid);

            this.Content = root;
            TryResize(820, 600);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
        }

        public void BringToFront() => WindowHelpers.BringToFront(this);
        private void TryResize(int width, int height)
        {
            WindowHelpers.Resize(this, width, height);
        }

        public void Initialize(PolicyPlusPolicy policy, AdmxBundle bundle, IPolicySource compSource, IPolicySource userSource, AdmxPolicySection section)
        {
            _policy = policy;
            _bundle = bundle;
            _compSource = compSource;
            _userSource = userSource;
            _currentSection = section == AdmxPolicySection.Both ? AdmxPolicySection.Machine : section;
            TitleText.Text = policy.DisplayName;
            SectionSelector.SelectedIndex = _currentSection == AdmxPolicySection.Machine ? 0 : 1;
            ViewSelector.SelectedIndex = 0;
            RefreshOutput();
        }

        private void SectionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentSection = SectionSelector.SelectedIndex == 0 ? AdmxPolicySection.Machine : AdmxPolicySection.User;
            RefreshOutput();
        }

        private void ViewSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshOutput();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var data = new global::Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
            data.SetText(OutputBox.Text ?? string.Empty);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
        }

        private void RefreshOutput()
        {
            var src = _currentSection == AdmxPolicySection.User ? _userSource : _compSource;
            var state = PolicyProcessing.GetPolicyState(src, _policy);
            bool showReg = ViewSelector.SelectedIndex == 1;
            if (showReg)
            {
                OutputBox.Text = BuildRegExport(src);
                HintText.Text = "This is a .reg snippet you can copy.";
            }
            else
            {
                OutputBox.Text = BuildFormatted(src, state);
                HintText.Text = "Formatted view of the policy path, state, options, and registry.";
            }
        }

        private string BuildFormatted(IPolicySource src, PolicyState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Path:");
            sb.AppendLine(_policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "  Computer Configuration",
                AdmxPolicySection.User => "  User Configuration",
                _ => "  Computer/User Configuration"
            });
            sb.AppendLine("    + Administrative Templates");
            if (_policy.Category != null)
            {
                foreach (var catName in GetCategoryChain(_policy.Category))
                    sb.AppendLine($"    + {catName}");
            }
            sb.AppendLine($"    + {_policy.DisplayName}");
            sb.AppendLine();

            var applies = _policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "Computer",
                AdmxPolicySection.User => "User",
                _ => "Both"
            };
            sb.AppendLine($"Applies to: {applies}");
            if (_policy.SupportedOn != null)
                sb.AppendLine($"Supported on: {_policy.SupportedOn.DisplayName}");
            if (!string.IsNullOrWhiteSpace(_policy.DisplayExplanation))
            {
                sb.AppendLine();
                sb.AppendLine("Description:");
                sb.AppendLine(IndentLines(_policy.DisplayExplanation.Trim(), "  "));
            }
            sb.AppendLine();

            sb.AppendLine($"State: {state}");

            if (state == PolicyState.Enabled)
            {
                var options = PolicyProcessing.GetPolicyOptionStates(src, _policy);
                if (options.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Options:");
                    foreach (var (label, value) in FormatOptions(options))
                    {
                        sb.AppendLine($"  {label}: {value}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("Registry:");
            var values = PolicyProcessing.GetReferencedRegistryValues(_policy);
            if (values.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                foreach (var kv in values)
                {
                    var hivePrefix = RootForSection(_currentSection);
                    sb.AppendLine($"  [{hivePrefix}\\{kv.Key}]");
                    if (!string.IsNullOrEmpty(kv.Value))
                    {
                        var data = src.GetValue(kv.Key, kv.Value);
                        var (typeName, dataText) = GetTypeAndDataText(data);
                        sb.AppendLine($"    Name: {kv.Value}");
                        sb.AppendLine($"    Type: {typeName}");
                        foreach (var line in SplitMultiline(dataText))
                            sb.AppendLine($"    Data: {line}");
                    }
                }
            }

            return sb.ToString();
        }

        private IEnumerable<(string label, string value)> FormatOptions(Dictionary<string, object> options)
        {
            if (_policy.Presentation?.Elements == null || _policy.RawPolicy.Elements == null)
                yield break;
            var elemDict = _policy.RawPolicy.Elements.ToDictionary(e => e.ID);
            foreach (var pres in _policy.Presentation.Elements)
            {
                if (string.Equals(pres.ElementType, "text", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!options.TryGetValue(pres.ID, out var val))
                    continue;
                string label = pres switch
                {
                    CheckBoxPresentationElement cb => cb.Text,
                    TextBoxPresentationElement tb => tb.Label,
                    NumericBoxPresentationElement nb => nb.Label,
                    ComboBoxPresentationElement cbx => cbx.Label,
                    DropDownPresentationElement dd => dd.Label,
                    ListPresentationElement lp => lp.Label,
                    MultiTextPresentationElement mt => mt.Label,
                    _ => pres.ID
                } ?? pres.ID;
                string text = FormatOptionValue(pres, elemDict, val);
                yield return (label, text);
            }
        }

        private string FormatOptionValue(PresentationElement pres, Dictionary<string, PolicyElement> elemDict, object val)
        {
            switch (pres.ElementType)
            {
                case "decimalTextBox":
                    return val?.ToString() ?? string.Empty;
                case "textBox":
                case "comboBox":
                    return Convert.ToString(val) ?? string.Empty;
                case "checkBox":
                    return (val is bool b && b) ? "True" : "False";
                case "dropdownList":
                    {
                        if (!(val is int idx)) return Convert.ToString(val) ?? string.Empty;
                        if (!elemDict.TryGetValue(pres.ID, out var pe) || pe is not EnumPolicyElement ee) return idx.ToString();
                        if (idx < 0 || idx >= ee.Items.Count) return idx.ToString();
                        var disp = _bundle.ResolveString(ee.Items[idx].DisplayCode, _policy.RawPolicy.DefinedIn);
                        return string.IsNullOrWhiteSpace(disp) ? idx.ToString() : disp;
                    }
                case "listBox":
                    {
                        if (val is List<string> ls)
                            return $"{ls.Count} item(s): " + string.Join(", ", ls);
                        if (val is List<KeyValuePair<string, string>> kvp)
                            return $"{kvp.Count} pair(s): " + string.Join(", ", kvp.Select(p => $"{p.Key}={p.Value}"));
                        if (val is Dictionary<string, string> dict)
                            return $"{dict.Count} pair(s): " + string.Join(", ", dict.Select(p => $"{p.Key}={p.Value}"));
                        return Convert.ToString(val) ?? string.Empty;
                    }
                case "multiTextBox":
                    {
                        if (val is string[] arr)
                            return string.Join(" | ", arr);
                        return Convert.ToString(val) ?? string.Empty;
                    }
            }
            return Convert.ToString(val) ?? string.Empty;
        }

        private static IEnumerable<string> GetCategoryChain(PolicyPlusCategory cat)
        {
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null)
            {
                stack.Push(cur.DisplayName);
                cur = cur.Parent;
            }
            return stack;
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
                    return ("REG_SZ", $"\"{s}\"");
                case string[] arr:
                    return ("REG_MULTI_SZ", string.Join("\n          ", arr.Select(x => $"- {x}")));
                case byte[] bin:
                    return ("REG_BINARY", string.Join(" ", bin.Select(b => b.ToString("x2"))));
                default:
                    return ("(unknown)", Convert.ToString(data) ?? string.Empty);
            }
        }

        private static IEnumerable<string> SplitMultiline(string s)
        {
            if (string.IsNullOrEmpty(s)) { yield return string.Empty; yield break; }
            var parts = s.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var p in parts) yield return p;
        }

        private static string RootForSection(AdmxPolicySection section)
        {
            return section == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
        }

        private static string IndentLines(string text, string indent)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(indent).AppendLine(lines[i]);
            }
            return sb.ToString();
        }

        private string BuildRegExport(IPolicySource src)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            var values = PolicyProcessing.GetReferencedRegistryValues(_policy);
            foreach (var kv in values)
            {
                sb.AppendLine();
                sb.AppendLine($"[{RootForSection(_currentSection)}\\{kv.Key}]");
                if (string.IsNullOrEmpty(kv.Value))
                {
                    continue;
                }
                var data = src.GetValue(kv.Key, kv.Value);
                if (data is null) continue;
                sb.AppendLine(FormatRegValue(kv.Value, data));
            }
            return sb.ToString();
        }

        private static string FormatRegValue(string name, object data)
        {
            if (data is uint u)
                return $"\"{name}\"=dword:{u:x8}";
            if (data is string s)
                return $"\"{name}\"=\"{EscapeRegString(s)}\"";
            if (data is string[] arr)
            {
                var encoded = EncodeMultiString(arr);
                return $"\"{name}\"=hex(7):{encoded}";
            }
            if (data is byte[] bin)
            {
                return $"\"{name}\"=hex:{string.Join(",", bin.Select(b => b.ToString("x2")))}";
            }
            if (data is ulong qu)
            {
                var b = BitConverter.GetBytes(qu); // little-endian
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
            bytes.Add(0); bytes.Add(0); // double-null
            return string.Join(",", bytes.Select(b => b.ToString("x2")));
        }
    }
}
