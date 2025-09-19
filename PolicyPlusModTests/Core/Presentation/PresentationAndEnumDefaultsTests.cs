using System.Collections.Generic;
using PolicyPlusCore.Admx; // ADMX models
using PolicyPlusCore.Core; // core models
using PolicyPlusModTests.TestHelpers; // PolAssert
using PolicyPlusModTests.Testing;
using Xunit;

namespace PolicyPlusModTests.Core.Presentation
{
    /// <summary>
    /// Tests for presentation-driven defaults and enum handling.
    /// Updated: Enum option state now returns selected index (not underlying numeric value).
    /// </summary>
    public class PresentationAndEnumDefaultsTests
    {
        private static AdmxFile DummyAdmx() => new AdmxFile { SourceFile = "dummy.admx" };

        private static PolicyPlusPolicy BuildPolicy(
            AdmxPolicy raw,
            string idSuffix,
            string displayName
        ) =>
            new PolicyPlusPolicy
            {
                RawPolicy = raw,
                UniqueID = $"MACHINE:{idSuffix}",
                DisplayName = displayName,
            };

        [Fact(DisplayName = "Enum element stores underlying numeric value but reports index state")]
        public void EnumElement_NonSequential_IndexReported_NumericStored()
        {
            // Arrange: Enum with values 10,20,30; selecting index 1 should persist 20, state reports 1.
            var enumElem = new EnumPolicyElement
            {
                ID = "EnumElem",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumValue",
            };
            enumElem.Items.Add(
                new EnumPolicyElementItem
                {
                    DisplayCode = "$(string.Item1)",
                    Value = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 10,
                    },
                }
            );
            enumElem.Items.Add(
                new EnumPolicyElementItem
                {
                    DisplayCode = "$(string.Item2)",
                    Value = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 20,
                    },
                }
            );
            enumElem.Items.Add(
                new EnumPolicyElementItem
                {
                    DisplayCode = "$(string.Item3)",
                    Value = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 30,
                    },
                }
            );

            var raw = new AdmxPolicy
            {
                RegistryKey = enumElem.RegistryKey,
                RegistryValue = enumElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { enumElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx(),
            };
            var policy = BuildPolicy(raw, "EnumNonSeq", "Enum NonSeq");
            var polFile = new PolFile();

            // Act
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                polFile,
                policy,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "EnumElem", 1 } }
            );

            // Assert registry underlying numeric value stored
            PolAssert.HasDwordValue(polFile, raw.RegistryKey, raw.RegistryValue, 20u);
            // Assert option state reports index
            var optStates = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyOptionStates(
                polFile,
                policy
            );
            Assert.Equal(1, (int)optStates["EnumElem"]);
        }

        [Fact(
            DisplayName = "CheckBox defaultChecked=true is applied when enabling with no explicit option value"
        )]
        public void CheckBox_DefaultChecked_AppliedOnEnable()
        {
            var boolElem = new BooleanPolicyElement
            {
                ID = "BoolElem",
                ElementType = "boolean",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "BoolValue",
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = boolElem.RegistryKey,
                RegistryValue = boolElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { boolElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx(),
            };
            var policy = BuildPolicy(raw, "BoolDefault", "Bool Default");
            policy.Presentation = new global::PolicyPlusCore.Core.Presentation
            {
                Elements = new List<PresentationElement>
                {
                    new CheckBoxPresentationElement
                    {
                        ID = "BoolElem",
                        ElementType = "checkBox",
                        DefaultState = true,
                        Text = "Test Bool",
                    },
                },
            };

            var polFile = new PolFile();
            var edit = new EditSettingTestable();
            edit.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            edit.NotConfiguredOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.EnabledOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.ApplyToPolicySource_PublicForTest();

            Assert.True(polFile.ContainsValue(raw.RegistryKey, raw.RegistryValue));
            var stored = polFile.GetValue(raw.RegistryKey, raw.RegistryValue);
            Assert.True(
                object.Equals(stored, 1u) || object.Equals(stored, "1"),
                $"Unexpected stored bool value: {stored}"
            );
        }

        [Fact(
            DisplayName = "Decimal presentation defaultValue is applied when no user input provided"
        )]
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
                StoreAsText = false,
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = decElem.RegistryKey,
                RegistryValue = decElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { decElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx(),
            };
            var policy = BuildPolicy(raw, "DecDefault", "Dec Default");
            policy.Presentation = new global::PolicyPlusCore.Core.Presentation
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
                        Label = "Dec Label",
                    },
                },
            };

            var polFile = new PolFile();
            var edit = new EditSettingTestable();
            edit.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            edit.NotConfiguredOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.EnabledOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.ApplyToPolicySource_PublicForTest();

            PolAssert.HasDwordValue(polFile, raw.RegistryKey, raw.RegistryValue, 42u);
        }

        [Fact(
            DisplayName = "TextBox presentation defaultValue is applied when no user input provided"
        )]
        public void Text_DefaultValue_Applied()
        {
            var textElem = new TextPolicyElement
            {
                ID = "TxtElem",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "TxtValue",
                MaxLength = 200,
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = textElem.RegistryKey,
                RegistryValue = textElem.RegistryValue,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = DummyAdmx(),
            };
            var policy = BuildPolicy(raw, "TxtDefault", "Txt Default");
            policy.Presentation = new global::PolicyPlusCore.Core.Presentation
            {
                Elements = new List<PresentationElement>
                {
                    new TextBoxPresentationElement
                    {
                        ID = "TxtElem",
                        ElementType = "textBox",
                        DefaultValue = "PresetValue",
                        Label = "Text Label",
                    },
                },
            };

            var polFile = new PolFile();
            var edit = new EditSettingTestable();
            edit.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            edit.NotConfiguredOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.EnabledOption.Checked = true;
            edit.InvokeStateRadiosChanged();
            edit.ApplyToPolicySource_PublicForTest();

            PolAssert.HasStringValue(polFile, raw.RegistryKey, raw.RegistryValue, "PresetValue");
        }
    }
}
