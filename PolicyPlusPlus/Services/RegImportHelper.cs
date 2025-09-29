using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.IO;

namespace PolicyPlusPlus.Services
{
    internal static class RegImportHelper
    {
        public static (RegFile userReg, RegFile machineReg) SplitByHive(RegFile source)
        {
            var user = new RegFile();
            user.SetPrefix(string.Empty);
            var machine = new RegFile();
            machine.SetPrefix(string.Empty);

            foreach (var key in source.Keys)
            {
                var name = key.Name ?? string.Empty;
                if (StartsWith(name, "HKEY_LOCAL_MACHINE\\") || StartsWith(name, "HKLM\\"))
                {
                    machine.Keys.Add(CloneKey(key));
                }
                else if (
                    StartsWith(name, "HKEY_CURRENT_USER\\")
                    || StartsWith(name, "HKCU\\")
                    || StartsWith(name, "HKEY_USERS\\")
                    || StartsWith(name, "HKU\\")
                )
                {
                    user.Keys.Add(CloneKey(key));
                }
                else
                {
                    // Unknown root: ignore for safety
                }
            }
            return (user, machine);
        }

        public static (PolFile userPol, PolFile machinePol) ToPolByHive(RegFile source)
        {
            var (userReg, machineReg) = SplitByHive(source);
            var userPol = new PolFile();
            var machinePol = new PolFile();
            try
            {
                if (userReg.Keys.Count > 0)
                    userReg.Apply(userPol);
            }
            catch (Exception ex)
            {
                // Partial failure applying user hive; proceeding with whatever succeeded.
                Logging.Log.Warn(
                    "RegImport",
                    "Apply user hive failed type="
                        + ex.GetType().Name
                        + " msg="
                        + ex.Message
                        + " keys="
                        + userReg.Keys.Count
                );
            }
            try
            {
                if (machineReg.Keys.Count > 0)
                    machineReg.Apply(machinePol);
            }
            catch (Exception ex)
            {
                // Partial failure applying machine hive; continuing.
                Logging.Log.Warn(
                    "RegImport",
                    "Apply machine hive failed type="
                        + ex.GetType().Name
                        + " msg="
                        + ex.Message
                        + " keys="
                        + machineReg.Keys.Count
                );
            }
            return (userPol, machinePol);
        }

        // Removes all keys that are not under well-known policy root paths.
        public static void FilterToPolicyKeysInPlace(RegFile source)
        {
            if (source == null)
                return;
            try
            {
                var policyRoots = PolicyPlusCore
                    .IO.RegistryPolicyProxy.PolicyKeys.Select(k => k.ToLowerInvariant())
                    .ToArray();

                bool IsPolicyKey(string fullName)
                {
                    if (string.IsNullOrEmpty(fullName))
                        return false;
                    var lower = fullName.ToLowerInvariant();
                    lower = PolicyPlusCore.Utilities.RegistryHiveNormalization.StripHive(lower); // Remove hive for comparison
                    foreach (var r in policyRoots)
                    {
                        if (lower.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                        {
                            // Ensure boundary (exact match or next char is '\\')
                            if (lower.Length == r.Length || lower[r.Length] == '\\')
                                return true;
                        }
                    }
                    return false;
                }

                var filtered = new List<RegFile.RegFileKey>();
                foreach (var k in source.Keys)
                {
                    if (k != null && IsPolicyKey(k.Name ?? string.Empty))
                        filtered.Add(k);
                }
                source.Keys = filtered;
            }
            catch (Exception ex)
            {
                // Non-fatal filter failure; upstream caller will decide how to proceed.
                Logging.Log.Warn(
                    "RegImport",
                    "FilterToPolicyKeysInPlace failed type="
                        + ex.GetType().Name
                        + " msg="
                        + ex.Message
                );
            }
        }

        private static bool StartsWith(string text, string prefix) =>
            text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        private static RegFile.RegFileKey CloneKey(RegFile.RegFileKey src)
        {
            var k = new RegFile.RegFileKey { Name = src.Name, IsDeleter = src.IsDeleter };
            foreach (var v in src.Values)
            {
                k.Values.Add(
                    new RegFile.RegFileValue
                    {
                        Name = v.Name,
                        Data = v.Data,
                        Kind = v.Kind,
                        IsDeleter = v.IsDeleter,
                    }
                );
            }
            return k;
        }

        public static RegFile Clone(RegFile src)
        {
            var clone = new RegFile();
            clone.SetPrefix(string.Empty);
            foreach (var key in src.Keys)
            {
                clone.Keys.Add(CloneKey(key));
            }
            return clone;
        }
    }
}
