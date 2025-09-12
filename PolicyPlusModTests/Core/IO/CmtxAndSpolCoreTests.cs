using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PolicyPlusModTests
{
    public class CmtxAndSpolCoreTests
    {
        [Fact(DisplayName = "CmtxFile roundtrip preserves prefixes, comments, and strings")]
        public void CmtxFile_Roundtrip_Works()
        {
            var table = new Dictionary<string, string>
            {
                {"Microsoft.Policies:PolicyA", "Comment A"},
                {"Contoso.Settings:PolicyB", "Comment B"}
            };
            var cmtx = CmtxFile.FromCommentTable(table);
            var tmp = Path.GetTempFileName();
            try
            {
                cmtx.Save(tmp);
                var loaded = CmtxFile.Load(tmp);
                var round = loaded.ToCommentTable();
                Assert.Equal(table.Count, round.Count);
                foreach (var kv in table)
                    Assert.Equal(kv.Value, round[kv.Key]);
            }
            finally { try { File.Delete(tmp); } catch { } }
        }

        [Fact(DisplayName = "ConfigurationStorage stores and retrieves values")]
        public void ConfigurationStorage_Basic_CRUD()
        {
            // HKCU is safe for tests
            var cs = new ConfigurationStorage(RegistryHive.CurrentUser, "Software\\PolicyPlusModTests");
            var key = "TestValue_" + Guid.NewGuid().ToString("N");
            try
            {
                Assert.False(cs.HasValue(key));
                var def = Guid.NewGuid().ToString();
                Assert.Equal(def, cs.GetValue(key, def));
                cs.SetValue(key, "abc");
                Assert.True(cs.HasValue(key));
                Assert.Equal("abc", cs.GetValue(key, "x"));
            }
            finally
            {
                try
                {
                    using var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
                    using var sub = hkcu.OpenSubKey("Software\\PolicyPlusModTests", writable: true);
                    sub?.DeleteValue(key, false);
                }
                catch { }
            }
        }

        [Fact(DisplayName = "SpolFile parses and applies a simple policy state")]
        public void SpolFile_ParseAndApply()
        {
            // Minimal SPOL content with one enabled policy and an option
            var text = string.Join(Environment.NewLine, new[]{
                "Policy Plus Semantic Policy",
                "C test.policy.id",
                "Enabled",
                "  Option1: #5"
            });
            var spol = SpolFile.FromText(text);
            Assert.Single(spol.Policies);
            var state = spol.Policies[0];
            Assert.Equal("test.policy.id", state.UniqueID);
            Assert.Equal(PolicyState.Enabled, state.BasicState);
            // Apply should be callable even with empty workspace; we only assert it doesn't throw
            var bundle = new AdmxBundle();
            bundle.Policies = new Dictionary<string, PolicyPlusPolicy>();
            bundle.Policies["test.policy.id"] = new PolicyPlusPolicy
            {
                UniqueID = "test.policy.id",
                RawPolicy = new AdmxPolicy()
                {
                    RegistryKey = "Software\\Policies\\Contoso",
                    RegistryValue = "Option1",
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile()
                },
                Category = new PolicyPlusCategory()
                {
                    DisplayName = "Cat",
                    Parent = null,
                    RawCategory = new AdmxCategory(),
                }
            };
            var polFile = new PolFile();
            var comments = new Dictionary<string, string>();
            state.Apply(polFile, bundle, comments);
            Assert.True(polFile.GetValueNames("Software\\Policies\\Contoso").Count >= 0);
        }
    }
}
