using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;

namespace PolicyPlusPlus.Services
{
    // Builds pending changes by diffing current policy sources vs in-memory POLs produced from a .reg import.
    internal static class PolicyPolDiffImporter
    {
        public static int QueueFromReg(RegFile reg, AdmxBundle bundle)
        {
            if (reg == null || bundle == null)
                return 0;
            // Split hive early so we respect User / Computer scope.
            var (userPolFile, machinePolFile) = RegImportHelper.ToPolByHive(reg);
            // Additionally build hive-stripped variants to maximize match with ADMX keys (which store hive-less paths).
            var (userStripped, machineStripped) = BuildHiveStrippedPols(reg);
            int total = 0;
            if (PolicySourceManager.Instance.CompSource is IPolicySource compSrc)
            {
                // Prefer stripped if it contains entries; fallback to original.
                var imported = HasAnyEntries(machineStripped) ? machineStripped : machinePolFile;
                total += QueueDiff(imported, compSrc, bundle, "Computer");
            }
            if (PolicySourceManager.Instance.UserSource is IPolicySource userSrc)
            {
                var imported = HasAnyEntries(userStripped) ? userStripped : userPolFile;
                total += QueueDiff(imported, userSrc, bundle, "User");
            }
            return total;
        }

        private static bool HasAnyEntries(PolFile pol)
        {
            if (pol == null)
                return false;
            try
            {
                // Fast emptiness heuristic using only public APIs.
                // A POL has content if it defines at least one normal value at the root OR any child key segment.
                // GetValueNames("") excludes special "**" deletion markers (which we do not expect from stripped builds).
                if (pol.GetValueNames(string.Empty).Count > 0)
                    return true;
                if (pol.GetKeyNames(string.Empty).Count > 0)
                    return true;
                // Fallback: probe a representative policy root key that commonly appears; short-circuits if absent.
                // This avoids scanning all policies when file is genuinely empty (already O(n) earlier but cheap here).
                var commonRoots = new[] { "software", "system" };
                foreach (var root in commonRoots)
                {
                    if (pol.GetKeyNames(root).Count > 0 || pol.GetValueNames(root).Count > 0)
                        return true;
                }
                return false;
            }
            catch
            {
                // Defensive: if introspection fails treat as non-empty so caller will still attempt diff (safer than skipping).
                return true;
            }
        }

        private static (PolFile userNoHive, PolFile machineNoHive) BuildHiveStrippedPols(
            RegFile original
        )
        {
            var userReg = new RegFile();
            userReg.SetPrefix(string.Empty);
            var machineReg = new RegFile();
            machineReg.SetPrefix(string.Empty);
            foreach (var k in original.Keys)
            {
                if (k?.Name == null)
                    continue;
                var hive = DetectHive(k.Name);
                if (hive == HiveType.Machine)
                    machineReg.Keys.Add(CloneWithStrippedName(k));
                else if (hive == HiveType.User)
                    userReg.Keys.Add(CloneWithStrippedName(k));
            }
            var userPol = new PolFile();
            var machinePol = new PolFile();
            try
            {
                userReg.Apply(userPol);
            }
            catch { }
            try
            {
                machineReg.Apply(machinePol);
            }
            catch { }
            return (userPol, machinePol);
        }

        private enum HiveType
        {
            Unknown,
            Machine,
            User,
        }

        private static HiveType DetectHive(string key)
        {
            if (
                key.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase)
            )
                return HiveType.Machine;
            if (
                key.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("HKEY_USERS\\", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("HKU\\", StringComparison.OrdinalIgnoreCase)
            )
                return HiveType.User;
            return HiveType.Unknown;
        }

        private static RegFile.RegFileKey CloneWithStrippedName(RegFile.RegFileKey src)
        {
            var name = PolicyPlusCore.Utilities.RegistryHiveNormalization.StripHive(
                src.Name ?? string.Empty
            );
            var nk = new RegFile.RegFileKey { Name = name, IsDeleter = src.IsDeleter };
            foreach (var v in src.Values)
            {
                nk.Values.Add(
                    new RegFile.RegFileValue
                    {
                        Name = v.Name,
                        Data = v.Data,
                        Kind = v.Kind,
                        IsDeleter = v.IsDeleter,
                    }
                );
            }
            return nk;
        }

