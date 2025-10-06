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
        public static int QueueFromReg(RegFile reg, AdmxBundle bundle) =>
            QueueFromReg(reg, bundle, includeClearsForMissing: false);

        // includeClearsForMissing: when true, policies currently configured but absent from imported .reg are queued as Clear (NotConfigured) operations.
        public static int QueueFromReg(RegFile reg, AdmxBundle bundle, bool includeClearsForMissing)
        {
            // Use shared synchronization root with PolicySourceManager to keep source snapshots stable during diff.
            lock (PolicySourceManager.SourcesSync)
            {
                if (reg == null || bundle == null)
                    return 0;

                // Build POL views split by hive so we respect Machine/User scope.
                var (userPolFile, machinePolFile) = RegImportHelper.ToPolByHive(reg);
                int total = 0;

                var compSrcRef = PolicySourceManager.Instance.CompSource;
                var userSrcRef = PolicySourceManager.Instance.UserSource;

                if (compSrcRef is IPolicySource compSrc)
                {
                    total += QueueDiff(
                        machinePolFile,
                        compSrc,
                        bundle,
                        scope: "Computer",
                        includeClearsForMissing
                    );
                }
                if (userSrcRef is IPolicySource userSrc)
                {
                    total += QueueDiff(
                        userPolFile,
                        userSrc,
                        bundle,
                        scope: "User",
                        includeClearsForMissing
                    );
                }
                return total;
            }
        }

        private static int QueueDiff(
            PolFile imported,
            IPolicySource current,
            AdmxBundle bundle,
            string scope,
            bool includeClearsForMissing
        )
        {
            if (imported == null || current == null || bundle?.Policies == null)
                return 0;

            int queued = 0;
            int policyErrors = 0;

            // Adapter allows hive-less lookups to still succeed.
            var importedView =
                new PolicyPlusCore.Utilities.RegistryHiveNormalization.HiveFlexiblePolicySource(
                    imported,
                    scope.Equals("Computer", StringComparison.OrdinalIgnoreCase)
                );

            foreach (var policy in bundle.Policies.Values)
            {
                try
                {
                    // Scope gating early.
                    var section = policy.RawPolicy?.Section ?? AdmxPolicySection.Machine;
                    if (
                        section == AdmxPolicySection.Machine
                        && !scope.Equals("Computer", StringComparison.OrdinalIgnoreCase)
                    )
                        continue;
                    if (
                        section == AdmxPolicySection.User
                        && !scope.Equals("User", StringComparison.OrdinalIgnoreCase)
                    )
                        continue;

                    var newState = PolicyProcessing.GetPolicyState(importedView, policy);
                    var currentState = PolicyProcessing.GetPolicyState(current, policy);

                    // Heuristic: Some simple toggle ADMX entries rely on synthetic on=1/off=delete inference.
                    // When a .reg import provides an explicit numeric 0 (or 1) value for such a policy the core
                    // evaluation can return NotConfigured (because OffValue is modeled as deletion). That leads
                    // Replace mode to incorrectly queue Clear instead of Disable. Map numeric root values back
                    // to Enabled/Disabled only when the policy defines neither explicit OnValue nor OffValue.
                    if (newState == PolicyState.NotConfigured)
                    {
                        try
                        {
                            var raw = policy.RawPolicy;
                            if (
                                raw != null
                                && raw.AffectedValues != null
                                && raw.AffectedValues.OnValue == null
                                && raw.AffectedValues.OffValue == null
                            )
                            {
                                if (
                                    !string.IsNullOrEmpty(raw.RegistryKey)
                                    && !string.IsNullOrEmpty(raw.RegistryValue)
                                )
                                {
                                    // Ensure the value name truly exists in the imported reg snapshot (avoid default numeric fallbacks).
                                    var importedNames = importedView.GetValueNames(raw.RegistryKey);
                                    bool namePresent =
                                        importedNames != null
                                        && importedNames.Any(n =>
                                            string.Equals(
                                                n,
                                                raw.RegistryValue,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        );
                                    if (
                                        namePresent
                                        && importedView.ContainsValue(
                                            raw.RegistryKey,
                                            raw.RegistryValue
                                        )
                                    )
                                    {
                                        var rootVal = importedView.GetValue(
                                            raw.RegistryKey,
                                            raw.RegistryValue
                                        );
                                        if (rootVal != null && IsNumeric(rootVal))
                                        {
                                            try
                                            {
                                                long num = Convert.ToInt64(rootVal);
                                                if (num == 0)
                                                    newState = PolicyState.Disabled; // numeric 0 explicitly represents Disabled
                                                else if (num == 1)
                                                    newState = PolicyState.Enabled; // numeric 1 explicitly represents Enabled
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    // After heuristic mapping: if replace mode and imported is NotConfigured while current configured, queue Clear.
                    if (
                        includeClearsForMissing
                        && newState == PolicyState.NotConfigured
                        && (
                            HasPolicyFootprint(current, policy)
                            || PolicyPlusCore.Core.ConfiguredPolicyTracker.WasEverConfigured(
                                policy.UniqueID,
                                scope
                            )
                        )
                    )
                    {
                        if (
                            !PendingChangesService.Instance.Pending.Any(p =>
                                string.Equals(
                                    p.PolicyId,
                                    policy.UniqueID,
                                    StringComparison.OrdinalIgnoreCase
                                )
                                && string.Equals(p.Scope, scope, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        {
                            PendingChangesService.Instance.Add(
                                new PendingChange
                                {
                                    PolicyId = policy.UniqueID,
                                    PolicyName = policy.DisplayName ?? policy.UniqueID,
                                    Scope = scope,
                                    Action = "Clear",
                                    Details = "Clear",
                                    DetailsFull = "Clear",
                                    DesiredState = PolicyState.NotConfigured,
                                }
                            );
                            queued++;
                        }
                        continue;
                    }

                    if (newState == PolicyState.NotConfigured)
                        continue; // merge mode or replace with both sides NotConfigured

                    // Heuristic: list policies may appear NotConfigured in currentState if only list prefix values exist (no root value); treat footprint as Enabled to suppress false diffs.
                    if (
                        currentState == PolicyState.NotConfigured
                        && newState == PolicyState.Enabled
                    )
                    {
                        try
                        {
                            if (
                                policy.RawPolicy?.Elements != null
                                && policy.RawPolicy.Elements.Any(e =>
                                    string.Equals(
                                        e.ElementType,
                                        "list",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            )
                            {
                                if (HasPolicyFootprint(current, policy))
                                    currentState = PolicyState.Enabled;
                            }
                        }
                        catch { }
                    }

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
                        continue;

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
                    if (policyErrors <= 5)
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

            // Final sweep: ensure any configured policies missing from import are cleared when includeClearsForMissing is true.
            if (includeClearsForMissing)
            {
                foreach (var policy in bundle.Policies.Values)
                {
                    try
                    {
                        var section = policy.RawPolicy?.Section ?? AdmxPolicySection.Machine;
                        if (
                            section == AdmxPolicySection.Machine
                            && !scope.Equals("Computer", StringComparison.OrdinalIgnoreCase)
                        )
                            continue;
                        if (
                            section == AdmxPolicySection.User
                            && !scope.Equals("User", StringComparison.OrdinalIgnoreCase)
                        )
                            continue;
                        bool alreadyQueued = PendingChangesService.Instance.Pending.Any(p =>
                            string.Equals(
                                p.PolicyId,
                                policy.UniqueID,
                                StringComparison.OrdinalIgnoreCase
                            ) && string.Equals(p.Scope, scope, StringComparison.OrdinalIgnoreCase)
                        );
                        if (alreadyQueued)
                            continue;
                        var curState = PolicyProcessing.GetPolicyState(current, policy);
                        bool hadFootprint =
                            curState != PolicyState.NotConfigured
                            || HasPolicyFootprint(current, policy)
                            || PolicyPlusCore.Core.ConfiguredPolicyTracker.WasEverConfigured(
                                policy.UniqueID,
                                scope
                            );
                        if (!hadFootprint)
                            continue;
                        var importedState = PolicyProcessing.GetPolicyState(importedView, policy);
                        if (importedState != PolicyState.NotConfigured)
                            continue;
                        PendingChangesService.Instance.Add(
                            new PendingChange
                            {
                                PolicyId = policy.UniqueID,
                                PolicyName = policy.DisplayName ?? policy.UniqueID,
                                Scope = scope,
                                Action = "Clear",
                                Details = "Clear",
                                DetailsFull = "Clear",
                                DesiredState = PolicyState.NotConfigured,
                            }
                        );
                        queued++;
                    }
                    catch (Exception ex)
                    {
                        policyErrors++;
                        if (policyErrors <= 5)
                        {
                            Logging.Log.Warn(
                                "RegImportDiff",
                                "FinalSweep failure scope="
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
                return false; // Disabled/NotConfigured equality handled by state compare
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
            var na = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var nb = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in a!)
                na[kv.Key] = kv.Value;
            foreach (var kv in b!)
                nb[kv.Key] = kv.Value;
            if (na.Count != nb.Count)
                return false;
            foreach (var kv in na)
            {
                if (!nb.TryGetValue(kv.Key, out var other))
                    return false;
                if (!ValueEquals(NormalizeOpt(kv.Value), NormalizeOpt(other)))
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
            if (x is List<string> lxs)
                x = lxs.ToArray();
            if (y is List<string> lys)
                y = lys.ToArray();
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
            if (
                x is System.Collections.IEnumerable enx
                && y is System.Collections.IEnumerable eny
                && x is not string
                && y is not string
            )
            {
                var left = new List<object?>();
                foreach (var o in enx)
                    left.Add(o);
                var right = new List<object?>();
                foreach (var o in eny)
                    right.Add(o);
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

        private static object? NormalizeOpt(object? v)
        {
            if (v == null)
                return null;
            if (v is string s)
                return s.Trim();
            if (v is Array arr)
            {
                // Preserve original ordering for multi-text arrays so that reordering is treated as a meaningful diff.
                var ordered = new object?[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                    ordered[i] = NormalizeOpt(arr.GetValue(i));
                return ordered;
            }
            if (v is List<string> ls)
                return ls.Select(e => (object?)NormalizeOpt(e)).ToArray();
            if (v is IEnumerable<string> es && v.GetType().Name != "String")
                return es.Select(e => (object?)NormalizeOpt(e)).ToArray();
            if (v is System.Collections.IDictionary dict)
            {
                var kvs = new List<string>();
                foreach (var key in dict.Keys)
                {
                    var val = dict[key];
                    kvs.Add(Convert.ToString(key) + "=" + Convert.ToString(val));
                }
                kvs.Sort(StringComparer.OrdinalIgnoreCase);
                return kvs.ToArray();
            }
            return v;
        }

        // Heuristic: determine if a policy left any registry footprint (value present OR list prefix remnants) even if GetPolicyState evaluates NotConfigured (e.g., Disabled-as-deletion case).
        private static bool HasPolicyFootprint(IPolicySource src, PolicyPlusPolicy policy)
        {
            try
            {
                var raw = policy.RawPolicy;
                if (raw == null)
                    return false;
                // Root value present
                if (
                    !string.IsNullOrEmpty(raw.RegistryKey)
                    && !string.IsNullOrEmpty(raw.RegistryValue)
                )
                {
                    if (src.ContainsValue(raw.RegistryKey, raw.RegistryValue))
                        return true;
                }
                if (raw.Elements != null)
                {
                    foreach (var el in raw.Elements)
                    {
                        string key = string.IsNullOrEmpty(el.RegistryKey)
                            ? raw.RegistryKey
                            : el.RegistryKey;
                        if (string.IsNullOrEmpty(key))
                            continue;
                        if (el.ElementType == "list")
                        {
                            var names = src.GetValueNames(key);
                            if (names.Count > 0)
                                return true;
                        }
                        else if (!string.IsNullOrEmpty(el.RegistryValue))
                        {
                            if (src.ContainsValue(key, el.RegistryValue))
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
