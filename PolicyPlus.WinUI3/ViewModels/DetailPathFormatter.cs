using System.Collections.Generic;
using System.Text;

namespace PolicyPlus.WinUI3.ViewModels
{
    public static class DetailPathFormatter
    {
        public static string BuildPathText(PolicyPlusPolicy policy)
        {
            var sb = new StringBuilder();
            sb.AppendLine(policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "Computer Configuration",
                AdmxPolicySection.User => "User Configuration",
                _ => "Computer or User Configuration"
            });
            sb.AppendLine("+ Administrative Templates");
            if (policy.Category != null)
            {
                foreach (var name in GetCategoryChain(policy.Category))
                    sb.AppendLine("  + " + name);
            }
            sb.Append("  + ").Append(policy.DisplayName);
            return sb.ToString();
        }

        private static IEnumerable<string> GetCategoryChain(PolicyPlusCategory cat)
        {
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null) { stack.Push(cur.DisplayName); cur = cur.Parent; }
            return stack;
        }
    }
}
