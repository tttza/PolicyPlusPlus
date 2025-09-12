using PolicyPlusPlus.Services;
using Microsoft.Win32;
using Xunit;
using System.Linq;

namespace PolicyPlusModTests.Core
{
    public class RegImportHelperTests
    {
        [Fact(DisplayName = "FilterToPolicyKeysInPlace keeps only policy roots and preserves others when off")]
        public void Filter_PolicyRoots()
        {
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            // Policy key
            reg.Keys.Add(new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\Policies\\SamplePolicy",
                Values = { new RegFile.RegFileValue { Name = "Val1", Kind = RegistryValueKind.DWord, Data = 1u } }
            });
            // Non-policy key should be removed
            reg.Keys.Add(new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\RandomVendor\\Setting",
                Values = { new RegFile.RegFileValue { Name = "X", Kind = RegistryValueKind.String, Data = "abc" } }
            });
            // Similar prefix but boundary test (software\policiesX) should be removed
            reg.Keys.Add(new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\PoliciesExtra\\ShouldGo",
                Values = { new RegFile.RegFileValue { Name = "Y", Kind = RegistryValueKind.String, Data = "abc" } }
            });

            RegImportHelper.FilterToPolicyKeysInPlace(reg);

            Assert.Single(reg.Keys); // Only one key should remain
            var remaining = reg.Keys.First();
            Assert.Contains("Software\\Policies\\SamplePolicy", remaining.Name);
        }

        [Fact(DisplayName = "Clone creates deep copy so filtering does not mutate original")]
        public void Clone_IsDeep()
        {
            var original = new RegFile();
            original.Keys.Add(new RegFile.RegFileKey
            {
                Name = "HKEY_CURRENT_USER\\Software\\Policies\\DeepCopyTest",
                Values = { new RegFile.RegFileValue { Name = "A", Kind = RegistryValueKind.DWord, Data = 5u } }
            });
            var copy = RegImportHelper.Clone(original);
            RegImportHelper.FilterToPolicyKeysInPlace(copy);

            // Mutate copy value to ensure original untouched
            copy.Keys[0].Values[0].Data = 9u;

            Assert.Equal(5u, original.Keys[0].Values[0].Data); // Original retained
        }
    }
}
