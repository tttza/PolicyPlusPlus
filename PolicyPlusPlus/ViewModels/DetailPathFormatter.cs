using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PolicyPlusCore.Core;
using PolicyPlusPlus.Services;

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

        public static string BuildPathText(
            PolicyPlusPolicy policy,
            string joinSymbol,
            bool useSecondLanguage,
            string? secondLanguageCode
        )
        {
            var sb = new StringBuilder();

            string primaryLang = GetPrimaryLanguage();
            string lang = useSecondLanguage ? (secondLanguageCode ?? primaryLang) : primaryLang;
            bool isJa = lang.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase);
            string T(string en)
            {
                if (!isJa)
                    return en;
                return en switch
                {
                    "Computer Configuration" => "コンピューターの構成",
                    "User Configuration" => "ユーザーの構成",
                    "Computer or User Configuration" =>
                        "コンピューターの構成 または ユーザーの構成",
                    "Administrative Templates" => "管理用テンプレート",
                    _ => en,
                };
            }

            sb.AppendLine(
                policy.RawPolicy.Section switch
                {
                    AdmxPolicySection.Machine => T("Computer Configuration"),
                    AdmxPolicySection.User => T("User Configuration"),
                    _ => T("Computer or User Configuration"),
                }
            );

            sb.AppendLine("  " + joinSymbol + " " + T("Administrative Templates"));

            int depth = 2;
            if (policy.Category != null)
            {
                foreach (
                    var name in GetCategoryChain(
                        policy.Category,
                        useSecondLanguage,
                        secondLanguageCode
                    )
                )
                {
                    var display = string.IsNullOrWhiteSpace(name)
                        ? policy.Category.DisplayName
                        : name; // fallback safety
                    sb.AppendLine(new string(' ', depth * 2) + joinSymbol + " " + display);
                    depth++;
                }
            }

            string policyName = useSecondLanguage
                ? LocalizedTextService.GetPolicyNameIn(policy, secondLanguageCode ?? primaryLang)
                : policy.DisplayName;
            if (string.IsNullOrWhiteSpace(policyName))
                policyName = policy.DisplayName; // fallback
            sb.Append(new string(' ', depth * 2) + " " + policyName);

            return sb.ToString();
        }

        private static IEnumerable<string> GetCategoryChain(
            PolicyPlusCategory cat,
            bool useSecondLanguage,
            string? secondLanguageCode
        )
        {
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null)
            {
                string name = useSecondLanguage
                    ? LocalizedTextService.GetCategoryNameIn(
                        cur,
                        secondLanguageCode ?? GetPrimaryLanguage()
                    )
                    : cur.DisplayName;
                if (string.IsNullOrWhiteSpace(name))
                    name = cur.DisplayName; // ensure non-empty so test expectations hold
                stack.Push(name);
                cur = cur.Parent;
            }
            return stack;
        }

        private static string GetUiLanguage()
        {
            try
            {
                return CultureInfo.CurrentUICulture.Name;
            }
            catch
            {
                return "en-US";
            }
        }

        private static string GetPrimaryLanguage()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                var lang = s.Language;
                if (!string.IsNullOrWhiteSpace(lang))
                    return lang!;
                return GetUiLanguage();
            }
            catch
            {
                return GetUiLanguage();
            }
        }
    }
}
