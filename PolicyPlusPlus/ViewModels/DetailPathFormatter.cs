using PolicyPlus.Core.Core;
using PolicyPlusPlus.Services;

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PolicyPlusPlus.ViewModels
{
    public static class DetailPathFormatter
    {
        public static string BuildPathText(PolicyPlusPolicy policy, string joinSymbol = "+")
        {
            var s = SettingsService.Instance.LoadSettings();
            bool secondEnabled = s.SecondLanguageEnabled ?? false;
            string secondLang = s.SecondLanguage ?? "en-US";
            return BuildPathText(policy, joinSymbol, secondEnabled, secondLang);
        }

        public static string BuildPathText(PolicyPlusPolicy policy, string joinSymbol, bool useSecondLanguage, string? secondLanguageCode)
        {
            var sb = new StringBuilder();

            // Use app-selected primary language (ADMX language) instead of OS UI culture for consistency
            string primaryLang = GetPrimaryLanguage();
            string lang = useSecondLanguage ? (secondLanguageCode ?? primaryLang) : primaryLang;
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

            sb.AppendLine("  " + joinSymbol + " " + T("Administrative Templates"));

            int depth = 2;
            if (policy.Category != null)
            {
                foreach (var name in GetCategoryChain(policy.Category, useSecondLanguage, secondLanguageCode))
                {
                    sb.AppendLine(new string(' ', depth * 2) + joinSymbol + " " + name);
                    depth++;
                }
            }

            string policyName = useSecondLanguage ? LocalizedTextService.GetPolicyNameIn(policy, secondLanguageCode ?? primaryLang) : policy.DisplayName;
            sb.Append(new string(' ', depth * 2) + " " + policyName);

            return sb.ToString();
        }

        private static IEnumerable<string> GetCategoryChain(PolicyPlusCategory cat, bool useSecondLanguage, string? secondLanguageCode)
        {
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null)
            {
                string name = useSecondLanguage ? LocalizedTextService.GetCategoryNameIn(cur, secondLanguageCode ?? GetPrimaryLanguage()) : cur.DisplayName;
                stack.Push(name);
                cur = cur.Parent;
            }
            return stack;
        }

        private static string GetUiLanguage()
        {
            try { return CultureInfo.CurrentUICulture.Name; } catch { return "en-US"; }
        }

        private static string GetPrimaryLanguage()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                var lang = s.Language;
                if (!string.IsNullOrWhiteSpace(lang)) return lang!;
                return GetUiLanguage();
            }
            catch { return GetUiLanguage(); }
        }
    }
}
