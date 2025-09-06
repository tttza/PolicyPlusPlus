using System.Collections.Generic;
using PolicyPlus;
using PolicyPlus.Core.Core;

namespace PolicyPlusModTests.TestHelpers
{
    // Helper factory for building test policies to reduce duplication in test classes
    public static class TestPolicyFactory
    {
        public static PolicyPlusPolicy CreateSimpleTogglePolicy(string uniqueId = "MACHINE:TestPolicy", string displayName = "Test Policy", string regKey = "Software\\PolicyPlusTest", string regValue = "TestValue")
        {
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = regKey,
                RegistryValue = regValue,
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = displayName
            };
        }

        public static PolicyPlusPolicy CreateTextPolicy(string uniqueId = "MACHINE:TextPolicy")
        {
            var textElem = new TextPolicyElement
            {
                ID = "TextElem",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "TextValue",
                MaxLength = 100
            };
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "TextValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "Text Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new TextBoxPresentationElement
                        {
                            ID = "TextElem",
                            ElementType = "textBox",
                            Label = "Text Label",
                            DefaultValue = ""
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateListPolicy(string uniqueId = "MACHINE:ListPolicy")
        {
            var listElem = new ListPolicyElement
            {
                ID = "ListElem",
                ElementType = "list",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ListPrefix",
                HasPrefix = true,
                UserProvidesNames = false
            };
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ListPrefix",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { listElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "List Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new ListPresentationElement
                        {
                            ID = "ListElem",
                            ElementType = "listBox",
                            Label = "List Label"
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateEnumPolicy(string uniqueId = "MACHINE:EnumPolicy")
        {
            var enumElem = new EnumPolicyElement
            {
                ID = "EnumElem",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumValue",
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem { Value = new PolicyRegistryValue { NumberValue = 1U, RegistryType = PolicyRegistryValueType.Numeric }, DisplayCode = "One" },
                    new EnumPolicyElementItem { Value = new PolicyRegistryValue { NumberValue = 2U, RegistryType = PolicyRegistryValueType.Numeric }, DisplayCode = "Two" }
                }
            };
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { enumElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "Enum Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new DropDownPresentationElement
                        {
                            ID = "EnumElem",
                            ElementType = "dropdownList",
                            Label = "Enum Label",
                            NoSort = true,
                            DefaultItemID = 0
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateMultiTextPolicy(string uniqueId = "MACHINE:MultiTextPolicy")
        {
            var multiTextElem = new MultiTextPolicyElement
            {
                ID = "MultiTextElem",
                ElementType = "multiText",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "MultiTextValue"
            };
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "MultiTextValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { multiTextElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "MultiText Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new MultiTextPresentationElement
                        {
                            ID = "MultiTextElem",
                            ElementType = "multiTextBox",
                            Label = "MultiText Label"
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateBinaryPolicy(string uniqueId = "MACHINE:BinaryPolicy")
        {
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "BinaryValue",
                Section = AdmxPolicySection.Machine,
                Elements = null!,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "Binary Policy"
            };
        }

        public static PolicyPlusPolicy CreateQwordPolicy(string uniqueId = "MACHINE:QwordPolicy")
        {
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "QwordValue",
                Section = AdmxPolicySection.Machine,
                Elements = null!,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "Qword Policy"
            };
        }

        public static PolicyPlusPolicy CreateExpandStringPolicy(string uniqueId = "MACHINE:ExpandStringPolicy")
        {
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ExpandStringValue",
                Section = AdmxPolicySection.Machine,
                Elements = null!,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "ExpandString Policy"
            };
        }

        public static PolicyPlusPolicy CreateDeletePolicy(string uniqueId = "MACHINE:DeletePolicy")
        {
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DeleteValue",
                Section = AdmxPolicySection.Machine,
                Elements = null!,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "Delete Policy"
            };
        }

        public static PolicyPlusPolicy CreateClearKeyPolicy(string uniqueId = "MACHINE:ClearKeyPolicy")
        {
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest\\ClearMe",
                RegistryValue = null!,
                Section = AdmxPolicySection.Machine,
                Elements = null!,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "ClearKey Policy"
            };
        }

        public static PolicyPlusPolicy CreateNamedListPolicy(string uniqueId = "MACHINE:NamedListPolicy")
        {
            var listElem = new ListPolicyElement
            {
                ID = "NamedListElem",
                ElementType = "list",
                RegistryKey = "Software\\PolicyPlusTest\\NamedList",
                RegistryValue = string.Empty,
                HasPrefix = false,
                UserProvidesNames = true
            };
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest\\NamedList",
                RegistryValue = string.Empty,
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { listElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "Named List Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new ListPresentationElement
                        {
                            ID = "NamedListElem",
                            ElementType = "listBox",
                            Label = "Named List Label"
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateExpandableTextElementPolicy(string uniqueId = "MACHINE:ExpandableTextElemPolicy")
        {
            var textElem = new TextPolicyElement
            {
                ID = "ExpTextElem",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ExpandTextValue",
                MaxLength = 200,
                RegExpandSz = true
            };
            var rawPolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ExpandTextValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = rawPolicy,
                UniqueID = uniqueId,
                DisplayName = "Expandable Text Element Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new TextBoxPresentationElement
                        {
                            ID = "ExpTextElem",
                            ElementType = "textBox",
                            Label = "Expandable Text Label",
                            DefaultValue = string.Empty
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateRequiredTextPolicy(string uniqueId = "MACHINE:RequiredTextPolicy")
        {
            var textElem = new TextPolicyElement
            {
                ID = "ReqTextElem",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ReqTextValue",
                MaxLength = 50,
                Required = true
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ReqTextValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = raw,
                UniqueID = uniqueId,
                DisplayName = "Required Text Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new TextBoxPresentationElement
                        {
                            ID = "ReqTextElem",
                            ElementType = "textBox",
                            Label = "Required Text",
                            DefaultValue = string.Empty
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateMaxLengthTextPolicy(string uniqueId = "MACHINE:MaxLenTextPolicy", int maxLen = 5)
        {
            var textElem = new TextPolicyElement
            {
                ID = "MaxLenTextElem",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "MaxLenTextValue",
                MaxLength = maxLen
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "MaxLenTextValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = raw,
                UniqueID = uniqueId,
                DisplayName = "Max Length Text Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new TextBoxPresentationElement
                        {
                            ID = "MaxLenTextElem",
                            ElementType = "textBox",
                            Label = "MaxLen Text",
                            DefaultValue = string.Empty
                        }
                    }
                }
            };
        }

        public static PolicyPlusPolicy CreateComboBoxTextPolicy(string uniqueId = "MACHINE:ComboBoxTextPolicy")
        {
            var textElem = new TextPolicyElement
            {
                ID = "ComboTextElem",
                ElementType = "text",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ComboTextValue",
                MaxLength = 100
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "ComboTextValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { textElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            return new PolicyPlusPolicy
            {
                RawPolicy = raw,
                UniqueID = uniqueId,
                DisplayName = "ComboBox Text Policy",
                Presentation = new Presentation
                {
                    Elements = new List<PresentationElement>
                    {
                        new ComboBoxPresentationElement
                        {
                            ID = "ComboTextElem",
                            ElementType = "comboBox",
                            Label = "Combo Text",
                            DefaultText = "DefaultCombo",
                            Suggestions = new List<string>{"Sug1","Sug2"},
                            NoSort = true
                        }
                    }
                }
            };
        }
    }
}
