using PolicyPlusModTests.Testing;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3.ImportExport
{
    public class RegistrySearchTests
    {
        [Fact(
            DisplayName = "Value-name-only search does not match key path substring (e.g., 'Edge')"
        )]
        public void ValueNameOnly_DoesNotMatch_KeySubstring()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy(
                uniqueId: "MACHINE:EdgeTest1",
                displayName: "Edge Test",
                regKey: "Software\\Policies\\Microsoft\\Edge",
                regValue: "TargetBlankImpliesNoOpener"
            );

            Assert.False(
                RegistrySearch.SearchRegistryValueNameOnly(pol, "edge", allowSubstring: true)
            );
            Assert.False(
                RegistrySearch.SearchRegistryValueNameOnly(pol, "*edge*", allowSubstring: true)
            );
        }

        [Fact(DisplayName = "Value-name-only search matches when value name contains substring")]
        public void ValueNameOnly_Matches_ValueSubstring()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy(
                uniqueId: "MACHINE:EdgeTest2",
                displayName: "Edge Test 2",
                regKey: "Software\\Policies\\Microsoft\\Edge",
                regValue: "EdgeSettingEnabled"
            );

            Assert.True(
                RegistrySearch.SearchRegistryValueNameOnly(pol, "edge", allowSubstring: true)
            );
            Assert.True(
                RegistrySearch.SearchRegistryValueNameOnly(pol, "*Edge*", allowSubstring: true)
            );
            Assert.True(
                RegistrySearch.SearchRegistryValueNameOnly(
                    pol,
                    "EdgeSettingEnabled",
                    allowSubstring: false
                )
            );
        }

        [Fact(DisplayName = "Key path search still matches key substring for completeness")]
        public void KeyPathSearch_Matches_KeySubstring()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy(
                uniqueId: "MACHINE:EdgeTest3",
                displayName: "Edge Test 3",
                regKey: "Software\\Policies\\Microsoft\\Edge",
                regValue: "TargetBlankImpliesNoOpener"
            );

            Assert.True(
                RegistrySearch.SearchRegistry(
                    pol,
                    keyName: "edge",
                    valName: string.Empty,
                    allowSubstring: true
                )
            );
            Assert.True(
                RegistrySearch.SearchRegistry(
                    pol,
                    keyName: "*edge*",
                    valName: string.Empty,
                    allowSubstring: true
                )
            );
        }
    }
}
