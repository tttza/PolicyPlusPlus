using System;
using System.IO;
using System.Xml;
using Microsoft.Win32;

using PolicyPlus.Core.IO;

using Xunit;

namespace PolicyPlusModTests
{
    public class CmtxAndConfigStorageTests
    {
        [Fact]
        public void CmtxFile_Load_Parses_Prefixes_Comments_Strings()
        {
            string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<policyComments xmlns=""http://www.microsoft.com/GroupPolicy/CommentDefinitions"">
  <policyNamespaces>
    <using prefix=""foo"" namespace=""http://example.com/foo"" />
  </policyNamespaces>
  <comments>
    <admTemplate>
      <comment policyRef=""bar"" commentText=""Hello"" />
    </admTemplate>
  </comments>
  <resources>
    <stringTable>
      <string id=""s1"">World</string>
    </stringTable>
  </resources>
</policyComments>";

            string path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.cmtx");
            File.WriteAllText(path, xml);
            try
            {
                var cmtx = CmtxFile.Load(path);
                Assert.Equal(path, cmtx.SourceFile);
                Assert.Equal("http://example.com/foo", cmtx.Prefixes["foo"]);
                Assert.Equal("Hello", cmtx.Comments["bar"]);
                Assert.Equal("World", cmtx.Strings["s1"]);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void ConfigurationStorage_Set_Get_HasValue_Works()
        {
            string subkey = $"Software\\PolicyPlusModTests\\ConfigStorage_{Guid.NewGuid():N}";
            try
            {
                var cfg = new ConfigurationStorage(RegistryHive.CurrentUser, subkey);
                Assert.False(cfg.HasValue("k1"));
                Assert.Equal(42, cfg.GetValue("k1", 42));
                cfg.SetValue("k1", "v1");
                Assert.True(cfg.HasValue("k1"));
                Assert.Equal("v1", cfg.GetValue("k1", string.Empty));
            }
            finally
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
                    baseKey.DeleteSubKeyTree(subkey, throwOnMissingSubKey: false);
                }
                catch { }
            }
        }
    }
}
