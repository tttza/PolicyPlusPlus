using System.Diagnostics;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Policies;

/// <summary>
/// Evaluates the current state of a policy by examining registry values.
/// Uses evidence scoring when explicit On/Off values are not decisive.
/// </summary>
internal sealed class PolicyStateEvaluator
{
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
            Debug.WriteLine($"[Core:{area}] ERROR {message} :: {ex.GetType().Name} {ex.Message}");
        }
        catch { }
    }

    /// <summary>
    /// Evaluates the state of a policy based on current registry values.
    /// </summary>
    /// <param name="policySource">The policy source containing registry values.</param>
    /// <param name="policy">The policy to evaluate.</param>
    /// <returns>The detected policy state (Enabled, Disabled, NotConfigured, or Unknown).</returns>
    public static PolicyState Evaluate(IPolicySource policySource, PolicyPlusPolicy? policy)
    {
        if (policy == null)
            return PolicyState.NotConfigured;

        var rawpolTemp = policy.RawPolicy;
        if (rawpolTemp == null)
            return PolicyState.NotConfigured;

        var rawpol = rawpolTemp;
        if (rawpol.AffectedValues == null)
            rawpol.AffectedValues = new PolicyRegistryList();

        var cached = new CachedPolicySource(policySource);

        // Phase 1: Check explicit On/Off matches
        var (explicitPos, explicitNeg, rootOnMatched, rootOffMatched) = EvaluateExplicitMatches(
            cached,
            rawpol
        );

        if (explicitPos > 0 && explicitNeg == 0)
            return PolicyState.Enabled;
        if (explicitNeg > 0 && explicitPos == 0)
            return PolicyState.Disabled;

        // Phase 2: Evidence-based scoring
        var (enabledEvidence, disabledEvidence) = EvaluateEvidence(cached, rawpol);

        if (enabledEvidence > disabledEvidence)
            return PolicyState.Enabled;
        if (disabledEvidence > enabledEvidence)
            return PolicyState.Disabled;
        if (enabledEvidence == 0m)
            return PolicyState.NotConfigured;

        return PolicyState.Unknown;
    }

    private static (
        int explicitPos,
        int explicitNeg,
        bool rootOnMatched,
        bool rootOffMatched
    ) EvaluateExplicitMatches(CachedPolicySource cached, AdmxPolicy rawpol)
    {
        int explicitPos = 0;
        int explicitNeg = 0;
        bool rootOnMatched = false;
        bool rootOffMatched = false;

        // Check root registry value explicit matches
        if (
            !string.IsNullOrEmpty(rawpol.RegistryKey) && !string.IsNullOrEmpty(rawpol.RegistryValue)
        )
        {
            try
            {
                if (
                    rawpol.AffectedValues.OnValue is object
                    && ValuePresent(
                        rawpol.AffectedValues.OnValue,
                        cached,
                        rawpol.RegistryKey,
                        rawpol.RegistryValue
                    )
                )
                {
                    rootOnMatched = true;
                }
                else if (
                    rawpol.AffectedValues.OnValueList is object
                    && ValueListPresent(
                        rawpol.AffectedValues.OnValueList,
                        cached,
                        rawpol.RegistryKey,
                        rawpol.RegistryValue
                    )
                )
                {
                    rootOnMatched = true;
                }

                if (
                    rawpol.AffectedValues.OffValue is object
                    && ValuePresent(
                        rawpol.AffectedValues.OffValue,
                        cached,
                        rawpol.RegistryKey,
                        rawpol.RegistryValue
                    )
                )
                {
                    rootOffMatched = true;
                }
                else if (
                    rawpol.AffectedValues.OffValueList is object
                    && ValueListPresent(
                        rawpol.AffectedValues.OffValueList,
                        cached,
                        rawpol.RegistryKey,
                        rawpol.RegistryValue
                    )
                )
                {
                    rootOffMatched = true;
                }
            }
            catch (Exception ex)
            {
                LogError("PolicyStateEvaluator", "explicit root match eval failed", ex);
            }
        }

        if (rootOnMatched)
            explicitPos++;
        if (rootOffMatched)
            explicitNeg++;

        // Check element explicit matches
        if (rawpol.Elements is object)
        {
            foreach (var elem in rawpol.Elements)
            {
                try
                {
                    var (elemPos, elemNeg) = EvaluateElementExplicitMatch(cached, rawpol, elem);
                    explicitPos += elemPos;
                    explicitNeg += elemNeg;
                }
                catch (Exception ex)
                {
                    LogError(
                        "PolicyStateEvaluator",
                        $"element explicit eval failed elemId={elem.ID}",
                        ex
                    );
                }
            }
        }

        return (explicitPos, explicitNeg, rootOnMatched, rootOffMatched);
    }

    private static (int pos, int neg) EvaluateElementExplicitMatch(
        CachedPolicySource cached,
        AdmxPolicy rawpol,
        PolicyElement elem
    )
    {
        int pos = 0;
        int neg = 0;

        string elemKey = string.IsNullOrEmpty(elem.RegistryKey)
            ? rawpol.RegistryKey
            : elem.RegistryKey;

        if (elem.ElementType == "boolean")
        {
            var be = (BooleanPolicyElement)elem;
            bool onMatch = false;
            bool offMatch = false;

            if (
                be.AffectedRegistry.OnValue is object
                && ValuePresent(be.AffectedRegistry.OnValue, cached, elemKey, elem.RegistryValue)
            )
            {
                onMatch = true;
            }
            else if (
                be.AffectedRegistry.OnValueList is object
                && ValueListPresent(
                    be.AffectedRegistry.OnValueList,
                    cached,
                    elemKey,
                    elem.RegistryValue
                )
            )
            {
                onMatch = true;
            }

            if (
                be.AffectedRegistry.OffValue is object
                && ValuePresent(be.AffectedRegistry.OffValue, cached, elemKey, elem.RegistryValue)
            )
            {
                offMatch = true;
            }
            else if (
                be.AffectedRegistry.OffValueList is object
                && ValueListPresent(
                    be.AffectedRegistry.OffValueList,
                    cached,
                    elemKey,
                    elem.RegistryValue
                )
            )
            {
                offMatch = true;
            }

            if (onMatch)
                pos++;
            if (offMatch)
                neg++;
        }
        else if (elem.ElementType == "enum")
        {
            var ee = (EnumPolicyElement)elem;
            foreach (var item in ee.Items)
            {
                if (ValuePresent(item.Value, cached, elemKey, elem.RegistryValue))
                {
                    if (
                        item.ValueList is null
                        || ValueListPresent(item.ValueList, cached, elemKey, elem.RegistryValue)
                    )
                    {
                        pos++;
                        break;
                    }
                }
            }
        }

        return (pos, neg);
    }

    private static (decimal enabledEvidence, decimal disabledEvidence) EvaluateEvidence(
        CachedPolicySource cached,
        AdmxPolicy rawpol
    )
    {
        decimal enabledEvidence = 0m;
        decimal disabledEvidence = 0m;
        bool hasRegistryValue = false;

        if (rawpol.AffectedValues == null)
            rawpol.AffectedValues = new PolicyRegistryList();

        void checkOneVal(
            PolicyRegistryValue? Value,
            string Key,
            string ValueName,
            ref decimal EvidenceVar
        )
        {
            if (Value is null)
                return;
            try
            {
                if (ValuePresent(Value, cached, Key, ValueName))
                    EvidenceVar += 1m;
            }
            catch (Exception ex)
            {
                LogError(
                    "PolicyStateEvaluator",
                    $"checkOneVal failed key={Key} val={ValueName}",
                    ex
                );
            }
        }

        void checkValList(
            PolicyRegistrySingleList? ValList,
            string DefaultKey,
            ref decimal EvidenceVar
        )
        {
            if (ValList is null)
                return;
            try
            {
                string listKey = string.IsNullOrEmpty(ValList.DefaultRegistryKey)
                    ? DefaultKey
                    : ValList.DefaultRegistryKey;
                foreach (var regVal in ValList.AffectedValues)
                {
                    string entryKey = string.IsNullOrEmpty(regVal.RegistryKey)
                        ? listKey
                        : regVal.RegistryKey;
                    checkOneVal(regVal.Value, entryKey, regVal.RegistryValue, ref EvidenceVar);
                }
            }
            catch (Exception ex)
            {
                LogError("PolicyStateEvaluator", "checkValList failed", ex);
            }
        }

        // Check root registry value evidence
        if (!string.IsNullOrEmpty(rawpol.RegistryValue))
        {
            if (rawpol.AffectedValues.OnValue is null)
            {
                hasRegistryValue = true;
                checkOneVal(
                    new PolicyRegistryValue()
                    {
                        NumberValue = 1U,
                        RegistryType = PolicyRegistryValueType.Numeric,
                    },
                    rawpol.RegistryKey,
                    rawpol.RegistryValue,
                    ref enabledEvidence
                );
            }
            else
            {
                checkOneVal(
                    rawpol.AffectedValues.OnValue,
                    rawpol.RegistryKey,
                    rawpol.RegistryValue,
                    ref enabledEvidence
                );
            }

            if (rawpol.AffectedValues.OffValue is null)
            {
                hasRegistryValue = true;
                checkOneVal(
                    new PolicyRegistryValue() { RegistryType = PolicyRegistryValueType.Delete },
                    rawpol.RegistryKey,
                    rawpol.RegistryValue,
                    ref disabledEvidence
                );
            }
            else
            {
                checkOneVal(
                    rawpol.AffectedValues.OffValue,
                    rawpol.RegistryKey,
                    rawpol.RegistryValue,
                    ref disabledEvidence
                );
            }
        }

        checkValList(rawpol.AffectedValues.OnValueList, rawpol.RegistryKey, ref enabledEvidence);
        checkValList(rawpol.AffectedValues.OffValueList, rawpol.RegistryKey, ref disabledEvidence);

        // Check element evidence
        if (rawpol.Elements is object)
        {
            var (presentElements, deletedElements) = EvaluateElementEvidence(
                cached,
                rawpol,
                hasRegistryValue
            );

            if (presentElements > 0m)
            {
                enabledEvidence += presentElements;
            }
            else if (deletedElements > 0m)
            {
                disabledEvidence += deletedElements;
            }
        }

        return (enabledEvidence, disabledEvidence);
    }

    private static (decimal presentElements, decimal deletedElements) EvaluateElementEvidence(
        CachedPolicySource cached,
        AdmxPolicy rawpol,
        bool hasRegistryValue
    )
    {
        decimal deletedElements = 0m;
        decimal presentElements = 0m;

        foreach (var elem in rawpol.Elements!)
        {
            string elemKey = string.IsNullOrEmpty(elem.RegistryKey)
                ? rawpol.RegistryKey
                : elem.RegistryKey;

            try
            {
                if (elem.ElementType == "list")
                {
                    int neededValues = 0;
                    if (cached.WillDeleteValue(elemKey, ""))
                    {
                        deletedElements += 1m;
                        neededValues = 1;
                    }

                    if (cached.GetValueNames(elemKey).Count > 0)
                    {
                        deletedElements -= neededValues;
                        presentElements += 1m;
                    }
                }
                else if (elem.ElementType == "boolean")
                {
                    BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                    if (cached.WillDeleteValue(elemKey, elem.RegistryValue))
                    {
                        deletedElements += 1m;
                    }
                    else
                    {
                        decimal checkboxDisabled = 0m;
                        CheckOneValForEvidence(
                            booleanElem.AffectedRegistry.OffValue,
                            cached,
                            elemKey,
                            elem.RegistryValue,
                            ref checkboxDisabled
                        );
                        CheckValListForEvidence(
                            booleanElem.AffectedRegistry.OffValueList,
                            cached,
                            elemKey,
                            ref checkboxDisabled
                        );
                        deletedElements += checkboxDisabled * 0.1m;

                        CheckOneValForEvidence(
                            booleanElem.AffectedRegistry.OnValue,
                            cached,
                            elemKey,
                            elem.RegistryValue,
                            ref presentElements
                        );
                        CheckValListForEvidence(
                            booleanElem.AffectedRegistry.OnValueList,
                            cached,
                            elemKey,
                            ref presentElements
                        );
                    }
                }
                else if (cached.WillDeleteValue(elemKey, elem.RegistryValue))
                {
                    bool allow =
                        !hasRegistryValue
                        || (
                            rawpol.AffectedValues?.OnValue == null
                            && rawpol.AffectedValues?.OffValue == null
                        );
                    if (allow)
                        deletedElements += 1m;
                }
                else if (cached.ContainsValue(elemKey, elem.RegistryValue))
                {
                    bool allow =
                        !hasRegistryValue
                        || (
                            rawpol.AffectedValues?.OnValue == null
                            && rawpol.AffectedValues?.OffValue == null
                        );
                    if (allow)
                        presentElements += 1m;
                }
            }
            catch (Exception ex)
            {
                LogError(
                    "PolicyStateEvaluator",
                    $"element evidence eval failed elemId={elem.ID}",
                    ex
                );
            }
        }

        return (presentElements, deletedElements);
    }

    private static void CheckOneValForEvidence(
        PolicyRegistryValue? Value,
        CachedPolicySource cached,
        string Key,
        string ValueName,
        ref decimal EvidenceVar
    )
    {
        if (Value is null)
            return;
        try
        {
            if (ValuePresent(Value, cached, Key, ValueName))
                EvidenceVar += 1m;
        }
        catch (Exception ex)
        {
            LogError("PolicyStateEvaluator", $"checkOneVal failed key={Key} val={ValueName}", ex);
        }
    }

    private static void CheckValListForEvidence(
        PolicyRegistrySingleList? ValList,
        CachedPolicySource cached,
        string DefaultKey,
        ref decimal EvidenceVar
    )
    {
        if (ValList is null)
            return;
        try
        {
            string listKey = string.IsNullOrEmpty(ValList.DefaultRegistryKey)
                ? DefaultKey
                : ValList.DefaultRegistryKey;
            foreach (var regVal in ValList.AffectedValues)
            {
                string entryKey = string.IsNullOrEmpty(regVal.RegistryKey)
                    ? listKey
                    : regVal.RegistryKey;
                CheckOneValForEvidence(
                    regVal.Value,
                    cached,
                    entryKey,
                    regVal.RegistryValue,
                    ref EvidenceVar
                );
            }
        }
        catch (Exception ex)
        {
            LogError("PolicyStateEvaluator", "checkValList failed", ex);
        }
    }

    #region Value Matching Helpers

    internal static bool ValuePresent(
        PolicyRegistryValue? Value,
        IPolicySource Source,
        string Key,
        string ValueName
    )
    {
        if (Value == null)
            return false;
        switch (Value.RegistryType)
        {
            case PolicyRegistryValueType.Delete:
                return Source.WillDeleteValue(Key, ValueName);

            case PolicyRegistryValueType.Numeric:
                if (!Source.ContainsValue(Key, ValueName))
                    return false;
                var sourceVal = Source.GetValue(Key, ValueName);
                if (!(sourceVal is uint) && !(sourceVal is int))
                    return false;
                try
                {
                    long num = Convert.ToInt64(sourceVal);
                    return num == Value.NumberValue;
                }
                catch
                {
                    return false;
                }

            case PolicyRegistryValueType.Text:
                if (!Source.ContainsValue(Key, ValueName))
                    return false;
                var sourceValText = Source.GetValue(Key, ValueName);
                if (!(sourceValText is string))
                    return false;
                return (Convert.ToString(sourceValText) ?? "") == (Value.StringValue ?? "");

            default:
                throw new InvalidOperationException("Illegal value type");
        }
    }

    internal static bool ValueListPresent(
        PolicyRegistrySingleList ValueList,
        IPolicySource Source,
        string Key,
        string ValueName
    )
    {
        string sublistKey = string.IsNullOrEmpty(ValueList.DefaultRegistryKey)
            ? Key
            : ValueList.DefaultRegistryKey;
        return ValueList.AffectedValues.All(e =>
        {
            string entryKey = string.IsNullOrEmpty(e.RegistryKey) ? sublistKey : e.RegistryKey;
            return ValuePresent(e.Value, Source, entryKey, e.RegistryValue);
        });
    }

    #endregion

    #region Cached Policy Source

    /// <summary>
    /// Memoizing wrapper to reduce repeated lookups during a single evaluation.
    /// </summary>
    internal sealed class CachedPolicySource : IPolicySource
    {
        private readonly IPolicySource _inner;

        private readonly Dictionary<(string key, string value), bool> _contains = new();
        private readonly Dictionary<(string key, string value), bool> _willDelete = new();
        private readonly Dictionary<(string key, string value), object?> _values = new();
        private readonly Dictionary<string, List<string>> _valueNames = new(
            StringComparer.OrdinalIgnoreCase
        );

        public CachedPolicySource(IPolicySource inner)
        {
            _inner = inner;
        }

        private static (string, string) NKey(string k, string v) =>
            ((k ?? string.Empty).ToLowerInvariant(), (v ?? string.Empty).ToLowerInvariant());

        private static string NKeyOnly(string k) => k ?? string.Empty;

        public bool ContainsValue(string Key, string Value)
        {
            var tk = NKey(Key, Value);
            if (_contains.TryGetValue(tk, out var b))
                return b;
            b = _inner.ContainsValue(Key, Value);
            _contains[tk] = b;
            return b;
        }

        public object? GetValue(string Key, string Value)
        {
            var tk = NKey(Key, Value);
            if (_values.TryGetValue(tk, out var o))
                return o;
            o = _inner.GetValue(Key, Value);
            _values[tk] = o;
            return o;
        }

        public bool WillDeleteValue(string Key, string Value)
        {
            var tk = NKey(Key, Value);
            if (_willDelete.TryGetValue(tk, out var b))
                return b;
            b = _inner.WillDeleteValue(Key, Value);
            _willDelete[tk] = b;
            return b;
        }

        public List<string> GetValueNames(string Key)
        {
            var k = NKeyOnly(Key);
            if (_valueNames.TryGetValue(k, out var list))
                return list;
            list = _inner.GetValueNames(Key);
            _valueNames[k] = list;
            return list;
        }

        // Mutators forward to inner (not used during evaluation)
        public void SetValue(
            string Key,
            string Value,
            object Data,
            Microsoft.Win32.RegistryValueKind DataType
        ) => _inner.SetValue(Key, Value, Data, DataType);

        public void ForgetValue(string Key, string Value) => _inner.ForgetValue(Key, Value);

        public void DeleteValue(string Key, string Value) => _inner.DeleteValue(Key, Value);

        public void ClearKey(string Key) => _inner.ClearKey(Key);

        public void ForgetKeyClearance(string Key) => _inner.ForgetKeyClearance(Key);
    }

    #endregion
}
