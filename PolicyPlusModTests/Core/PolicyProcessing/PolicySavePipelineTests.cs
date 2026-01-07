using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessing
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
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            var p = new PolicyPlusPolicy
            {
                RawPolicy = policy,
                UniqueID = id,
                DisplayName = "Test Policy",
            };
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
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
            };
            var p = new PolicyPlusPolicy
            {
                RawPolicy = policy,
                UniqueID = id,
                DisplayName = "Test Policy",
            };
            var bundle = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy>() };
            bundle.Policies[id] = p;
            return bundle;
        }

        [Fact(
            DisplayName = "BuildLocalGpoBuffers creates machine POL bytes that contain expected value"
        )]
        public void BuildLocalGpoBuffers_Machine_WritesExpectedValue()
        {
            var id = "MACHINE:TestPolicy";
            var bundle = BuildBundleWithMachineToggle(id);
            var changes = new[]
            {
                new PolicyChangeRequest
                {
                    PolicyId = id,
                    Scope = PolicyTargetScope.Machine,
                    DesiredState = PolicyState.Enabled,
                    Options = new Dictionary<string, object>(),
                },
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
            finally
            {
                try
                {
                    File.Delete(tmp);
                }
                catch { }
            }
        }

        [Fact(
            DisplayName = "BuildLocalGpoBuffers creates user POL bytes that contain expected value"
        )]
        public void BuildLocalGpoBuffers_User_WritesExpectedValue()
        {
            var id = "USER:TestPolicy";
            var bundle = BuildBundleWithUserToggle(id);
            var changes = new[]
            {
                new PolicyChangeRequest
                {
                    PolicyId = id,
                    Scope = PolicyTargetScope.User,
                    DesiredState = PolicyState.Enabled,
                    Options = new Dictionary<string, object>(),
                },
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
            finally
            {
                try
                {
                    File.Delete(tmp);
                }
                catch { }
            }
        }

        [Fact(DisplayName = "POL save orders delvals before named value '*'")]
        public void PolFile_SerializesDelvalsBeforeAsteriskValue()
        {
            var pol = new PolFile();
            const string key = "Software\\PolicyPlusTest";
            pol.ClearKey(key);
            pol.SetValue(key, "*", "value", RegistryValueKind.String);

            var order = ReadValueNamesInOrder(SaveToBytes(pol));

            Assert.Equal(new[] { "**delvals.", "*" }, order);
        }

        [Fact(DisplayName = "POL ApplyDifference clears key before writing named value '*'")]
        public void PolFile_ApplyDifference_ClearsKeyBeforeAsteriskValue()
        {
            var pol = new PolFile();
            const string key = "Software\\PolicyPlusTest";
            pol.ClearKey(key);
            pol.SetValue(key, "*", "value", RegistryValueKind.String);

            var recorder = new RecordingPolicySource();
            pol.ApplyDifference(null, recorder);

            Assert.Equal(2, recorder.Calls.Count);
            Assert.StartsWith($"ClearKey:{key.ToLowerInvariant()}", recorder.Calls[0]);
            Assert.Equal($"SetValue:{key};*;{RegistryValueKind.String}", recorder.Calls[1]);
        }

        private sealed class RecordingPolicySource : IPolicySource
        {
            public readonly List<string> Calls = new List<string>();

            public bool ContainsValue(string Key, string Value) => false;

            public object? GetValue(string Key, string Value) => null;

            public bool WillDeleteValue(string Key, string Value) => false;

            public List<string> GetValueNames(string Key) => new List<string>();

            public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
            {
                Calls.Add($"SetValue:{Key};{Value};{DataType}");
            }

            public void ForgetValue(string Key, string Value)
            {
                Calls.Add($"ForgetValue:{Key};{Value}");
            }

            public void DeleteValue(string Key, string Value)
            {
                Calls.Add($"DeleteValue:{Key};{Value}");
            }

            public void ClearKey(string Key)
            {
                Calls.Add($"ClearKey:{Key}");
            }

            public void ForgetKeyClearance(string Key)
            {
                Calls.Add($"ForgetKeyClearance:{Key}");
            }
        }

        private static byte[] SaveToBytes(PolFile pol)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.Unicode, leaveOpen: true))
            {
                pol.Save(bw);
            }
            return ms.ToArray();
        }

        private static List<string> ReadValueNamesInOrder(byte[] polBytes)
        {
            using var ms = new MemoryStream(polBytes);
            using var reader = new BinaryReader(ms, Encoding.Unicode, leaveOpen: true);

            if (reader.ReadUInt32() != 0x67655250U)
                throw new InvalidDataException("Missing PReg signature");
            if (reader.ReadUInt32() != 1U)
                throw new InvalidDataException("Unsupported POL version");

            var values = new List<string>();
            while (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                reader.BaseStream.Position += 2; // '['
                _ = ReadSz(reader); // key
                reader.BaseStream.Position += 2; // ';'
                string valueName = ReadSz(reader);
                values.Add(valueName);

                if (reader.ReadUInt16() != ';')
                    reader.BaseStream.Position += 2;
                reader.BaseStream.Position += 4; // RegistryValueKind
                reader.BaseStream.Position += 2; // ';'
                uint length = reader.ReadUInt32();
                reader.BaseStream.Position += 2; // ';'
                reader.BaseStream.Position += length; // data
                reader.BaseStream.Position += 2; // ']'
            }

            return values;
        }

        private static string ReadSz(BinaryReader reader)
        {
            var sb = new StringBuilder();
            while (true)
            {
                int charCode = reader.ReadUInt16();
                if (charCode == 0)
                    break;
                sb.Append(char.ConvertFromUtf32(charCode));
            }
            return sb.ToString();
        }
    }
}
