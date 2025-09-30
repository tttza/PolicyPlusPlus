using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingEdge
{
    // Covers deletion semantics, support range expansion (MaxVersion null), and deduplication edge conditions.
    public class PolicyProcessingDeletionRangeAndDedupEdgeTests
    {
        private static AdmxFile Dummy() => new AdmxFile { SourceFile = "dummy.admx" };

        [Fact(
            DisplayName = "DeleteValue without prior existence does not mark Disabled explicitly"
        )]
        public void DeleteValue_NoPrior_NotDisabled()
        {
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DelVal",
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Delete,
                    },
                },
                Elements = new List<PolicyElement>(),
                DefinedIn = Dummy(),
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:DelX",
                DisplayName = "DelX",
                RawPolicy = raw,
            };
            var src = new PolicyPlusCore.IO.PolFile(); // value never set then deleted
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.NotConfigured, state);
        }

        [Fact(DisplayName = "Range MaxVersion null expands to children versions")]
        public void Range_MaxVersion_Null_Expands()
        {
            var parent = new PolicyPlusProduct
            {
                UniqueID = "SuiteParent",
                DisplayName = "SuiteParent",
                RawProduct = new AdmxProduct
                {
                    ID = "SuiteParent",
                    Version = 0,
                    DefinedIn = Dummy(),
                },
            };
            var v1 = new PolicyPlusProduct
            {
                UniqueID = "SuiteParentV1",
                DisplayName = "SuiteParentV1",
                Parent = parent,
                RawProduct = new AdmxProduct
                {
                    ID = "SuiteParentV1",
                    Version = 1,
                    Parent = parent.RawProduct,
                    DefinedIn = Dummy(),
                },
            };
            var v2 = new PolicyPlusProduct
            {
                UniqueID = "SuiteParentV2",
                DisplayName = "SuiteParentV2",
                Parent = parent,
                RawProduct = new AdmxProduct
                {
                    ID = "SuiteParentV2",
                    Version = 2,
                    Parent = parent.RawProduct,
                    DefinedIn = Dummy(),
                },
            };
            parent.Children.Add(v1);
            parent.Children.Add(v2);
            var supRaw = new AdmxSupportDefinition
            {
                ID = "RangeNull",
                Logic = AdmxSupportLogicType.AnyOf,
                DefinedIn = Dummy(),
            };
            var sup = new PolicyPlusSupport
            {
                UniqueID = "RangeNull",
                DisplayName = "RangeNull",
                RawSupport = supRaw,
            };
            sup.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    Product = parent,
                    RawSupportEntry = new AdmxSupportEntry
                    {
                        ProductID = parent.UniqueID,
                        IsRange = true,
                        MinVersion = 1,
                        MaxVersion = null,
                    },
                }
            );
            var rawPol = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "RangeNullVal",
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList(),
                Elements = new List<PolicyElement>(),
                DefinedIn = Dummy(),
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:RangeNull",
                DisplayName = "RangeNull",
                RawPolicy = rawPol,
                SupportedOn = sup,
            };
            bool supported = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { v2 },
                false,
                false
            );
            Assert.True(supported);
        }

        [Fact(DisplayName = "DeduplicatePolicies skips when one already Both")]
        public void DeduplicatePolicies_AlreadyBoth_Skips()
        {
            var bundle = new AdmxBundle();
            var cat = new PolicyPlusCategory
            {
                UniqueID = "CATB",
                DisplayName = "CATB",
                RawCategory = new AdmxCategory { ID = "CATB", DefinedIn = Dummy() },
            };
            bundle.Categories[cat.UniqueID] = cat;
            var both = new PolicyPlusPolicy
            {
                UniqueID = "POL1",
                DisplayName = "SamePol",
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "Val",
                    Section = AdmxPolicySection.Both,
                    AffectedValues = new PolicyRegistryList(),
                    Elements = new List<PolicyElement>(),
                    DefinedIn = Dummy(),
                },
                Category = cat,
            };
            var machine = new PolicyPlusPolicy
            {
                UniqueID = "POL2",
                DisplayName = "SamePol",
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "Val",
                    Section = AdmxPolicySection.Machine,
                    AffectedValues = new PolicyRegistryList(),
                    Elements = new List<PolicyElement>(),
                    DefinedIn = Dummy(),
                },
                Category = cat,
            };
            cat.Policies.Add(both);
            cat.Policies.Add(machine);
            bundle.Policies[both.UniqueID] = both;
            bundle.Policies[machine.UniqueID] = machine;
            int count = global::PolicyPlusCore.Core.PolicyProcessing.DeduplicatePolicies(bundle);
            Assert.Equal(0, count);
            Assert.Equal(2, bundle.Policies.Values.Count(p => p.DisplayName == "SamePol"));
        }

        [Fact(DisplayName = "DeduplicatePolicies merges when explanation null vs empty string")]
        public void DeduplicatePolicies_NullVsEmptyExplanation_Merges()
        {
            var bundle = new AdmxBundle();
            var cat = new PolicyPlusCategory
            {
                UniqueID = "CATC",
                DisplayName = "CATC",
                RawCategory = new AdmxCategory { ID = "CATC", DefinedIn = Dummy() },
            };
            bundle.Categories[cat.UniqueID] = cat;
            var pA = new PolicyPlusPolicy
            {
                UniqueID = "PA",
                DisplayName = "NameX",
                DisplayExplanation = null,
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.Machine,
                    AffectedValues = new PolicyRegistryList(),
                    Elements = new List<PolicyElement>(),
                    DefinedIn = Dummy(),
                },
                Category = cat,
            };
            var pB = new PolicyPlusPolicy
            {
                UniqueID = "PB",
                DisplayName = "NameX",
                DisplayExplanation = string.Empty,
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.User,
                    AffectedValues = new PolicyRegistryList(),
                    Elements = new List<PolicyElement>(),
                    DefinedIn = Dummy(),
                },
                Category = cat,
            };
            cat.Policies.Add(pA);
            cat.Policies.Add(pB);
            bundle.Policies[pA.UniqueID] = pA;
            bundle.Policies[pB.UniqueID] = pB;
            int count = global::PolicyPlusCore.Core.PolicyProcessing.DeduplicatePolicies(bundle);
            Assert.Equal(1, count);
            Assert.Single(bundle.Policies.Values, p => p.DisplayName == "NameX");
        }
    }
}
