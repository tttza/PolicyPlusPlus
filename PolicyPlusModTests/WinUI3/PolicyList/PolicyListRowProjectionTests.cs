using PolicyPlusPlus.Models;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3.PolicyList
{
    public class PolicyListRowProjectionTests
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
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
                },
                Category = new PolicyPlusCategory
                {
                    DisplayName = "Cat",
                    RawCategory = new AdmxCategory(),
                },
            };
        }

        [Fact]
        public void FromPolicy_ReflectsPendingChanges_OverActual()
        {
            var p = MakeBothPolicy("BOTH:Sample");
            IPolicySource comp = new PolFile();
            IPolicySource user = new PolFile();

            PendingChangesService.Instance.Pending.Clear();
            // Actual none; set pending for User enabled
            PendingChangesService.Instance.Add(
                new PendingChange
                {
                    PolicyId = p.UniqueID,
                    PolicyName = p.DisplayName,
                    Scope = "User",
                    Action = "Enable",
                    DesiredState = PolicyState.Enabled,
                }
            );

            var row = PolicyListRow.FromPolicy(p, comp, user);
            Assert.True(row.UserConfigured);
            Assert.True(row.UserEnabled);
            Assert.Equal("\uE73E", row.UserGlyph);
            Assert.Equal("Enabled", row.UserStateText);
            Assert.False(row.ComputerConfigured);
            Assert.Equal("Not configured", row.ComputerStateText);
        }

        [Fact]
        public void FromGroup_PrefersEnabledGlyph_WhenBothExist()
        {
            var userPol = MakeBothPolicy("USER:Sample");
            userPol.RawPolicy.Section = AdmxPolicySection.User;
            var compPol = MakeBothPolicy("MACHINE:Sample");
            compPol.RawPolicy.Section = AdmxPolicySection.Machine;

            // Pending: user Disabled, computer Enabled
            PendingChangesService.Instance.Pending.Clear();
            PendingChangesService.Instance.Add(
                new PendingChange
                {
                    PolicyId = userPol.UniqueID,
                    Scope = "User",
                    DesiredState = PolicyState.Disabled,
                }
            );
            PendingChangesService.Instance.Add(
                new PendingChange
                {
                    PolicyId = compPol.UniqueID,
                    Scope = "Computer",
                    DesiredState = PolicyState.Enabled,
                }
            );

            var row = PolicyListRow.FromGroup(
                compPol,
                new[] { userPol, compPol },
                new PolFile(),
                new PolFile()
            );
            Assert.Equal("\uE73E", row.ComputerGlyph);
            Assert.Equal("Disabled", row.UserStateText);
        }
    }
}
