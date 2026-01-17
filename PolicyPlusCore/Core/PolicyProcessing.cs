using System.Diagnostics;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core.Policies;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core
{
    public class PolicyProcessing
    {
        // Internal helper to emit lightweight debug info without introducing dependency on WinUI logging layer.
        private static void LogDebug(string area, string message)
        {
            try
            {
                Debug.WriteLine($"[Core:{area}] {message}");
            }
            catch { }
        }

        private static void LogError(string area, string message, Exception ex)
        {
            try
            {
                Debug.WriteLine(
                    $"[Core:{area}] ERROR {message} :: {ex.GetType().Name} {ex.Message}"
                );
            }
            catch { }
        }

        /// <summary>
        /// Evaluates the current state of a policy based on registry values.
        /// Delegates to <see cref="PolicyStateEvaluator"/> for the actual evaluation logic.
        /// </summary>
        public static PolicyState GetPolicyState(
            IPolicySource PolicySource,
            PolicyPlusPolicy Policy
        )
        {
            return PolicyStateEvaluator.Evaluate(PolicySource, Policy);
        }

        public static int DeduplicatePolicies(AdmxBundle Workspace)
        {
            int dedupeCount = 0;
            foreach (var cat in Workspace.Policies.GroupBy(c => c.Value.Category))
            {
                foreach (
                    var namegroup in cat.GroupBy(p => p.Value.DisplayName)
                        .Select(x => x.ToList())
                        .ToList()
                )
                {
                    if (namegroup.Count != 2)
                        continue;
                    var a = namegroup[0].Value;
                    var b = namegroup[1].Value;
                    if (
                        (int)a.RawPolicy.Section + (int)b.RawPolicy.Section
                        != (int)AdmxPolicySection.Both
                    )
                        continue;
                    if ((a.DisplayExplanation ?? "") != (b.DisplayExplanation ?? ""))
                        continue;
                    if ((a.RawPolicy.RegistryKey ?? "") != (b.RawPolicy.RegistryKey ?? ""))
                        continue;
                    a.Category?.Policies.Remove(a);
                    Workspace.Policies.Remove(a.UniqueID);
                    b.RawPolicy.Section = AdmxPolicySection.Both;
                    dedupeCount += 1;
                }
            }

            return dedupeCount;
        }

        /// <summary>
        /// Retrieves the current option states for a policy's elements.
        /// Delegates to <see cref="PolicyOptionReader"/> (ADR 0013 Phase 3a).
        /// </summary>
        public static Dictionary<string, object> GetPolicyOptionStates(
            IPolicySource PolicySource,
            PolicyPlusPolicy Policy
        )
        {
            return PolicyOptionReader.GetOptionStates(PolicySource, Policy);
        }

        /// <summary>
        /// Gets all registry key/value pairs referenced by a policy.
        /// Delegates to <see cref="PolicyRegistryWalker"/> (ADR 0013 Phase 3b).
        /// </summary>
        public static List<RegistryKeyValuePair> GetReferencedRegistryValues(
            PolicyPlusPolicy Policy
        )
        {
            return PolicyRegistryWalker.GetReferencedValues(Policy);
        }

        /// <summary>
        /// Clears all registry values associated with a policy.
        /// Delegates to <see cref="PolicyRegistryWalker"/> (ADR 0013 Phase 3b).
        /// </summary>
        public static void ForgetPolicy(IPolicySource PolicySource, PolicyPlusPolicy Policy)
        {
            PolicyRegistryWalker.Forget(PolicySource, Policy);
        }

        /// <summary>
        /// Apply the specified policy state (Enabled/Disabled) to the policy source.
        /// Delegates to PolicyStateApplier (ADR 0013 Phase 2).
        /// </summary>
        public static void SetPolicyState(
            IPolicySource PolicySource,
            PolicyPlusPolicy Policy,
            PolicyState State,
            Dictionary<string, object> Options
        )
        {
            Policies.PolicyStateApplier.Apply(PolicySource, Policy, State, Options);
        }

        /// <summary>
        /// Determines if a policy is supported by the given product set.
        /// Delegates to <see cref="PolicySupportChecker"/> (ADR 0013 Phase 3c).
        /// </summary>
        public static bool IsPolicySupported(
            PolicyPlusPolicy Policy,
            List<PolicyPlusProduct> Products,
            bool AlwaysUseAny,
            bool ApproveLiterals
        )
        {
            return PolicySupportChecker.IsSupported(
                Policy,
                Products,
                AlwaysUseAny,
                ApproveLiterals
            );
        }
    }

    public enum PolicyState
    {
        NotConfigured = 0,
        Disabled = 1,
        Enabled = 2,
        Unknown = 3,
    }

    public class RegistryKeyValuePair : IEquatable<RegistryKeyValuePair?>
    {
        public string Key = string.Empty;
        public string Value = string.Empty;

        bool IEquatable<RegistryKeyValuePair?>.Equals(RegistryKeyValuePair? other)
        {
            if (other is null)
                return false;
            return other.Key.Equals(Key, StringComparison.InvariantCultureIgnoreCase)
                && other.Value.Equals(Value, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool EqualsRKVP(RegistryKeyValuePair? other)
        {
            return ((IEquatable<RegistryKeyValuePair>)this).Equals(other);
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is RegistryKeyValuePair))
                return false;
            return EqualsRKVP((RegistryKeyValuePair)obj);
        }

        public override int GetHashCode()
        {
            return Key.ToLowerInvariant().GetHashCode() ^ Value.ToLowerInvariant().GetHashCode();
        }
    }

    public class Registry : RegistryKeyValuePair
    {
        public string Type = string.Empty;
    }
}
