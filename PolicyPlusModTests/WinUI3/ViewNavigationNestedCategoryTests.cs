using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlus;
using PolicyPlus.WinUI3.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class ViewNavigationNestedCategoryTests
    {
        private static (AdmxBundle bundle, PolicyPlusCategory root, PolicyPlusCategory sub1, PolicyPlusCategory sub2) BuildNested()
        {
            var root = new PolicyPlusCategory { UniqueID = "CAT:ROOT", DisplayName = "Root", Children = new List<PolicyPlusCategory>() };
            var sub1 = new PolicyPlusCategory { UniqueID = "CAT:ROOT:SUB1", DisplayName = "Sub1", Parent = root, Children = new List<PolicyPlusCategory>() };
            var sub2 = new PolicyPlusCategory { UniqueID = "CAT:ROOT:SUB1:SUB2", DisplayName = "Sub2", Parent = sub1, Children = new List<PolicyPlusCategory>() };
            root.Children.Add(sub1);
            sub1.Children.Add(sub2);

            var pol1 = new PolicyPlusPolicy { UniqueID = "MACHINE:P1", DisplayName = "P1", Category = sub1, RawPolicy = new AdmxPolicy { RegistryKey = "HKLM\\S", RegistryValue = "V", Section = AdmxPolicySection.Machine, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "d.admx" } } };
            var pol2 = new PolicyPlusPolicy { UniqueID = "USER:P2", DisplayName = "P2", Category = sub2, RawPolicy = new AdmxPolicy { RegistryKey = "HKCU\\S", RegistryValue = "V", Section = AdmxPolicySection.User, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "d.admx" } } };
            sub1.Policies = new List<PolicyPlusPolicy> { pol1 };
            sub2.Policies = new List<PolicyPlusPolicy> { pol2 };

            var bundle = new AdmxBundle
            {
                Categories = new Dictionary<string, PolicyPlusCategory>(StringComparer.OrdinalIgnoreCase)
                {
                    { root.UniqueID, root }
                },
                Policies = new Dictionary<string, PolicyPlusPolicy>(StringComparer.OrdinalIgnoreCase)
                {
                    { pol1.UniqueID, pol1 },
                    { pol2.UniqueID, pol2 }
                }
            };
            return (bundle, root, sub1, sub2);
        }

        [Fact(DisplayName = "CategoryIndex resolves nested subcategory IDs")]
        public void FlatIndex_ResolvesNested()
        {
            var (bundle, _, sub1, sub2) = BuildNested();
            var idx = CategoryIndex.BuildIndex(bundle);
            Assert.True(idx.ContainsKey(sub1.UniqueID));
            Assert.True(idx.ContainsKey(sub2.UniqueID));
            Assert.Equal(sub1, idx[sub1.UniqueID]);
            Assert.Equal(sub2, idx[sub2.UniqueID]);
        }

        [Fact(DisplayName = "ViewState chain back navigates across nested categories logically")]
        public void History_BackAcrossNested()
        {
            var (bundle, _, sub1, sub2) = BuildNested();
            var s0 = ViewState.Create(null, string.Empty, AdmxPolicySection.Both, false);
            var s1 = ViewState.Create(sub1.UniqueID, string.Empty, AdmxPolicySection.Both, false);
            var s2 = ViewState.Create(sub2.UniqueID, string.Empty, AdmxPolicySection.Both, false);

            ViewNavigationService.Instance.Clear();
            ViewNavigationService.Instance.Push(s0);
            ViewNavigationService.Instance.Push(s1);
            ViewNavigationService.Instance.Push(s2);

            var back1 = ViewNavigationService.Instance.GoBack();
            Assert.Equal(sub1.UniqueID, back1!.CategoryId);
            var back0 = ViewNavigationService.Instance.GoBack();
            Assert.Null(back0!.CategoryId);
            var f1 = ViewNavigationService.Instance.GoForward();
            Assert.Equal(sub1.UniqueID, f1!.CategoryId);
        }
    }
}
