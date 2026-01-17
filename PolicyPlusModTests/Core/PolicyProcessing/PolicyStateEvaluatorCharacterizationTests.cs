using System.Collections.Generic;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusModTests.Testing;
using Xunit;
using PP = PolicyPlusCore.Core.PolicyProcessing;

namespace PolicyPlusModTests.Core.PolicyProcessingCharacterization
{
    /// <summary>
    /// Characterization tests for PolicyStateEvaluator extraction (ADR 0013).
    /// These tests document the existing behavior to prevent regressions during refactoring.
    /// </summary>
    public class PolicyStateEvaluatorCharacterizationTests
    {
        private sealed class InMemorySource : IPolicySource
        {
            private readonly PolFile _pol;

            public InMemorySource(PolFile p)
            {
                _pol = p;
            }

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
        }

        #region Explicit Match Priority Over Evidence Scoring

        [Fact(
            DisplayName = "ADR0013: Explicit root OnValue takes precedence over element evidence"
        )]
        public void ExplicitRootOn_TakesPrecedence_OverElementEvidence()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ExplicitPriority1");
            pol.RawPolicy.AffectedValues.OnValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Numeric,
                NumberValue = 1U,
            };

            var boolElem = new BooleanPolicyElement
            {
                ID = "BoolOff",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "ElemFlag",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 0U,
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "ElemFlag",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: Explicit enum item match takes precedence")]
        public void ExplicitEnumMatch_TakesPrecedence()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ExplicitEnumPriority");

            var enumElem = new EnumPolicyElement
            {
                ID = "Enum1",
                ElementType = "enum",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "EnumValue",
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                    },
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 2U,
                        },
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { enumElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "EnumValue",
                2U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        #endregion

        #region Evidence Tie Behavior

        [Fact(DisplayName = "ADR0013: Boolean explicit OnValue match wins over OffValue match")]
        public void BooleanExplicitOnOff_OnWins()
        {
            // When both boolean OnValue and OffValue match, OnValue is explicit match and wins
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:EvidenceTie");

            var boolOn = new BooleanPolicyElement
            {
                ID = "BoolOn",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "OnFlag",
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                },
            };
            var boolOff = new BooleanPolicyElement
            {
                ID = "BoolOff",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "OffFlag",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 0U,
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolOn, boolOff };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OnFlag",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffFlag",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            // Boolean OnValue is explicit match, wins over OffValue explicit match
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: Zero evidence for both produces NotConfigured state")]
        public void ZeroEvidence_ProducesNotConfigured()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ZeroEvidence");
            pol.RawPolicy.AffectedValues.OnValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Numeric,
                NumberValue = 99U,
            };
            pol.RawPolicy.AffectedValues.OffValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Numeric,
                NumberValue = 0U,
            };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                50U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.NotConfigured, state);
        }

        #endregion

        #region Boolean Element Evidence Scoring Weight

        [Fact(
            DisplayName = "ADR0013: Boolean OnValue evidence outweighs multiple OffValue due to 0.1 weight"
        )]
        public void BooleanOnValue_OutweighsMultipleOffValues()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:BoolWeightCompare");

            var boolOn = new BooleanPolicyElement
            {
                ID = "BoolOn",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "OnFlag",
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                },
            };
            var boolOff1 = new BooleanPolicyElement
            {
                ID = "BoolOff1",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "OffFlag1",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 0U,
                    },
                },
            };
            var boolOff2 = new BooleanPolicyElement
            {
                ID = "BoolOff2",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "OffFlag2",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 0U,
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolOn, boolOff1, boolOff2 };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OnFlag",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffFlag1",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffFlag2",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            // 1.0 enabled > 0.2 disabled -> Enabled
            Assert.Equal(PolicyState.Enabled, state);
        }

        #endregion

        #region Null/Empty Policy Handling

        [Fact(DisplayName = "ADR0013: Null policy returns NotConfigured")]
        public void NullPolicy_ReturnsNotConfigured()
        {
            var file = new PolFile();
            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, null!);
            Assert.Equal(PolicyState.NotConfigured, state);
        }

        [Fact(DisplayName = "ADR0013: Policy with null RawPolicy returns NotConfigured")]
        public void NullRawPolicy_ReturnsNotConfigured()
        {
            var pol = new PolicyPlusPolicy { RawPolicy = null! };
            var file = new PolFile();
            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.NotConfigured, state);
        }

        #endregion

        #region List Element Evidence

        [Fact(
            DisplayName = "ADR0013: List element with ClearKey and values present produces Unknown (tie)"
        )]
        public void ListElementClearedButHasValues_ProducesUnknown()
        {
            // ClearKey provides disabled evidence, values present provides enabled evidence
            // When both are equal, result is Unknown
            var pol = TestPolicyFactory.CreateListPolicy("MACHINE:ListClearWithValues");

            var file = new PolFile();
            file.ClearKey("Software\\PolicyPlusTest");
            file.SetValue(
                "Software\\PolicyPlusTest",
                "Item1",
                "Value1",
                Microsoft.Win32.RegistryValueKind.String
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            // ClearKey deletion evidence cancels out value presence evidence
            Assert.Equal(PolicyState.Unknown, state);
        }

        #endregion

        #region Element Evidence Conditions

        [Fact(
            DisplayName = "ADR0013: Element evidence allowed when root has no explicit On/Off values"
        )]
        public void ElementEvidence_AllowedWhenNoRootOnOff()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ElemEvidenceAllow");
            pol.RawPolicy.AffectedValues = new PolicyRegistryList();

            var decElem = new DecimalPolicyElement
            {
                ID = "Dec",
                ElementType = "decimal",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "DecValue",
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { decElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "DecValue",
                100U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(
            DisplayName = "ADR0013: Element deletion evidence allowed when root has no explicit On/Off values"
        )]
        public void ElementDeletionEvidence_AllowedWhenNoRootOnOff()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ElemDelEvidenceAllow");
            pol.RawPolicy.AffectedValues = new PolicyRegistryList();

            var textElem = new TextPolicyElement
            {
                ID = "Text",
                ElementType = "text",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "TextValue",
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { textElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "TextValue",
                "test",
                Microsoft.Win32.RegistryValueKind.String
            );
            file.DeleteValue(pol.RawPolicy.RegistryKey, "TextValue");

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Disabled, state);
        }

        #endregion

        #region ValueList Explicit Match Tests

        [Fact(DisplayName = "ADR0013: Root OnValueList explicit match returns Enabled")]
        public void ExplicitMatch_RootOnValueList_ReturnsEnabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:OnValueListExplicit");
            pol.RawPolicy.AffectedValues.OnValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "Val1",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                    },
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "Val2",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 2U,
                        },
                    },
                },
            };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "Val1",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "Val2",
                2U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(
            DisplayName = "ADR0013: Root OnValueList partial match does not trigger explicit match"
        )]
        public void ExplicitMatch_RootOnValueListPartial_NoExplicitMatch()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:OnValueListPartial");
            pol.RawPolicy.AffectedValues.OnValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "Val1",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                    },
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "Val2",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 2U,
                        },
                    },
                },
            };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "Val1",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            // Val2 is missing

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            // Partial match falls through to evidence scoring
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: Root OffValueList explicit match returns Disabled")]
        public void ExplicitMatch_RootOffValueList_ReturnsDisabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:OffValueListExplicit");
            pol.RawPolicy.AffectedValues.OffValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "OffVal1",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 0U,
                        },
                    },
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "OffVal2",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Text,
                            StringValue = "off",
                        },
                    },
                },
            };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffVal1",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffVal2",
                "off",
                Microsoft.Win32.RegistryValueKind.String
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "ADR0013: Boolean OnValueList explicit match returns Enabled")]
        public void ExplicitMatch_BooleanOnValueList_ReturnsEnabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:BoolOnValueList");

            var boolElem = new BooleanPolicyElement
            {
                ID = "BoolList",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "BoolMain",
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValueList = new PolicyRegistrySingleList
                    {
                        AffectedValues = new List<PolicyRegistryListEntry>
                        {
                            new PolicyRegistryListEntry
                            {
                                RegistryValue = "BoolMain",
                                Value = new PolicyRegistryValue
                                {
                                    RegistryType = PolicyRegistryValueType.Numeric,
                                    NumberValue = 1U,
                                },
                            },
                            new PolicyRegistryListEntry
                            {
                                RegistryValue = "BoolExtra",
                                Value = new PolicyRegistryValue
                                {
                                    RegistryType = PolicyRegistryValueType.Text,
                                    StringValue = "enabled",
                                },
                            },
                        },
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "BoolMain",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "BoolExtra",
                "enabled",
                Microsoft.Win32.RegistryValueKind.String
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: Boolean OffValueList explicit match returns Disabled")]
        public void ExplicitMatch_BooleanOffValueList_ReturnsDisabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:BoolOffValueList");

            var boolElem = new BooleanPolicyElement
            {
                ID = "BoolListOff",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "BoolMain",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValueList = new PolicyRegistrySingleList
                    {
                        AffectedValues = new List<PolicyRegistryListEntry>
                        {
                            new PolicyRegistryListEntry
                            {
                                RegistryValue = "BoolMain",
                                Value = new PolicyRegistryValue
                                {
                                    RegistryType = PolicyRegistryValueType.Numeric,
                                    NumberValue = 0U,
                                },
                            },
                        },
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "BoolMain",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "ADR0013: Enum with ValueList requires both Value and ValueList match")]
        public void ExplicitMatch_EnumWithValueList_RequiresBothMatch()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:EnumValueList");

            var enumElem = new EnumPolicyElement
            {
                ID = "EnumList",
                ElementType = "enum",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "EnumMain",
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                        ValueList = new PolicyRegistrySingleList
                        {
                            AffectedValues = new List<PolicyRegistryListEntry>
                            {
                                new PolicyRegistryListEntry
                                {
                                    RegistryValue = "EnumExtra",
                                    Value = new PolicyRegistryValue
                                    {
                                        RegistryType = PolicyRegistryValueType.Numeric,
                                        NumberValue = 100U,
                                    },
                                },
                            },
                        },
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { enumElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "EnumMain",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "EnumExtra",
                100U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: Enum with ValueList missing ValueList match falls through")]
        public void ExplicitMatch_EnumWithValueListPartial_NoMatch()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:EnumValueListPartial");

            var enumElem = new EnumPolicyElement
            {
                ID = "EnumListPartial",
                ElementType = "enum",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "EnumMain",
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                        ValueList = new PolicyRegistrySingleList
                        {
                            AffectedValues = new List<PolicyRegistryListEntry>
                            {
                                new PolicyRegistryListEntry
                                {
                                    RegistryValue = "EnumExtra",
                                    Value = new PolicyRegistryValue
                                    {
                                        RegistryType = PolicyRegistryValueType.Numeric,
                                        NumberValue = 100U,
                                    },
                                },
                            },
                        },
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { enumElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "EnumMain",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            // EnumExtra is missing, so ValueList does not match

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            // Falls through to evidence; EnumMain=1 present gives evidence for enabled
            Assert.Equal(PolicyState.Enabled, state);
        }

        #endregion

        #region ValueList Evidence Tests

        [Fact(DisplayName = "ADR0013: Root OnValueList contributes to enabled evidence")]
        public void Evidence_RootOnValueList_ContributesToEnabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:OnValueListEvidence");
            pol.RawPolicy.AffectedValues = new PolicyRegistryList
            {
                OnValueList = new PolicyRegistrySingleList
                {
                    AffectedValues = new List<PolicyRegistryListEntry>
                    {
                        new PolicyRegistryListEntry
                        {
                            RegistryValue = "Ev1",
                            Value = new PolicyRegistryValue
                            {
                                RegistryType = PolicyRegistryValueType.Numeric,
                                NumberValue = 1U,
                            },
                        },
                        new PolicyRegistryListEntry
                        {
                            RegistryValue = "Ev2",
                            Value = new PolicyRegistryValue
                            {
                                RegistryType = PolicyRegistryValueType.Numeric,
                                NumberValue = 2U,
                            },
                        },
                    },
                },
            };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "Ev1",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "Ev2",
                2U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: Root OffValueList contributes to disabled evidence")]
        public void Evidence_RootOffValueList_ContributesToDisabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:OffValueListEvidence");
            pol.RawPolicy.AffectedValues = new PolicyRegistryList
            {
                OffValueList = new PolicyRegistrySingleList
                {
                    AffectedValues = new List<PolicyRegistryListEntry>
                    {
                        new PolicyRegistryListEntry
                        {
                            RegistryValue = "OffEv1",
                            Value = new PolicyRegistryValue
                            {
                                RegistryType = PolicyRegistryValueType.Numeric,
                                NumberValue = 0U,
                            },
                        },
                    },
                },
            };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffEv1",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "ADR0013: Boolean OnValueList contributes to enabled evidence")]
        public void Evidence_BooleanOnValueList_ContributesToEnabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:BoolOnValueListEvidence");
            pol.RawPolicy.AffectedValues = new PolicyRegistryList();

            var boolElem = new BooleanPolicyElement
            {
                ID = "BoolListEv",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "BoolMain",
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValueList = new PolicyRegistrySingleList
                    {
                        AffectedValues = new List<PolicyRegistryListEntry>
                        {
                            new PolicyRegistryListEntry
                            {
                                RegistryValue = "BoolMain",
                                Value = new PolicyRegistryValue
                                {
                                    RegistryType = PolicyRegistryValueType.Numeric,
                                    NumberValue = 1U,
                                },
                            },
                        },
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "BoolMain",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: Boolean OffValueList evidence uses 0.1 weight")]
        public void Evidence_BooleanOffValueList_Uses0_1Weight()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:BoolOffValueListWeight");
            pol.RawPolicy.AffectedValues = new PolicyRegistryList();

            var boolElem = new BooleanPolicyElement
            {
                ID = "BoolListOffEv",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "BoolMain",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValueList = new PolicyRegistrySingleList
                    {
                        AffectedValues = new List<PolicyRegistryListEntry>
                        {
                            new PolicyRegistryListEntry
                            {
                                RegistryValue = "BoolMain",
                                Value = new PolicyRegistryValue
                                {
                                    RegistryType = PolicyRegistryValueType.Numeric,
                                    NumberValue = 0U,
                                },
                            },
                        },
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };

            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "BoolMain",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            // OffValueList match gives 1.0 * 0.1 = 0.1 disabled evidence
            // No other evidence, so disabled wins
            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "ADR0013: ValueList with custom DefaultRegistryKey uses that key")]
        public void ValueList_CustomDefaultRegistryKey_UsesSpecifiedKey()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ValueListCustomKey");
            string customKey = "MACHINE\\Software\\CustomKey";
            pol.RawPolicy.AffectedValues.OnValueList = new PolicyRegistrySingleList
            {
                DefaultRegistryKey = customKey,
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "CustomVal",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 42U,
                        },
                    },
                },
            };

            var file = new PolFile();
            file.SetValue(customKey, "CustomVal", 42U, Microsoft.Win32.RegistryValueKind.DWord);

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "ADR0013: ValueList entry with custom RegistryKey overrides default")]
        public void ValueList_EntryCustomRegistryKey_OverridesDefault()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ValueListEntryKey");
            string entryKey = "MACHINE\\Software\\EntryKey";
            pol.RawPolicy.AffectedValues.OnValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryKey = entryKey,
                        RegistryValue = "EntryVal",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 99U,
                        },
                    },
                },
            };

            var file = new PolFile();
            file.SetValue(entryKey, "EntryVal", 99U, Microsoft.Win32.RegistryValueKind.DWord);

            var src = new InMemorySource(file);
            var state = PP.GetPolicyState(src, pol);

            Assert.Equal(PolicyState.Enabled, state);
        }

        #endregion
    }
}
