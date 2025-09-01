using System;
using System.Collections.Generic;
using System.IO;
using PolicyPlus;
using Xunit;

namespace PolicyPlusModTests
{
    public class PolicySavePipelineTests
    {
        private static AdmxBundle BuildBundleWithMachineToggle(string id = "MACHINE:TestPolicy")
        {
            var policy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "TestValue",
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var p = new PolicyPlusPolicy { RawPolicy = policy, UniqueID = id, DisplayName = "Test Policy" };
            var bundle = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy>() };
            bundle.Policies[id] = p;
            return bundle;
        }

        private static AdmxBundle BuildBundleWithUserToggle(string id = "USER:TestPolicy")
        {
            var policy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "TestValue",
                Section = AdmxPolicySection.User,
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var p = new PolicyPlusPolicy { RawPolicy = policy, UniqueID = id, DisplayName = "Test Policy" };
            var bundle = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy>() };
            bundle.Policies[id] = p;
            return bundle;
        }

        [Fact(DisplayName = "BuildLocalGpoBuffers creates machine POL bytes that contain expected value")]
        public void BuildLocalGpoBuffers_Machine_WritesExpectedValue()
        {
            var id = "MACHINE:TestPolicy";
            var bundle = BuildBundleWithMachineToggle(id);
            var changes = new[]
            {
                new PolicyChangeRequest { PolicyId = id, Scope = PolicyTargetScope.Machine, DesiredState = PolicyState.Enabled, Options = new Dictionary<string, object>() }
            };

            var buffers = PolicySavePipeline.BuildLocalGpoBuffers(bundle, changes);
            Assert.NotNull(buffers.MachinePolBytes);

            // Persist to temp, then load with PolFile.Load to assert
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pol");
            File.WriteAllBytes(tmp, buffers.MachinePolBytes!);
            try
            {
                var pol = PolFile.Load(tmp);
                Assert.True(pol.ContainsValue("Software\\PolicyPlusTest", "TestValue"));
            }
            finally { try { File.Delete(tmp); } catch { } }
        }

        [Fact(DisplayName = "BuildLocalGpoBuffers creates user POL bytes that contain expected value")]
        public void BuildLocalGpoBuffers_User_WritesExpectedValue()
        {
            var id = "USER:TestPolicy";
            var bundle = BuildBundleWithUserToggle(id);
            var changes = new[]
            {
                new PolicyChangeRequest { PolicyId = id, Scope = PolicyTargetScope.User, DesiredState = PolicyState.Enabled, Options = new Dictionary<string, object>() }
            };

            var buffers = PolicySavePipeline.BuildLocalGpoBuffers(bundle, changes);
            Assert.NotNull(buffers.UserPolBytes);

            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pol");
            File.WriteAllBytes(tmp, buffers.UserPolBytes!);
            try
            {
                var pol = PolFile.Load(tmp);
                Assert.True(pol.ContainsValue("Software\\PolicyPlusTest", "TestValue"));
            }
            finally { try { File.Delete(tmp); } catch { } }
        }
    }
}