        private static int QueueDiff(
            PolFile imported,
            IPolicySource current,
            AdmxBundle bundle,
            string scope
        )
        {
            if (imported == null || current == null)
                return 0;
            int queued = 0;
            int policyErrors = 0; // count per-policy evaluation errors for diagnostics
            // Wrap imported PolFile with hive-normalizing read-only view so that policies defined without hive still match.
            var importedView =
                new PolicyPlusCore.Utilities.RegistryHiveNormalization.HiveFlexiblePolicySource(
                    imported,
                    scope.Equals("Computer", StringComparison.OrdinalIgnoreCase)
                );
            foreach (var policy in bundle.Policies.Values)
            {
                try
                {
                    // State in imported REG-derived view
                    var newState = PolicyProcessing.GetPolicyState(importedView, policy);
                    if (newState == PolicyState.NotConfigured)
                        continue; // Not present in imported data -> skip

                    // Current state from existing source for diff.
                    var currentState = PolicyProcessing.GetPolicyState(current, policy);

                    Dictionary<string, object>? newOpts = null;
                    Dictionary<string, object>? curOpts = null;
                    if (newState == PolicyState.Enabled)
                    {
                        try
                        {
                            newOpts = PolicyProcessing.GetPolicyOptionStates(importedView, policy);
                        }
                        catch { }
                        if (newOpts != null && newOpts.Count == 0)
                            newOpts = null;
                        if (currentState == PolicyState.Enabled)
                        {
                            try
                            {
                                curOpts = PolicyProcessing.GetPolicyOptionStates(current, policy);
                            }
                            catch { }
                            if (curOpts != null && curOpts.Count == 0)
                                curOpts = null;
                        }
                    }

                    if (!HasMeaningfulChange(currentState, newState, curOpts, newOpts))
                        continue; // No diff -> skip queue

                    var action = newState switch
                    {
                        PolicyState.Enabled => "Enable",
                        PolicyState.Disabled => "Disable",
                        _ => "Clear",
                    };
                    (string details, string detailsFull) = BuildDetails(
                        action,
                        newState,
                        newOpts,
                        policy
                    );
                    PendingChangesService.Instance.Add(
                        new PendingChange
                        {
                            PolicyId = policy.UniqueID,
                            PolicyName = policy.DisplayName ?? policy.UniqueID,
                            Scope = scope,
                            Action = action,
                            Details = details,
                            DetailsFull = detailsFull,
                            DesiredState = newState,
                            Options = newOpts,
                        }
                    );
                    queued++;
                }
                catch (Exception ex)
                {
                    policyErrors++;
                    if (policyErrors <= 5) // cap detailed logs to avoid spam on large failures
                    {
                        Logging.Log.Warn(
                            "RegImportDiff",
                            "Policy diff failure scope="
                                + scope
                                + " id="
                                + policy.UniqueID
                                + " type="
                                + ex.GetType().Name
                                + " msg="
                                + ex.Message
                        );
                    }
                }
            }
            if (policyErrors > 0)
            {
                Logging.Log.Info(
                    "RegImportDiff",
                    "Completed diff scope="
                        + scope
                        + " queued="
                        + queued
                        + " errors="
                        + policyErrors
                );
            }
            return queued;
        }

        private static bool HasMeaningfulChange(
            PolicyState currentState,
            PolicyState newState,
            Dictionary<string, object>? currentOptions,
            Dictionary<string, object>? newOptions
        )
        {
            if (currentState != newState)
                return true;
            if (newState != PolicyState.Enabled)
                return false; // Disabled/NotConfigured equality already handled above
            // Both Enabled -> compare options
            return !OptionsEqual(currentOptions, newOptions);
        }

