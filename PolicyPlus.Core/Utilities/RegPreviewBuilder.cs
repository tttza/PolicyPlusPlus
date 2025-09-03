using PolicyPlus.Core.IO;

using System;
using System.Linq;
using System.Text;

namespace PolicyPlus.Core.Utilities
{
    public static class RegPreviewBuilder
    {
        public static string BuildPreview(RegFile reg, int maxPerHive = 200)
        {
            if (reg == null) return string.Empty;
            static string GetHive(string keyName)
            {
                if (string.IsNullOrEmpty(keyName)) return "OTHER";
                var n = keyName.TrimStart('\\');
                if (n.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase) || n.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase) || n.StartsWith("CURRENT_USER\\", StringComparison.OrdinalIgnoreCase) || n.StartsWith("USER\\", StringComparison.OrdinalIgnoreCase)) return "HKCU";
                if (n.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase) || n.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase) || n.StartsWith("MACHINE\\", StringComparison.OrdinalIgnoreCase)) return "HKLM";
                if (n.StartsWith("HKEY_USERS\\", StringComparison.OrdinalIgnoreCase) || n.StartsWith("HKU\\", StringComparison.OrdinalIgnoreCase) || n.StartsWith("USERS\\", StringComparison.OrdinalIgnoreCase)) return "HKU";
                return "OTHER";
            }

            var groups = reg.Keys.GroupBy(k => GetHive(k.Name)).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var order = new[] { "HKCU", "HKLM", "HKU", "OTHER" };
            var sb = new StringBuilder();

            // Summary header
            int cnt(string k) => groups.TryGetValue(k, out var list) ? list.Count : 0;
            sb.AppendLine($"; Summary: HKCU={cnt("HKCU")} HKLM={cnt("HKLM")} HKU={cnt("HKU")} OTHER={cnt("OTHER")}");

            foreach (var hive in order)
            {
                if (!groups.TryGetValue(hive, out var keys) || keys.Count == 0) continue;
                sb.AppendLine();
                sb.AppendLine($"; {hive} ({keys.Count} key(s))");
                int shown = 0;
                foreach (var k in keys)
                {
                    if (shown >= maxPerHive) { sb.AppendLine("; ... (truncated) ..."); break; }
                    sb.AppendLine("[" + k.Name + "]");
                    foreach (var v in k.Values)
                        sb.AppendLine($"{(string.IsNullOrEmpty(v.Name) ? "@" : v.Name)} = {v.Kind} {FormatData(v)}");
                    sb.AppendLine();
                    shown++;
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string FormatData(RegFile.RegFileValue v)
        {
            if (v.Kind == Microsoft.Win32.RegistryValueKind.MultiString)
            {
                var arr = v.Data as string[] ?? Array.Empty<string>();
                return string.Join(" | ", arr.Select(s => s ?? string.Empty));
            }
            return Convert.ToString(v.Data) ?? string.Empty;
        }
    }
}
