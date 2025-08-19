using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PolicyPlus.UI.PolicyDetail;
using PolicyPlus;
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests
{
    public class DetailPolicyFormattedRegFileTests
    {
        private static string Normalize(string s) => new string(s.ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());

        [Fact(DisplayName = "List policy .reg export outputs all list values correctly")]
        public void ListPolicy_RegFileExport_OutputsAllListValues()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateListPolicy();
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue + "1", "A", Microsoft.Win32.RegistryValueKind.String);
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue + "2", "B", Microsoft.Win32.RegistryValueKind.String);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"ListPrefix1\"=\"A\""), norm);
            Assert.Contains(Normalize("\"ListPrefix2\"=\"B\""), norm);
        }

        [Fact(DisplayName = "Toggle policy .reg export outputs DWORD value correctly")]
        public void TogglePolicy_RegFileExport_OutputsDwordValue()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateSimpleTogglePolicy();
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, 1U, Microsoft.Win32.RegistryValueKind.DWord);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"TestValue\"=dword:00000001"), norm);
        }

        [Fact(DisplayName = "Text policy .reg export outputs string value correctly")]
        public void TextPolicy_RegFileExport_OutputsStringValue()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateTextPolicy();
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, "TestString", Microsoft.Win32.RegistryValueKind.String);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"TextValue\"=\"TestString\""), norm);
        }

        [Fact(DisplayName = "Enum policy .reg export outputs numeric value correctly")]
        public void EnumPolicy_RegFileExport_OutputsNumericValue()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateEnumPolicy();
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, 2U, Microsoft.Win32.RegistryValueKind.DWord);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"EnumValue\"=dword:00000002"), norm);
        }

        [Fact(DisplayName = "MultiText policy .reg export outputs multi-string value correctly")]
        public void MultiTextPolicy_RegFileExport_OutputsMultiStringValue()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateMultiTextPolicy();
            var lines = new[] { "line1", "line2" };
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, lines, Microsoft.Win32.RegistryValueKind.MultiString);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"MultiTextValue\"=hex(7):"), norm);
        }

        [Fact(DisplayName = "Binary policy .reg export outputs binary value correctly")]
        public void BinaryPolicy_RegFileExport_OutputsBinaryValue()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateBinaryPolicy();
            var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, data, Microsoft.Win32.RegistryValueKind.Binary);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"BinaryValue\"=hex:de,ad,be,ef"), norm);
        }

        [Fact(DisplayName = "Qword policy .reg export outputs QWORD value correctly")]
        public void QwordPolicy_RegFileExport_OutputsQwordValue()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateQwordPolicy();
            ulong value = 0x1122334455667788UL;
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, value, Microsoft.Win32.RegistryValueKind.QWord);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"QwordValue\"=hex(b):88,77,66,55,44,33,22,11"), norm);
        }

        [Fact(DisplayName = "ExpandString policy .reg export outputs REG_EXPAND_SZ value correctly")]
        public void ExpandStringPolicy_RegFileExport_OutputsExpandStringValue()
        {
            // Arrange
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateExpandStringPolicy();
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, "%SystemRoot%\\Test", Microsoft.Win32.RegistryValueKind.ExpandString);

            var formatter = new DetailPolicyFormatted();
            // Act
            var regFileString = GetRegFileStringForTest(formatter, polFile, policy);
            var norm = Normalize(regFileString);

            // Assert
            Assert.Contains(Normalize("\"ExpandStringValue\"=hex(2):"), norm);
        }

        private string GetRegFileStringForTest(DetailPolicyFormatted formatter, IPolicySource polFile, PolicyPlusPolicy policy)
        {
            var method = typeof(DetailPolicyFormatted).GetMethod("GetRegFileString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (string)method.Invoke(formatter, new object[] { polFile, PolicyState.Enabled, policy, false });
        }
    }
}
