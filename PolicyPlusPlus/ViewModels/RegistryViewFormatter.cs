using PolicyPlus.Core.Core;
using PolicyPlus.Core.IO;
using PolicyPlusPlus.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PolicyPlusPlus.ViewModels
{
    public static class RegistryViewFormatter
    {
        public static string BuildRegistryFormatted(PolicyPlusPolicy policy, IPolicySource source, AdmxPolicySection section)
        {
            // Default: use app UI language for labels
            var uiLang = GetLanguage();
            return BuildRegistryFormatted(policy, source, section, useSecondLanguage: false, secondLanguageCode: uiLang);
        }

        public static string BuildRegistryFormatted(PolicyPlusPolicy policy, IPolicySource source, AdmxPolicySection section, bool useSecondLanguage, string? secondLanguageCode)
        {
            string lang = useSecondLanguage ? (secondLanguageCode ?? GetLanguage()) : GetLanguage();
            bool isJa = lang.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
            string L(string en)
            {
                if (!isJa) return en;
                return en switch
                {
                    "Key" => "�L�[",
                    "Name" => "���O",
                    "Type" => "���",
                    "Value" => "�l",
                    _ => en
                };
            }
            string GetText(string en)
            {
                if (!isJa) return en;
                return en switch
                {
                    "(no referenced registry values)" => "(�Q�Ƃ���Ă��郌�W�X�g���l�͂���܂���)",
                    _ => en
                };
            }

            var sb = new StringBuilder();
            var values = PolicyProcessing.GetReferencedRegistryValues(policy);
            if (values.Count == 0)
            {
                return GetText("(no referenced registry values)");
            }
            foreach (var kv in values)
            {
                var hive = RootForSection(section);
                // Header block: Key, Name, Type, Value (localized labels)
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    var data = source.GetValue(kv.Key, kv.Value);
                    var (typeName, dataText) = GetTypeAndDataText(data);
                    sb.AppendLine($"{L("Key")}: {hive}\\{kv.Key}");
                    sb.AppendLine($"{L("Name")}: {kv.Value}");
                    sb.AppendLine($"{L("Type")}: {typeName}");
                    if (string.IsNullOrEmpty(dataText))
                    {
                        sb.AppendLine($"{L("Value")}: ");
                    }
                    else
                    {
                        // First line uses label, subsequent lines are just the content lines
                        var first = true;
                        foreach (var line in SplitMultiline(dataText))
                        {
                            if (first)
                            {
                                sb.AppendLine($"{L("Value")}: {line}");
                                first = false;
                            }
                            else
                            {
                                sb.AppendLine(line);
                            }
                        }
                    }
                }
                else
                {
                    // List-style or key-only reference: enumerate all present values under the key.
                    sb.AppendLine($"{L("Key")}: {hive}\\{kv.Key}");
                    sb.AppendLine(); // Blank line before repeating Name/Type/Value blocks for readability
                    var valueNames = new List<string>();
                    try { valueNames = source.GetValueNames(kv.Key); } catch { }
                    bool firstValue = true;
                    foreach (var name in valueNames)
                    {
                        if (!firstValue)
                        {
                            // Separate each value block with a blank line
                            sb.AppendLine();
                        }
                        firstValue = false;
                        var data = source.GetValue(kv.Key, name);
                        var (typeName, dataText) = GetTypeAndDataText(data);
                        sb.AppendLine($"{L("Name")}: {name}");
                        sb.AppendLine($"{L("Type")}: {typeName}");
                        if (string.IsNullOrEmpty(dataText))
                        {
                            sb.AppendLine($"{L("Value")}: ");
                        }
                        else
                        {
                            var first = true;
                            foreach (var line in SplitMultiline(dataText))
                            {
                                if (first)
                                {
                                    sb.AppendLine($"{L("Value")}: {line}");
                                    first = false;
                                }
                                else
                                {
                                    sb.AppendLine(line);
                                }
                            }
                        }
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public static string BuildRegExport(PolicyPlusPolicy policy, IPolicySource source, AdmxPolicySection section)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            var values = PolicyProcessing.GetReferencedRegistryValues(policy);
            foreach (var kv in values)
            {
                sb.AppendLine();
                var hive = RootForSection(section);
                sb.AppendLine($"[{hive}\\{kv.Key}]");
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    var data = source.GetValue(kv.Key, kv.Value);
                    if (data is null) continue;
                    sb.AppendLine(FormatRegValue(kv.Value, data));
                }
                else
                {
                    // Emit all values in the key for list-style entries
                    foreach (var name in source.GetValueNames(kv.Key))
                    {
                        var data = source.GetValue(kv.Key, name);
                        if (data is null) continue;
                        sb.AppendLine(FormatRegValue(name, data));
                    }
                }
            }
            return sb.ToString();
        }

        public static string RootForSection(AdmxPolicySection section)
        {
            return section == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
        }

        public static (string typeName, string dataText) GetTypeAndDataText(object? data)
        {
            if (data is null) return ("(not set)", "");
            switch (data)
            {
                case uint u: return ("REG_DWORD", $"{u}");
                case ulong uq: return ("REG_QWORD", $"{uq}");
                case string s: return ("REG_SZ", s);
                case string[] arr: return ("REG_MULTI_SZ", string.Join(" | ", arr));
                case byte[] bin: return ("REG_BINARY", string.Join(" ", bin.Select(b => b.ToString("x2"))));
                default: return ("(unknown)", Convert.ToString(data) ?? string.Empty);
            }
        }

        public static IEnumerable<string> SplitMultiline(string s)
        {
            if (string.IsNullOrEmpty(s)) { yield return string.Empty; yield break; }
            var parts = s.Replace("\r\n", "\n").Split('\n');
            foreach (var p in parts) yield return p;
        }

        public static string FormatRegValue(string name, object data)
        {
            if (data is uint u) return $"\"{name}\"=dword:{u:x8}";
            if (data is string s) return $"\"{name}\"=\"{EscapeRegString(s)}\"";
            if (data is string[] arr) return $"\"{name}\"=hex(7):{EncodeMultiString(arr)}";
            if (data is byte[] bin) return $"\"{name}\"=hex:{string.Join(",", bin.Select(b => b.ToString("x2")))}";
            if (data is ulong qu)
            {
                var b = BitConverter.GetBytes(qu); // little-endian order
                return $"\"{name}\"=hex(b):{string.Join(",", b.Select(x => x.ToString("x2")))}";
            }
            // Fallback for other numeric types
            try
            {
                var kind = Microsoft.Win32.RegistryValueKind.Binary;
                var bytes = (byte[])PolFile.ObjectToBytes(data, kind);
                return $"\"{name}\"=hex:{string.Join(",", bytes.Select(b => b.ToString("x2")))}";
            }
            catch
            {
                return $"\"{name}\"=hex:";
            }
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

        private static string GetLanguage()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                if (!string.IsNullOrWhiteSpace(s.Language))
                    return s.Language!;
            }
            catch { }
            try { return CultureInfo.CurrentUICulture.Name; } catch { return "en-US"; }
        }
    }
}
