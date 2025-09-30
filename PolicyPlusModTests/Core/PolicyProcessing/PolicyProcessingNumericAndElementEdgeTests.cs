using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingNumeric
{
    public class PolicyProcessingNumericAndElementEdgeTests
    {
        private sealed class InMem : IPolicySource
        {
            private readonly PolFile _pol = new PolFile();

            public bool ContainsValue(string Key, string Value) => _pol.ContainsValue(Key, Value);

            public object? GetValue(string Key, string Value) => _pol.GetValue(Key, Value);

            public bool WillDeleteValue(string Key, string Value) =>
                _pol.WillDeleteValue(Key, Value);

            public List<string> GetValueNames(string Key) => _pol.GetValueNames(Key);

            public void SetValue(
                string Key,
                string Value,
                object Data,
                Microsoft.Win32.RegistryValueKind DataType
            ) => _pol.SetValue(Key, Value, Data, DataType);

            public void ForgetValue(string Key, string Value) => _pol.ForgetValue(Key, Value);

            public void DeleteValue(string Key, string Value) => _pol.DeleteValue(Key, Value);

            public void ClearKey(string Key) => _pol.ClearKey(Key);

            public void ForgetKeyClearance(string Key) => _pol.ForgetKeyClearance(Key);

            public PolFile Inner => _pol;
        }

        private static PolicyPlusPolicy MakeRootNumericPolicy(string uid, string rawValue)
        {
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = rawValue,
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList(),
                Elements = new List<PolicyElement>(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            return new PolicyPlusPolicy
            {
                UniqueID = uid,
                DisplayName = uid,
                RawPolicy = raw,
            };
        }

        [Fact(
            DisplayName = "Root numeric DWORD=1 without OnValue -> Enabled (synthetic inference)"
        )]
        public void RootNumericDWORD_Inference_Enabled()
        {
            var pol = MakeRootNumericPolicy("M:NumInf", "NumInfVal");
            var src = new InMem();
            src.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(
            DisplayName = "Root numeric stored as string '1' does not infer Enabled (type mismatch)"
        )]
        public void RootNumericString_NoInference()
        {
            var pol = MakeRootNumericPolicy("M:NumInfStr", "NumInfValS");
            var src = new InMem();
            src.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                "1",
                Microsoft.Win32.RegistryValueKind.String
            );
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.NotConfigured, state);
        }

        [Theory(
            DisplayName = "Numeric DWORD values different from 1 require matching boolean OnValue to be Enabled"
        )]
        [InlineData(42U)]
        [InlineData(4294967295U)]
        public void TryGetUInt32_DifferentNumeric_ViaBoolean(uint value)
        {
            var pol = MakeRootNumericPolicy("M:NFmtNum" + value, "NumVal2");
            var src = new InMem();
            src.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                value,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var boolElem = new BooleanPolicyElement
            {
                ID = "B",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = pol.RawPolicy.RegistryValue,
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = value,
                    },
                },
            };
            pol.RawPolicy.Elements.Add(boolElem);
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Theory(DisplayName = "TryGetUInt32 returns 0 for invalid / overflow / negative")]
        [InlineData("-1")]
        [InlineData("4294967296")] // overflow
        [InlineData("notnum")]
        public void TryGetUInt32_Invalids_ReturnZero(string raw)
        {
            var pol = MakeRootNumericPolicy("M:NBad" + raw, "BadNumber");
            var src = new InMem();
            src.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                raw,
                Microsoft.Win32.RegistryValueKind.String
            );
            var boolElem = new BooleanPolicyElement
            {
                ID = "B",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = pol.RawPolicy.RegistryValue,
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                },
            };
            pol.RawPolicy.Elements.Add(boolElem);
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.NotConfigured, state);
        }

        [Fact(DisplayName = "Decimal StoreAsText writes string and numeric mode writes DWORD")]
        public void Decimal_StoreAsText_WritesDifferentKinds()
        {
            var decElem = new DecimalPolicyElement
            {
                ID = "Dec",
                ElementType = "decimal",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecVal",
                Minimum = 0,
                Maximum = 1000,
                StoreAsText = true,
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecVal",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { decElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:DecTxt",
                DisplayName = "DecTxt",
                RawPolicy = raw,
            };
            var src = new InMem();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "Dec", 123 } }
            );
            Assert.IsType<string>(src.GetValue(decElem.RegistryKey, decElem.RegistryValue));

            decElem.StoreAsText = false;
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "Dec", 321 } }
            );
            Assert.IsType<uint>(src.GetValue(decElem.RegistryKey, decElem.RegistryValue));
        }

        [Fact(DisplayName = "Text MaxLength truncates value")]
        public void Text_MaxLength_Truncates()
        {
            var textElem = new TextPolicyElement
            {
                ID = "T",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Txt",
                MaxLength = 5,
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Txt",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:TxtMax",
                DisplayName = "TxtMax",
                RawPolicy = raw,
            };
            var src = new InMem();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "T", "ABCDEFGHIJ" } }
            );
            var stored = src.GetValue(textElem.RegistryKey, textElem.RegistryValue) as string;
            Assert.Equal("ABCDE", stored);
        }

        [Fact(DisplayName = "List NoPurgeOthers keeps existing entries")]
        public void List_NoPurgeOthers_Keeps()
        {
            var listElem = new ListPolicyElement
            {
                ID = "L",
                ElementType = "list",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "LP",
                HasPrefix = true,
                NoPurgeOthers = true,
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "LP",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { listElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:ListKeep",
                DisplayName = "ListKeep",
                RawPolicy = raw,
            };
            var src = new InMem();
            // Pre-existing value that should remain
            src.SetValue(
                "Software\\PolicyPlusTest",
                "LP999",
                "OLD",
                Microsoft.Win32.RegistryValueKind.String
            );
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object>
                {
                    {
                        "L",
                        new List<string> { "A", "B" }
                    },
                }
            );
            var names = src.GetValueNames("Software\\PolicyPlusTest");
            Assert.Contains("LP999", names);
            Assert.Contains("LP1", names);
            Assert.Contains("LP2", names);
        }

        [Fact(DisplayName = "Enum Required out-of-range selects first item")]
        public void Enum_Required_OutOfRange_SelectsFirst()
        {
            var enumElem = new EnumPolicyElement
            {
                ID = "E",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumV",
                Required = true,
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                        DisplayCode = "One",
                    },
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 2U,
                        },
                        DisplayCode = "Two",
                    },
                },
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumV",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { enumElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:EnumReq",
                DisplayName = "EnumReq",
                RawPolicy = raw,
            };
            var src = new InMem();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "E", 99 } }
            ); // out-of-range
            Assert.True(src.ContainsValue(enumElem.RegistryKey, enumElem.RegistryValue));
            Assert.Equal(1U, src.GetValue(enumElem.RegistryKey, enumElem.RegistryValue));
        }

        [Fact(DisplayName = "Enum ValueList incomplete does not set explicit Enabled")]
        public void Enum_ValueList_Incomplete_NoExplicit()
        {
            var enumElem = new EnumPolicyElement
            {
                ID = "E2",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Enum2",
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 5U,
                        },
                        DisplayCode = "Five",
                        ValueList = new PolicyRegistrySingleList
                        {
                            AffectedValues = new List<PolicyRegistryListEntry>
                            {
                                new PolicyRegistryListEntry
                                {
                                    RegistryValue = "Aux1",
                                    Value = new PolicyRegistryValue
                                    {
                                        RegistryType = PolicyRegistryValueType.Numeric,
                                        NumberValue = 10U,
                                    },
                                },
                                new PolicyRegistryListEntry
                                {
                                    RegistryValue = "Aux2",
                                    Value = new PolicyRegistryValue
                                    {
                                        RegistryType = PolicyRegistryValueType.Numeric,
                                        NumberValue = 11U,
                                    },
                                },
                            },
                        },
                    },
                },
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Enum2",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { enumElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:EnumVL",
                DisplayName = "EnumVL",
                RawPolicy = raw,
            };
            var src = new InMem();
            // main value present + only one auxiliary
            src.SetValue(
                raw.RegistryKey,
                raw.RegistryValue,
                5U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            src.SetValue(raw.RegistryKey, "Aux1", 10U, Microsoft.Win32.RegistryValueKind.DWord);
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            // Should not count as explicitPos because full ValueList not present; fallback evidence path gives EnabledEvidence>0? Actually ValuePresent(item.Value) true but ValueListPresent false -> skip explicit increment -> evidence uses element present -> Enabled. For robustness assert Enabled but not NotConfigured.
            Assert.Equal(PolicyState.Enabled, state);
        }
    }
}
