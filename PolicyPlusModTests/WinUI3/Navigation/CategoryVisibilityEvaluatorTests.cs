using System.Collections.Generic;
using System.Linq;
using PolicyPlusPlus.ViewModels;
using Xunit;

namespace PolicyPlusModTests.WinUI3.Navigation
{
    public class CategoryVisibilityEvaluatorTests
        : PolicyPlusModTests.TestHelpers.PendingIsolationTestBase
    {
        private static (
            PolicyPlusCategory root,
            PolicyPlusCategory mid,
            PolicyPlusCategory leaf,
            List<PolicyPlusPolicy> all
        ) BuildTree()
        {
            var root = new PolicyPlusCategory
            {
                UniqueID = "cat:root",
                DisplayName = "Root",
                RawCategory = new AdmxCategory(),
            };
            var mid = new PolicyPlusCategory
            {
                UniqueID = "cat:mid",
                DisplayName = "Mid",
                RawCategory = new AdmxCategory(),
                Parent = root,
            };
            var leaf = new PolicyPlusCategory
            {
                UniqueID = "cat:leaf",
                DisplayName = "Leaf",
                RawCategory = new AdmxCategory(),
                Parent = mid,
            };
            root.Children.Add(mid);
            mid.Children.Add(leaf);

            var pUser = new PolicyPlusPolicy
            {
                UniqueID = "USER:Policy",
                DisplayName = "Policy",
                Category = leaf,
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.User,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "x.admx" },
                },
            };
            var pComp = new PolicyPlusPolicy
            {
                UniqueID = "MACHINE:Policy",
                DisplayName = "Policy",
                Category = leaf,
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.Machine,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "x.admx" },
                },
            };
            leaf.Policies.AddRange(new[] { pUser, pComp });
            var all = new List<PolicyPlusPolicy> { pUser, pComp };
            return (root, mid, leaf, all);
        }

        [Fact(DisplayName = "ConfiguredOnly=false returns non-empty categories visible")]
        public void NotConfiguredOnly_NonEmptyVisible()
        {
            var (root, mid, leaf, all) = BuildTree();
            var visible = CategoryVisibilityEvaluator.IsCategoryVisible(
                root,
                all,
                AdmxPolicySection.Both,
                configuredOnly: false,
                compSource: null,
                userSource: null
            );
            Assert.True(visible);
        }

        [Fact(
            DisplayName = "ConfiguredOnly=true hides categories when no configured policies in subtree"
        )]
        public void ConfiguredOnly_Hides_When_NoConfigured()
        {
            var (root, mid, leaf, all) = BuildTree();
            var comp = new PolFile();
            var user = new PolFile();
            try
            {
                // No states set -> not configured anywhere
                var visible = CategoryVisibilityEvaluator.IsCategoryVisible(
                    root,
                    all,
                    AdmxPolicySection.Both,
                    configuredOnly: true,
                    compSource: comp,
                    userSource: user
                );
                Assert.False(visible);
            }
            finally
            {
                PolicyPlusPlus.Services.PendingChangesService.Instance.Pending.Clear();
                PolicyPlusPlus.Services.PendingChangesService.Instance.History.Clear();
            }
        }

        [Fact(
            DisplayName = "ConfiguredOnly=true with Applies=User shows when any user policy configured"
        )]
        public void ConfiguredOnly_Applies_User_Shows_When_Configured()
        {
            var (root, mid, leaf, all) = BuildTree();
            var comp = new PolFile();
            var user = new PolFile();
            try
            {
                // Configure user policy only
                PolicyProcessing.SetPolicyState(
                    user,
                    all.First(p => p.RawPolicy.Section == AdmxPolicySection.User),
                    PolicyState.Enabled,
                    new Dictionary<string, object>()
                );
                var visible = CategoryVisibilityEvaluator.IsCategoryVisible(
                    root,
                    all,
                    AdmxPolicySection.User,
                    configuredOnly: true,
                    compSource: comp,
                    userSource: user
                );
                Assert.True(visible);
            }
            finally
            {
                PolicyPlusPlus.Services.PendingChangesService.Instance.Pending.Clear();
                PolicyPlusPlus.Services.PendingChangesService.Instance.History.Clear();
            }
        }

        [Fact(DisplayName = "ConfiguredOnly=true with pending User change shows category")]
        public void ConfiguredOnly_Pending_User_Shows()
        {
            var (root, mid, leaf, all) = BuildTree();
            var comp = new PolFile();
            var user = new PolFile();

            try
            {
                PolicyPlusPlus.Services.PendingChangesService.Instance.Pending.Clear();
                PolicyPlusPlus.Services.PendingChangesService.Instance.Add(
                    new PolicyPlusPlus.Services.PendingChange
                    {
                        PolicyId = all.First(p =>
                            p.RawPolicy.Section == AdmxPolicySection.User
                        ).UniqueID,
                        Scope = "User",
                        DesiredState = PolicyState.Enabled,
                    }
                );

                var visible = CategoryVisibilityEvaluator.IsCategoryVisible(
                    root,
                    all,
                    AdmxPolicySection.User,
                    configuredOnly: true,
                    compSource: comp,
                    userSource: user
                );
                Assert.True(visible);
            }
            finally
            {
                PolicyPlusPlus.Services.PendingChangesService.Instance.Pending.Clear();
                PolicyPlusPlus.Services.PendingChangesService.Instance.History.Clear();
            }
        }

        [Fact(DisplayName = "ConfiguredOnly=true with pending Machine change shows category")]
        public void ConfiguredOnly_Pending_Machine_Shows()
        {
            var (root, mid, leaf, all) = BuildTree();
            var comp = new PolFile();
            var user = new PolFile();

            try
            {
                PolicyPlusPlus.Services.PendingChangesService.Instance.Pending.Clear();
                PolicyPlusPlus.Services.PendingChangesService.Instance.Add(
                    new PolicyPlusPlus.Services.PendingChange
                    {
                        PolicyId = all.First(p =>
                            p.RawPolicy.Section == AdmxPolicySection.Machine
                        ).UniqueID,
                        Scope = "Computer",
                        DesiredState = PolicyState.Disabled,
                    }
                );

                var visible = CategoryVisibilityEvaluator.IsCategoryVisible(
                    root,
                    all,
                    AdmxPolicySection.Machine,
                    configuredOnly: true,
                    compSource: comp,
                    userSource: user
                );
                Assert.True(visible);
            }
            finally
            {
                PolicyPlusPlus.Services.PendingChangesService.Instance.Pending.Clear();
                PolicyPlusPlus.Services.PendingChangesService.Instance.History.Clear();
            }
        }
    }
}
