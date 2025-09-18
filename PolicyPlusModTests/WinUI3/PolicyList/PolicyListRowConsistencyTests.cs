using PolicyPlusPlus.Models;
using PolicyPlusPlus.Services;
using System.Collections.Generic;
using Xunit;

namespace PolicyPlusModTests.WinUI3.PolicyList
{
    public class PolicyListRowConsistencyTests
    {
        private static PolicyPlusPolicy MakeBothPolicy(string id)
        {
            return new PolicyPlusPolicy
            {
                UniqueID = id,
                DisplayName = "Sample",
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.Both,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
                }
            };
        }

        [Fact(DisplayName = "UI shows actual Enabled when no pending override exists")]
        public void Consistency_Actual_Enabled_NoPending()
        {
            var p = MakeBothPolicy("BOTH:Sample");
            var comp = new PolFile(); var user = new PolFile();
            PolicyProcessing.SetPolicyState(comp, p, PolicyState.Enabled, new Dictionary<string, object>());

            PendingChangesService.Instance.Pending.Clear();
            var row = PolicyListRow.FromPolicy(p, comp, user);
            Assert.True(row.ComputerConfigured);
            Assert.Equal("Enabled", row.ComputerStateText);
        }

        [Fact(DisplayName = "UI shows pending Enabled overriding actual Disabled")]
        public void Consistency_Pending_Overrides_Actual()
        {
            var p = MakeBothPolicy("BOTH:Sample2");
            var comp = new PolFile(); var user = new PolFile();
            PolicyProcessing.SetPolicyState(comp, p, PolicyState.Disabled, new Dictionary<string, object>());

            PendingChangesService.Instance.Pending.Clear();
            PendingChangesService.Instance.Add(new PendingChange { PolicyId = p.UniqueID, Scope = "Computer", DesiredState = PolicyState.Enabled });

            var row = PolicyListRow.FromPolicy(p, comp, user);
            Assert.True(row.ComputerConfigured);
            Assert.Equal("Enabled", row.ComputerStateText);
        }

        [Fact(DisplayName = "UI shows Not configured when both actual and pending are none")]
        public void Consistency_NotConfigured_When_None()
        {
            var p = MakeBothPolicy("BOTH:Sample3");
            var row = PolicyListRow.FromPolicy(p, new PolFile(), new PolFile());
            Assert.False(row.UserConfigured);
            Assert.False(row.ComputerConfigured);
            Assert.Equal("Not configured", row.UserStateText);
            Assert.Equal("Not configured", row.ComputerStateText);
        }
    }
}
