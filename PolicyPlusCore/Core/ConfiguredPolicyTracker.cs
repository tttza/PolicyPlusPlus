using System;
using System.Collections.Concurrent;

namespace PolicyPlusCore.Core
{
    // Tracks which policies have ever been explicitly configured (Enabled or Disabled) within the current process lifetime.
    // Used to distinguish Disabled-as-deletion (no registry footprint) from never-configured in replace diff logic.
    public static class ConfiguredPolicyTracker
    {
        private static readonly ConcurrentDictionary<string, byte> _configured = new(
            StringComparer.OrdinalIgnoreCase
        );

        private static string Key(string policyId, string scope) =>
            (policyId ?? string.Empty) + "|" + (scope ?? string.Empty);

        public static void MarkConfigured(string policyId, string scope)
        {
            if (string.IsNullOrEmpty(policyId))
                return;
            _configured[Key(policyId, scope)] = 1;
        }

        public static bool WasEverConfigured(string policyId, string scope)
        {
            if (string.IsNullOrEmpty(policyId))
                return false;
            return _configured.ContainsKey(Key(policyId, scope));
        }

        public static void Reset()
        {
            _configured.Clear();
        }
    }
}
