using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingSupport
{
    public class PolicyProcessingSupportAndDedupeTests
    {
        private static AdmxFile DummyFile() => new AdmxFile { SourceFile = "dummy.admx" };

        private static PolicyPlusProduct MakeProduct(
            string id,
            int version = 0,
            PolicyPlusProduct? parent = null
        )
        {
            var raw = new AdmxProduct
            {
                ID = id,
                Version = version,
                DefinedIn = DummyFile(),
            };
            var prod = new PolicyPlusProduct
            {
                UniqueID = id,
                DisplayName = id,
                RawProduct = raw,
                Parent = parent,
            };
            parent?.Children.Add(prod);
            return prod;
        }

        private static PolicyPlusSupport MakeSupport(
            string id,
            AdmxSupportLogicType logic,
            params PolicyPlusProduct[] products
        )
        {
            var raw = new AdmxSupportDefinition
            {
                ID = id,
                Logic = logic,
                DefinedIn = DummyFile(),
            };
            var support = new PolicyPlusSupport
            {
                UniqueID = id,
                DisplayName = id,
                RawSupport = raw,
            };
            foreach (var p in products)
            {
                support.Elements.Add(
                    new PolicyPlusSupportEntry
                    {
                        Product = p,
                        RawSupportEntry = new AdmxSupportEntry { ProductID = p.UniqueID },
                    }
                );
            }
            return support;
        }

        private static PolicyPlusSupport MakeRangeSupport(
            string id,
            PolicyPlusProduct parent,
            int min,
            int max
        )
        {
            var raw = new AdmxSupportDefinition
            {
                ID = id,
                Logic = AdmxSupportLogicType.AnyOf,
                DefinedIn = DummyFile(),
            };
            var support = new PolicyPlusSupport
            {
                UniqueID = id,
                DisplayName = id,
                RawSupport = raw,
            };
            support.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    Product = parent,
                    RawSupportEntry = new AdmxSupportEntry
                    {
                        ProductID = parent.UniqueID,
                        IsRange = true,
                        MinVersion = min,
                        MaxVersion = max,
                    },
                }
            );
            return support;
        }

        private static PolicyPlusPolicy MakePolicy(
            string uid,
            string name,
            AdmxPolicySection section,
            PolicyPlusSupport? support = null
        )
        {
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = name + "Value",
                Section = section,
                AffectedValues = new PolicyRegistryList(),
                Elements = new List<PolicyElement>(),
                DefinedIn = DummyFile(),
            };
            return new PolicyPlusPolicy
            {
                UniqueID = uid,
                DisplayName = name,
                RawPolicy = raw,
                SupportedOn = support,
            };
        }

        [Fact(
            DisplayName = "DeduplicatePolicies merges Machine+User into Both and removes one policy"
        )]
        public void DeduplicatePolicies_Merges()
        {
            var bundle = new AdmxBundle();
            var cat = new PolicyPlusCategory
            {
                UniqueID = "CAT",
                DisplayName = "Cat",
                RawCategory = new AdmxCategory { ID = "Cat", DefinedIn = DummyFile() },
            };
            bundle.Categories[cat.UniqueID] = cat;
            var pMachine = MakePolicy("M:Pol", "DupPol", AdmxPolicySection.Machine);
            var pUser = MakePolicy("U:Pol", "DupPol", AdmxPolicySection.User);
            pMachine.Category = cat;
            pUser.Category = cat;
            cat.Policies.Add(pMachine);
            cat.Policies.Add(pUser);
            bundle.Policies[pMachine.UniqueID] = pMachine;
            bundle.Policies[pUser.UniqueID] = pUser;

            int count = global::PolicyPlusCore.Core.PolicyProcessing.DeduplicatePolicies(bundle);

            Assert.Equal(1, count);
            Assert.Single(bundle.Policies.Values, p => p.DisplayName == "DupPol");
            var remaining = bundle.Policies.Values.First(p => p.DisplayName == "DupPol");
            Assert.Equal(AdmxPolicySection.Both, remaining.RawPolicy.Section);
            Assert.Single(cat.Policies, p => p.DisplayName == "DupPol");
        }

        [Fact(DisplayName = "DeduplicatePolicies skips when differing registry key")]
        public void DeduplicatePolicies_Skips_DifferentKey()
        {
            var bundle = new AdmxBundle();
            var cat = new PolicyPlusCategory
            {
                UniqueID = "CAT2",
                DisplayName = "Cat2",
                RawCategory = new AdmxCategory { ID = "Cat2", DefinedIn = DummyFile() },
            };
            bundle.Categories[cat.UniqueID] = cat;
            var pA = MakePolicy("M:PolA", "SameName", AdmxPolicySection.Machine);
            var pB = MakePolicy("U:PolB", "SameName", AdmxPolicySection.User);
            pB.RawPolicy.RegistryKey = "Software\\Different"; // difference prevents merge
            pA.Category = cat;
            pB.Category = cat;
            cat.Policies.Add(pA);
            cat.Policies.Add(pB);
            bundle.Policies[pA.UniqueID] = pA;
            bundle.Policies[pB.UniqueID] = pB;

            int count = global::PolicyPlusCore.Core.PolicyProcessing.DeduplicatePolicies(bundle);
            Assert.Equal(0, count);
            Assert.Equal(2, bundle.Policies.Values.Count(p => p.DisplayName == "SameName"));
        }

        [Fact(DisplayName = "IsPolicySupported AnyOf satisfied by one product")]
        public void IsPolicySupported_AnyOf_SingleMatch()
        {
            var root = MakeProduct("Root");
            var a = MakeProduct("ProdA", 1, root);
            var b = MakeProduct("ProdB", 1, root);
            var support = MakeSupport("SupAny", AdmxSupportLogicType.AnyOf, a, b);
            var pol = MakePolicy("M:SupAny", "SupportAny", AdmxPolicySection.Machine, support);
            bool supported = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { b },
                false,
                false
            );
            Assert.True(supported);
        }

        [Fact(
            DisplayName = "IsPolicySupported AllOf fallback treated as AnyOf when AlwaysUseAny=false"
        )]
        public void IsPolicySupported_AllOf_FallbackToAny()
        {
            var root = MakeProduct("Root2");
            var a = MakeProduct("ProdA2", 1, root);
            var b = MakeProduct("ProdB2", 1, root);
            var support = MakeSupport("SupAll", AdmxSupportLogicType.AllOf, a, b);
            var pol = MakePolicy("M:SupAll", "SupportAll", AdmxPolicySection.Machine, support);
            bool supported = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { a },
                false,
                false
            );
            Assert.True(supported); // behaves like AnyOf
        }

        [Fact(DisplayName = "IsPolicySupported AllOf enforced when AlwaysUseAny=true")]
        public void IsPolicySupported_AllOf_Enforced()
        {
            var root = MakeProduct("Root3");
            var a = MakeProduct("ProdA3", 1, root);
            var b = MakeProduct("ProdB3", 1, root);
            var support = MakeSupport("SupAllEnf", AdmxSupportLogicType.AllOf, a, b);
            var pol = MakePolicy(
                "M:SupAllEnf",
                "SupportAllEnf",
                AdmxPolicySection.Machine,
                support
            );
            bool onlyA = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { a },
                true,
                false
            );
            bool both = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { a, b },
                true,
                false
            );
            Assert.False(onlyA);
            Assert.True(both);
        }

        [Fact(DisplayName = "IsPolicySupported range entry satisfied by child version product")]
        public void IsPolicySupported_Range_ByChildVersion()
        {
            var parent = MakeProduct("Suite");
            var v1 = MakeProduct("SuiteV1", 1, parent);
            var v2 = MakeProduct("SuiteV2", 2, parent);
            var v3 = MakeProduct("SuiteV3", 3, parent);
            var support = MakeRangeSupport("SupRange", parent, 1, 3);
            var pol = MakePolicy("M:SupRange", "SupportRange", AdmxPolicySection.Machine, support);
            bool supported = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { v2 },
                false,
                false
            );
            Assert.True(supported);
        }

        [Fact(DisplayName = "IsPolicySupported blank logic respects ApproveLiterals flag")]
        public void IsPolicySupported_BlankLogic_ApproveLiterals()
        {
            var support = new PolicyPlusSupport
            {
                UniqueID = "BlankSup",
                DisplayName = "BlankSup",
                RawSupport = new AdmxSupportDefinition
                {
                    ID = "BlankSup",
                    Logic = AdmxSupportLogicType.Blank,
                    DefinedIn = DummyFile(),
                },
            };
            var pol = MakePolicy("M:Blank", "BlankPolicy", AdmxPolicySection.Machine, support);
            Assert.False(
                global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                    pol,
                    new List<PolicyPlusProduct>(),
                    false,
                    false
                )
            );
            Assert.True(
                global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                    pol,
                    new List<PolicyPlusProduct>(),
                    false,
                    true
                )
            );
        }

        [Fact(
            DisplayName = "IsPolicySupported nested support (root AnyOf -> child products) succeeds"
        )]
        public void IsPolicySupported_Nested_AnyOf()
        {
            var rootProd = MakeProduct("NestRoot");
            var c1 = MakeProduct("ChildP1", 1, rootProd);
            var c2 = MakeProduct("ChildP2", 2, rootProd);
            // Child support definition listing both products
            var childRaw = new AdmxSupportDefinition
            {
                ID = "ChildSupport",
                Logic = AdmxSupportLogicType.AnyOf,
                DefinedIn = DummyFile(),
            };
            var childSupport = new PolicyPlusSupport
            {
                UniqueID = "ChildSupport",
                DisplayName = "ChildSupport",
                RawSupport = childRaw,
            };
            childSupport.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    Product = c1,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = c1.UniqueID },
                }
            );
            childSupport.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    Product = c2,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = c2.UniqueID },
                }
            );

            // Root support referencing child support only
            var rootRaw = new AdmxSupportDefinition
            {
                ID = "RootSupport",
                Logic = AdmxSupportLogicType.AnyOf,
                DefinedIn = DummyFile(),
            };
            var rootSupport = new PolicyPlusSupport
            {
                UniqueID = "RootSupport",
                DisplayName = "RootSupport",
                RawSupport = rootRaw,
            };
            rootSupport.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    SupportDefinition = childSupport,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = childSupport.UniqueID },
                }
            );

            var pol = MakePolicy(
                "M:NestedAny",
                "NestedAnyPolicy",
                AdmxPolicySection.Machine,
                rootSupport
            );
            bool supported = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { c2 },
                false,
                false
            );
            Assert.True(supported);
        }

        [Fact(DisplayName = "IsPolicySupported nested AllOf enforced with AlwaysUseAny=true")]
        public void IsPolicySupported_Nested_AllOf_Enforced()
        {
            var rootProd = MakeProduct("NestRoot2");
            var c1 = MakeProduct("Child2P1", 1, rootProd);
            var c2 = MakeProduct("Child2P2", 2, rootProd);
            var childRaw = new AdmxSupportDefinition
            {
                ID = "ChildSupportAll",
                Logic = AdmxSupportLogicType.AllOf,
                DefinedIn = DummyFile(),
            };
            var childSupport = new PolicyPlusSupport
            {
                UniqueID = "ChildSupportAll",
                DisplayName = "ChildSupportAll",
                RawSupport = childRaw,
            };
            childSupport.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    Product = c1,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = c1.UniqueID },
                }
            );
            childSupport.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    Product = c2,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = c2.UniqueID },
                }
            );
            var rootRaw = new AdmxSupportDefinition
            {
                ID = "RootSupportAll",
                Logic = AdmxSupportLogicType.AnyOf, // Root can be AnyOf; child AllOf is what matters
                DefinedIn = DummyFile(),
            };
            var rootSupport = new PolicyPlusSupport
            {
                UniqueID = "RootSupportAll",
                DisplayName = "RootSupportAll",
                RawSupport = rootRaw,
            };
            rootSupport.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    SupportDefinition = childSupport,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = childSupport.UniqueID },
                }
            );
            var pol = MakePolicy(
                "M:NestedAll",
                "NestedAllPolicy",
                AdmxPolicySection.Machine,
                rootSupport
            );
            bool onlyOne = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { c1 },
                true,
                false
            );
            bool both = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { c1, c2 },
                true,
                false
            );
            Assert.False(onlyOne);
            Assert.True(both);
        }

        [Fact(DisplayName = "DeduplicatePolicies skips when DisplayExplanation differs")]
        public void DeduplicatePolicies_Skips_DifferentExplanation()
        {
            var bundle = new AdmxBundle();
            var cat = new PolicyPlusCategory
            {
                UniqueID = "CAT3",
                DisplayName = "Cat3",
                RawCategory = new AdmxCategory { ID = "Cat3", DefinedIn = DummyFile() },
            };
            bundle.Categories[cat.UniqueID] = cat;
            var pA = MakePolicy("M:PolC", "SameName2", AdmxPolicySection.Machine);
            var pB = MakePolicy("U:PolD", "SameName2", AdmxPolicySection.User);
            pA.DisplayExplanation = "Explain A";
            pB.DisplayExplanation = "Explain B"; // explanation difference prevents merge
            pA.Category = cat;
            pB.Category = cat;
            cat.Policies.Add(pA);
            cat.Policies.Add(pB);
            bundle.Policies[pA.UniqueID] = pA;
            bundle.Policies[pB.UniqueID] = pB;
            int count = global::PolicyPlusCore.Core.PolicyProcessing.DeduplicatePolicies(bundle);
            Assert.Equal(0, count);
            Assert.Equal(2, bundle.Policies.Values.Count(p => p.DisplayName == "SameName2"));
        }

        [Fact(DisplayName = "IsPolicySupported exact range Min=Max satisfied at boundary")]
        public void IsPolicySupported_RangeExactBoundary()
        {
            var parent = MakeProduct("SuiteExact");
            var v5 = MakeProduct("SuiteV5", 5, parent);
            var support = MakeRangeSupport("SupRangeExact", parent, 5, 5);
            var pol = MakePolicy(
                "M:SupRangeExact",
                "SupportRangeExact",
                AdmxPolicySection.Machine,
                support
            );
            bool supported = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { v5 },
                false,
                false
            );
            Assert.True(supported);
        }
    }
}
