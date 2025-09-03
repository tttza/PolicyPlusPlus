using PolicyPlus.Core.Core;
using PolicyPlus.WinUI3.Services;

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PolicyPlus.WinUI3.ViewModels
{
    public static class DetailPathFormatter
    {
        public static string BuildPathText(PolicyPlusPolicy policy)
        {
            var sb = new StringBuilder();

            string lang = GetLanguage();
            bool isJa = lang.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase);
            string T(string en)
            {
                if (!isJa) return en;
                return en switch
                {
                    "Computer Configuration" => "コンピューターの構成",
                    "User Configuration" => "ユーザーの構成",
                    "Computer or User Configuration" => "コンピューターの構成 または ユーザーの構成",
                    "Administrative Templates" => "管理用テンプレート",
                    _ => en
                };
            }

            sb.AppendLine(policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => T("Computer Configuration"),
                AdmxPolicySection.User => T("User Configuration"),
                _ => T("Computer or User Configuration")
            });

            // "Administrative Templates" at depth 1 (2 spaces + "+ ")
            sb.AppendLine("  + " + T("Administrative Templates"));

            // Categories: increase indent per depth
            int depth = 2; // start from depth 2 like legacy implementation
            if (policy.Category != null)
            {
                foreach (var name in GetCategoryChain(policy.Category))
                {
                    sb.AppendLine(new string(' ', depth * 2) + "+ " + name);
                    depth++;
                }
            }

            // Leaf: policy display name, no plus, indented one level deeper than last category
            sb.Append(new string(' ', depth * 2) + " " + policy.DisplayName);

            return sb.ToString();
        }

        private static IEnumerable<string> GetCategoryChain(PolicyPlusCategory cat)
        {
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null) { stack.Push(cur.DisplayName); cur = cur.Parent; }
            return stack;
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