        private static bool OptionsEqual(
            Dictionary<string, object>? a,
            Dictionary<string, object>? b
        )
        {
            static bool IsEmpty(Dictionary<string, object>? d) => d == null || d.Count == 0;
            if (IsEmpty(a) && IsEmpty(b))
                return true;
            if (IsEmpty(a) || IsEmpty(b))
                return false;
            if (a!.Count != b!.Count)
                return false; // Fast path – if counts differ treat as changed (some policies may allow missing optional keys; we consider that meaningful)
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out var other))
                    return false;
                if (!ValueEquals(kv.Value, other))
                    return false;
            }
            return true;
        }

        private static bool ValueEquals(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x == null || y == null)
                return false;
            if (x is string xs && y is string ys)
                return string.Equals(xs, ys, StringComparison.Ordinal);
            if (IsNumeric(x) && IsNumeric(y))
            {
                try
                {
                    return Convert.ToDecimal(x) == Convert.ToDecimal(y);
                }
                catch { }
            }
            if (x is Array ax && y is Array ay)
            {
                if (ax.Length != ay.Length)
                    return false;
                for (int i = 0; i < ax.Length; i++)
                    if (!ValueEquals(ax.GetValue(i), ay.GetValue(i)))
                        return false;
                return true;
            }
            if (x is IEnumerable<object> eo && y is IEnumerable<object> eo2)
            {
                // Fallback sequence comparison (rare path). Materialize small sets.
                var left = eo.ToList();
                var right = eo2.ToList();
                if (left.Count != right.Count)
                    return false;
                for (int i = 0; i < left.Count; i++)
                    if (!ValueEquals(left[i], right[i]))
                        return false;
                return true;
            }
            return Equals(x, y);
        }

        private static bool IsNumeric(object o) =>
            o is byte
            || o is sbyte
            || o is short
            || o is ushort
            || o is int
            || o is uint
            || o is long
            || o is ulong
            || o is float
            || o is double
            || o is decimal;

        private static (string shortText, string longText) BuildDetails(
            string action,
            PolicyState desired,
            Dictionary<string, object>? options,
            PolicyPlusPolicy policy
        )
        {
            try
            {
                if (desired != PolicyState.Enabled || options == null || options.Count == 0)
                    return (action, action);
                var shortSb = new System.Text.StringBuilder();
                shortSb.Append(action);
                var optionPairs = new List<string>();
                int shown = 0;
                foreach (var kv in options)
                {
                    if (shown >= 4)
                        break;
                    optionPairs.Add(kv.Key + "=" + FormatOpt(kv.Value));
                    shown++;
                }
                if (optionPairs.Count > 0)
                {
                    shortSb.Append(": ");
                    shortSb.Append(string.Join(", ", optionPairs));
                    if (options.Count > optionPairs.Count)
                        shortSb.Append($" (+{options.Count - optionPairs.Count} more)");
                }
                var longSb = new System.Text.StringBuilder();
                longSb.AppendLine(action);
                try
                {
                    longSb.AppendLine("Registry values:");
                    foreach (var kv in PolicyProcessing.GetReferencedRegistryValues(policy))
                        longSb.AppendLine(
                            "  ? "
                                + kv.Key
                                + (
                                    string.IsNullOrEmpty(kv.Value)
                                        ? string.Empty
                                        : " (" + kv.Value + ")"
                                )
                        );
                }
                catch (Exception ex)
                {
                    Logging.Log.Debug(
                        "RegImportDiff",
                        "GetReferencedRegistryValues failed id="
                            + policy.UniqueID
                            + " type="
                            + ex.GetType().Name
                            + " msg="
                            + ex.Message
                    );
                }
                longSb.AppendLine("Options:");
                foreach (var kv in options)
                    longSb.AppendLine("  - " + kv.Key + " = " + FormatOpt(kv.Value));
                return (shortSb.ToString(), longSb.ToString());
            }
            catch (Exception ex)
            {
                Logging.Log.Debug(
                    "RegImportDiff",
                    "BuildDetails failed id="
                        + policy.UniqueID
                        + " type="
                        + ex.GetType().Name
                        + " msg="
                        + ex.Message
                );
                return (action, action);
            }
        }

        private static string FormatOpt(object v)
        {
            if (v == null)
                return string.Empty;
            if (v is string s)
                return s;
            if (v is bool b)
                return b ? "true" : "false";
            if (v is Array arr)
            {
                var list = new List<string>();
                foreach (var o in arr)
                    list.Add(Convert.ToString(o) ?? string.Empty);
                return "[" + string.Join(",", list) + "]";
            }
            return Convert.ToString(v) ?? string.Empty;
        }
    }
}
