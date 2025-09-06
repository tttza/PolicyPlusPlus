using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.Admx;
using PolicyPlus.UI.PolicyDetail;
using Xunit;
using PolicyPlusModTests.TestHelpers; // PolAssert

namespace PolicyPlusModTests
{
    /// <summary>
    /// Tests to define desired behavior for upcoming improvements:
    ///  - Enum: index -> underlying numeric value mapping (non-sequential)
    ///  - Checkbox: defaultChecked applied when enabling if user made no change
    ///  - Decimal/Text: presentation defaultValue applied on enable with no user input
    /// These may FAIL initially; they are specification tests to drive implementation.
    /// </summary>
    public class PresentationAndEnumDefaultsTests
    {
        private static AdmxFile DummyAdmx() => new AdmxFile { SourceFile = "dummy.admx" };

        private static PolicyPlusPolicy BuildPolicy(AdmxPolicy raw, string idSuffix, string displayName)
        {
            return new PolicyPlusPolicy
            {
                RawPolicy = raw,
                UniqueID = $"MACHINE:{idSuffix}",
                DisplayName = displayName
            };
        }

        [Fact(DisplayName = "Enum element with non-sequential values writes underlying numeric value (index->value mapping)")]
        public void EnumElement_NonSequential_WritesUnderlyingValue()
        {
            // Arrange: Enum with values 10,20,30; selecting index 1 should persist 20.
            var enumElem = new EnumPolicyElement
            {
                ID = "EnumElem",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumValue"
            };
            enumElem.Items.Add(new EnumPolicyElementItem { DisplayCode = "$(string.Item1)", Value = new PolicyRegistryValue { RegistryType = PolicyRegistryValueType.Numeric, NumberValue = 10 } });
            enumElem.Items.Add(new EnumPolicyElementItem { DisplayCode = "$(string.Item2)", Value = new PolicyRegistryValue { RegistryType = PolicyRegistryValueType.Numeric, NumberValue = 20 } });
            enumElem.Items.Add(new EnumPolicyElementItem { DisplayCode = "$(string.Item3)", Value = new PolicyRegistryValue { RegistryType = PolicyRegistryValueType.Numeric, NumberValue = 30 } });

            var raw = new AdmxPolicy
            {
                RegistryKey = enumElem.RegistryKey,
                RegistryValue = enumElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { enumElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx()
            };
            var policy = BuildPolicy(raw, "EnumNonSeq", "Enum NonSeq");
            var polFile = new PolFile();

            // Act: pass enum index (1) as option value (current UI behavior). Desired: underlying numeric 20 written.
            PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "EnumElem", 1 } });

            // Assert (spec): DWORD 20 should be stored.
            PolAssert.HasDwordValue(polFile, raw.RegistryKey, raw.RegistryValue, 20u);
        }

        [Fact(DisplayName = "CheckBox defaultChecked=true is applied when enabling with no explicit option value")]
        public void CheckBox_DefaultChecked_AppliedOnEnable()
        {
            var boolElem = new BooleanPolicyElement
            {
                ID = "BoolElem",
                ElementType = "boolean",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "BoolValue"
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = boolElem.RegistryKey,
                RegistryValue = boolElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { boolElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx()
            };
            var policy = BuildPolicy(raw, "BoolDefault", "Bool Default");
            policy.Presentation = new Presentation
            {
                Elements = new List<PresentationElement>
                {
                    new CheckBoxPresentationElement
                    {
                        ID = "BoolElem",
                        ElementType = "checkBox",
                        DefaultState = true,
                        Text = "Test Bool"
                    }
                }
            };

            var polFile = new PolFile();
            var edit = new EditSettingTestable();
            edit.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            // Reset to Not Configured (SetTestContext forces Enabled) then re-enable to simulate user action
            edit.NotConfiguredOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.EnabledOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.ApplyToPolicySource_PublicForTest();

            // Expect registry value representing TRUE (DWORD 1 typically)
            Assert.True(polFile.ContainsValue(raw.RegistryKey, raw.RegistryValue));
            var stored = polFile.GetValue(raw.RegistryKey, raw.RegistryValue);
            Assert.True(object.Equals(stored, 1u) || object.Equals(stored, "1"), $"Unexpected stored bool value: {stored}");
        }

        [Fact(DisplayName = "Decimal presentation defaultValue is applied when no user input provided")]
        public void Decimal_DefaultValue_Applied()
        {
            var decElem = new DecimalPolicyElement
            {
                ID = "DecElem",
                ElementType = "decimal",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecValue",
                Minimum = 1,
                Maximum = 100,
                StoreAsText = false
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = decElem.RegistryKey,
                RegistryValue = decElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { decElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx()
            };
            var policy = BuildPolicy(raw, "DecDefault", "Dec Default");
            policy.Presentation = new Presentation
            {
                Elements = new List<PresentationElement>
                {
                    new NumericBoxPresentationElement
                    {
                        ID = "DecElem",
                        ElementType = "decimalTextBox",
                        DefaultValue = 42,
                        HasSpinner = true,
                        SpinnerIncrement = 5,
                        Label = "Dec Label"
                    }
                }
            };

            var polFile = new PolFile();
            var edit = new EditSettingTestable();
            edit.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            edit.NotConfiguredOption.Checked = true; edit.InvokeStateRadiosChanged();
            edit.EnabledOption.Checked = true; edit.InvokeStateRadiosChanged();
            edit.ApplyToPolicySource_PublicForTest();

            PolAssert.HasDwordValue(polFile, raw.RegistryKey, raw.RegistryValue, 42u);
        }

        [Fact(DisplayName = "TextBox presentation defaultValue is applied when no user input provided")]
        public void Text_DefaultValue_Applied()
        {
            var textElem = new TextPolicyElement
            {
                ID = "TxtElem",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "TxtValue",
                MaxLength = 200
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = textElem.RegistryKey,
                RegistryValue = textElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx()
            };
            var policy = BuildPolicy(raw, "TxtDefault", "Txt Default");
            policy.Presentation = new Presentation
            {
                Elements = new List<PresentationElement>
                {
                    new TextBoxPresentationElement
                    {
                        ID = "TxtElem",
                        ElementType = "textBox",
                        DefaultValue = "PresetValue",
                        Label = "Text Label"
                    }
                }
            };

            var polFile = new PolFile();
            var edit = new EditSettingTestable();
            edit.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            edit.NotConfiguredOption.Checked = true; edit.InvokeStateRadiosChanged();
            edit.EnabledOption.Checked = true; edit.InvokeStateRadiosChanged();
            edit.ApplyToPolicySource_PublicForTest();

            PolAssert.HasStringValue(polFile, raw.RegistryKey, raw.RegistryValue, "PresetValue");
        }
    }
}
