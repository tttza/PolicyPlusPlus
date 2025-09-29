using System;
using System.Collections.Generic;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Utilities
{
    // Provides helpers to strip registry hive prefixes and an adapter IPolicySource that can resolve keys with or without hives.
    public static class RegistryHiveNormalization
    {
        private static readonly string[] MachinePrefixes = { "HKEY_LOCAL_MACHINE\\", "HKLM\\" };
        private static readonly string[] UserPrefixes =
        {
            "HKEY_CURRENT_USER\\",
            "HKCU\\",
            "HKEY_USERS\\",
            "HKU\\",
        };

        public static string StripHive(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            foreach (var p in MachinePrefixes)
                if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return path.Substring(p.Length);
            foreach (var p in UserPrefixes)
                if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return path.Substring(p.Length);
            return path;
        }

        public static bool LooksMachine(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            foreach (var p in MachinePrefixes)
                if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static bool LooksUser(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            foreach (var p in UserPrefixes)
                if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // Read-only adapter for PolFile lookups permitting hive or hive-less keys interchangeably.
        public sealed class HiveFlexiblePolicySource : IPolicySource
        {
            private readonly PolFile _pol;
            private readonly bool _isMachine;

            public HiveFlexiblePolicySource(PolFile pol, bool isMachine)
            {
                _pol = pol;
                _isMachine = isMachine;
            }

            private IEnumerable<string> Candidates(string key)
            {
                yield return key; // raw
                if (!key.Contains("\\"))
                    yield break; // minimal sanity
                if (_isMachine)
                {
                    foreach (var p in MachinePrefixes)
                        yield return p + key;
                }
                else
                {
                    foreach (var p in UserPrefixes)
                        yield return p + key;
                }
            }

            public bool ContainsValue(string Key, string Value)
            {
                foreach (var k in Candidates(Key))
                    if (_pol.ContainsValue(k, Value))
                        return true;
                return false;
            }

            public object? GetValue(string Key, string Value)
            {
                foreach (var k in Candidates(Key))
                    if (_pol.ContainsValue(k, Value))
                        return _pol.GetValue(k, Value);
                return null;
            }

            public bool WillDeleteValue(string Key, string Value)
            {
                foreach (var k in Candidates(Key))
                    if (_pol.ContainsValue(k, Value))
                        return _pol.WillDeleteValue(k, Value);
                return false;
            }

            public List<string> GetValueNames(string Key)
            {
                foreach (var k in Candidates(Key))
                {
                    var names = _pol.GetValueNames(k);
                    if (names.Count > 0)
                        return names;
                }
                return new List<string>();
            }

            // Mutation operations not needed here.
            public void SetValue(
                string Key,
                string Value,
                object Data,
                Microsoft.Win32.RegistryValueKind DataType
            ) { }

            public void ForgetValue(string Key, string Value) { }

            public void DeleteValue(string Key, string Value) { }

            public void ClearKey(string Key) { }

            public void ForgetKeyClearance(string Key) { }
        }
    }
}
