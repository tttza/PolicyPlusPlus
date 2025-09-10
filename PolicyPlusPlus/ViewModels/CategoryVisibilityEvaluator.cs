using PolicyPlus.Core.Core;
using PolicyPlus.Core.IO;

using System;
using System.Collections.Generic;
using System.Linq;

namespace PolicyPlusPlus.ViewModels
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

            // Include pending (unsaved) changes so categories with pending items remain visible
            var pending = Services.PendingChangesService.Instance.Pending?.ToList() ?? new List<Services.PendingChange>();

            foreach (var p in seq)
            {
                // Check pending first
                bool hasPendingUser = pending.Any(pc => string.Equals(pc.PolicyId, p.UniqueID, StringComparison.OrdinalIgnoreCase)
                                                     && string.Equals(pc.Scope, "User", StringComparison.OrdinalIgnoreCase)
                                                     && (pc.DesiredState == PolicyState.Enabled || pc.DesiredState == PolicyState.Disabled));
                bool hasPendingComp = pending.Any(pc => string.Equals(pc.PolicyId, p.UniqueID, StringComparison.OrdinalIgnoreCase)
                                                     && string.Equals(pc.Scope, "Computer", StringComparison.OrdinalIgnoreCase)
                                                     && (pc.DesiredState == PolicyState.Enabled || pc.DesiredState == PolicyState.Disabled));

                if ((p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both) && hasPendingUser)
                    return true;
                if ((p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both) && hasPendingComp)
                    return true;

                // Fall back to actual source state
                try
                {
                    var comp = compSource != null ? PolicyProcessing.GetPolicyState(compSource, p) : PolicyState.Unknown;
                    var user = userSource != null ? PolicyProcessing.GetPolicyState(userSource, p) : PolicyState.Unknown;
                    if (comp == PolicyState.Enabled || comp == PolicyState.Disabled || user == PolicyState.Enabled || user == PolicyState.Disabled)
                        return true;
                }
                catch { }
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
