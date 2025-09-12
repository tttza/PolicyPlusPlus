using PolicyPlusPlus.Models;
using PolicyPlusPlus.Services;
using System.Collections.Generic;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class PolicyListRowGroupTests
    {
        private static (PolicyPlusPolicy userPol, PolicyPlusPolicy compPol) MakePair()
        {
            var user = new PolicyPlusPolicy
            {
                UniqueID = "USER:Pair",
                DisplayName = "Pair",
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.User,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
                }
            };
            var comp = new PolicyPlusPolicy
            {
                UniqueID = "MACHINE:Pair",
                DisplayName = "Pair",
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.Machine,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
                }
            };
            return (user, comp);
        }

        [Fact(DisplayName = "FromGroup reflects actual mixed states (user disabled, comp enabled)")]
        public void Group_Reflects_Actual_Mixed()
        {
            var (userPol, compPol) = MakePair();
            var comp = new PolFile(); var user = new PolFile();
            PolicyProcessing.SetPolicyState(comp, compPol, PolicyState.Enabled, new Dictionary<string, object>());
            PolicyProcessing.SetPolicyState(user, userPol, PolicyState.Disabled, new Dictionary<string, object>());

            PendingChangesService.Instance.Pending.Clear();
            var row = PolicyListRow.FromGroup(compPol, new[] { userPol, compPol }, comp, user);
            Assert.Equal("Enabled", row.ComputerStateText);
            Assert.Equal("Disabled", row.UserStateText);
            Assert.Equal("\uE73E", row.ComputerGlyph);
            Assert.Equal("\uE711", row.UserGlyph);
        }

        [Fact(DisplayName = "FromGroup pending overrides actual for targeted variant only")]
        public void Group_Pending_Overrides_TargetedVariant()
        {
            var (userPol, compPol) = MakePair();
            var comp = new PolFile(); var user = new PolFile();
            PolicyProcessing.SetPolicyState(comp, compPol, PolicyState.Disabled, new Dictionary<string, object>());
            PolicyProcessing.SetPolicyState(user, userPol, PolicyState.Disabled, new Dictionary<string, object>());

            PendingChangesService.Instance.Pending.Clear();
            PendingChangesService.Instance.Add(new PendingChange { PolicyId = compPol.UniqueID, Scope = "Computer", DesiredState = PolicyState.Enabled });

            var row = PolicyListRow.FromGroup(compPol, new[] { userPol, compPol }, comp, user);
            Assert.Equal("Enabled", row.ComputerStateText);
            Assert.Equal("Disabled", row.UserStateText);
        }

        [Fact(DisplayName = "FromGroup pending NotConfigured clears glyph overriding actual configured state")]
        public void Group_Pending_NotConfigured_ClearsGlyph()
        {
            var (userPol, compPol) = MakePair();
            var comp = new PolFile(); var user = new PolFile();
            PolicyProcessing.SetPolicyState(user, userPol, PolicyState.Enabled, new Dictionary<string, object>());

            PendingChangesService.Instance.Pending.Clear();
            PendingChangesService.Instance.Add(new PendingChange { PolicyId = userPol.UniqueID, Scope = "User", DesiredState = PolicyState.NotConfigured });

            var row = PolicyListRow.FromGroup(compPol, new[] { userPol, compPol }, comp, user);
            Assert.Equal("Not configured", row.UserStateText);
            Assert.Equal(string.Empty, row.UserGlyph);
        }
    }
}
