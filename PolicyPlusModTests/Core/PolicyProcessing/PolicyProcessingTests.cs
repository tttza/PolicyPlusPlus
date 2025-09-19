using System.Collections.Generic;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusModTests.Testing;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingSpecs
{
    public class PolicyProcessingTests
    {
        /// <summary>
        /// Enabled state should write a DWORD value 1 under the policy's registry key/value.
        /// </summary>
        [Fact(DisplayName = "Enabled state writes DWORD 1 value")]
        public void SetPolicyState_Enabled_WritesExpectedValueToPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateSimpleTogglePolicy();
            PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                polFile,
                policy,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            PolAssert.HasDwordValue(
                polFile,
                policy.RawPolicy.RegistryKey,
                policy.RawPolicy.RegistryValue,
                1U
            );
        }

        /// <summary>
        /// Disabled state should remove the registry value previously written by Enabled.
        /// </summary>
        [Fact(DisplayName = "Disabled state removes previously written value")]
        public void SetPolicyState_Disabled_RemovesValueFromPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateSimpleTogglePolicy();
            PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                polFile,
                policy,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            PolAssert.HasDwordValue(
                polFile,
                policy.RawPolicy.RegistryKey,
                policy.RawPolicy.RegistryValue,
                1U
            );
            PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                polFile,
                policy,
                PolicyState.Disabled,
                new Dictionary<string, object>()
            );
            PolAssert.NotContains(
                polFile,
                policy.RawPolicy.RegistryKey,
                policy.RawPolicy.RegistryValue
            );
        }

        /// <summary>
        /// NotConfigured (ForgetPolicy) should also remove any existing registry value.
        /// </summary>
        [Fact(DisplayName = "Not Configured removes existing value")]
        public void SetPolicyState_NotConfigured_RemovesValueFromPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateSimpleTogglePolicy();
            PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                polFile,
                policy,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            PolAssert.HasDwordValue(
                polFile,
                policy.RawPolicy.RegistryKey,
                policy.RawPolicy.RegistryValue,
                1U
            );
            PolicyPlusCore.Core.PolicyProcessing.ForgetPolicy(polFile, policy);
            PolAssert.NotContains(
                polFile,
                policy.RawPolicy.RegistryKey,
                policy.RawPolicy.RegistryValue
            );
        }
    }
}
