using System;
using System.Collections.Generic;
using System.Linq;

namespace PolicyPlus.WinUI3.ViewModels
{
    public static class CategoryVisibilityEvaluator
    {
        public static bool IsCategoryVisible(PolicyPlusCategory cat, IEnumerable<PolicyPlusPolicy> allPolicies,
            AdmxPolicySection appliesFilter, bool configuredOnly, IPolicySource? compSource, IPolicySource? userSource)
        {
            if (cat == null) return false;

            // When not using Configured Only, fall back to simple emptiness check
            if (!configuredOnly)
            {
                return !IsCategoryEmpty(cat);
            }

            // Collect all policies within this category subtree
            var ids = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            CollectPoliciesRecursive(cat, ids);
            IEnumerable<PolicyPlusPolicy> seq = allPolicies.Where(p => ids.Contains(p.UniqueID));

            // Respect the Applies filter
            if (appliesFilter == AdmxPolicySection.Machine)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both);
            else if (appliesFilter == AdmxPolicySection.User)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both);

            if (!seq.Any()) return false;
            if (compSource == null || userSource == null) return false;

            foreach (var p in seq)
            {
                var comp = PolicyProcessing.GetPolicyState(compSource, p);
                var user = PolicyProcessing.GetPolicyState(userSource, p);
                if (comp == PolicyState.Enabled || comp == PolicyState.Disabled || user == PolicyState.Enabled || user == PolicyState.Disabled)
                    return true;
            }
            return false;
        }

        private static bool IsCategoryEmpty(PolicyPlusCategory cat)
        {
            if (cat.Policies.Count > 0)
                return false;
            foreach (var child in cat.Children)
            {
                if (!IsCategoryEmpty(child))
                    return false;
            }
            return true;
        }

        private static void CollectPoliciesRecursive(PolicyPlusCategory cat, HashSet<string> sink)
        { foreach (var p in cat.Policies) sink.Add(p.UniqueID); foreach (var child in cat.Children) CollectPoliciesRecursive(child, sink); }
    }
}
