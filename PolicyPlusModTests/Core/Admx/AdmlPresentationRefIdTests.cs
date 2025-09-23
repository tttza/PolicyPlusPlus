using System;
using System.IO;
using System.Linq;
using PolicyPlusCore.Admx;
using SharedTest;
using Xunit;

namespace PolicyPlusModTests.Core.Admx
{
    public class AdmlPresentationRefIdTests
    {
        private static (AdmxBundle bundle, AdmxFile admx) LoadDummyVariety()
        {
            var baseDir = AdmxTestHelper.ResolveAdmxAssetsPath();
            var path = Path.Combine(baseDir, "DummyVariety.admx");
            var b = new AdmxBundle { EnableLanguageFallback = true };
            var fails = b.LoadFile(path, "en-US");
            Assert.Empty(fails);
            var admx = b.Sources.Keys.First();
            return (b, admx);
        }

        [Fact(DisplayName = "ADML dropdownList keeps refId (ModeSelect)")]
        public void DropdownList_RefId_Is_Preserved()
        {
            var (b, admx) = LoadDummyVariety();
            var adml = b.Sources[admx];
            Assert.True(adml.PresentationTable.ContainsKey("DummyEnumPresentation"));
            var pres = adml.PresentationTable["DummyEnumPresentation"];
            Assert.Contains(
                pres.Elements,
                e => string.Equals(e.ID, "ModeSelect", StringComparison.Ordinal)
            );
        }

        [Fact(DisplayName = "ADML listBox keeps refId (ListValues/NamedList)")]
        public void ListBox_RefIds_Are_Preserved()
        {
            var (b, admx) = LoadDummyVariety();
            var adml = b.Sources[admx];
            Assert.True(adml.PresentationTable.ContainsKey("DummyListPresentation"));
            var pres = adml.PresentationTable["DummyListPresentation"];
            Assert.Contains(
                pres.Elements,
                e => string.Equals(e.ID, "ListValues", StringComparison.Ordinal)
            );
            Assert.Contains(
                pres.Elements,
                e => string.Equals(e.ID, "NamedList", StringComparison.Ordinal)
            );
        }

        [Fact(DisplayName = "ADML decimalTextBox keeps refId (NumberValue)")]
        public void DecimalTextBox_RefId_Is_Preserved()
        {
            var (b, admx) = LoadDummyVariety();
            var adml = b.Sources[admx];
            Assert.True(adml.PresentationTable.ContainsKey("DummyDecimalPresentation"));
            var pres = adml.PresentationTable["DummyDecimalPresentation"];
            Assert.Contains(
                pres.Elements,
                e => string.Equals(e.ID, "NumberValue", StringComparison.Ordinal)
            );
        }

        [Fact(DisplayName = "ADML textBox keeps refId (TextValue/ExpandableValue)")]
        public void TextBox_RefIds_Are_Preserved()
        {
            var (b, admx) = LoadDummyVariety();
            var adml = b.Sources[admx];
            Assert.True(adml.PresentationTable.ContainsKey("DummyTextPresentation"));
            var pres = adml.PresentationTable["DummyTextPresentation"];
            Assert.Contains(
                pres.Elements,
                e => string.Equals(e.ID, "TextValue", StringComparison.Ordinal)
            );
            Assert.Contains(
                pres.Elements,
                e => string.Equals(e.ID, "ExpandableValue", StringComparison.Ordinal)
            );
        }

        [Fact(DisplayName = "Presentation element IDs map to RawPolicy elements")]
        public void PresentationIds_Map_To_RawPolicyElements()
        {
            var (b, _) = LoadDummyVariety();
            // Check several policies to ensure every presentation element ID exists in RawPolicy.Elements
            string[] targetPolicies = new[]
            {
                "PolicyPlus.DummyVariety:DummyDecimalPolicy",
                "PolicyPlus.DummyVariety:DummyTextPolicy",
                "PolicyPlus.DummyVariety:DummyListPolicy",
                "PolicyPlus.DummyVariety:DummyEnumPolicy",
                "PolicyPlus.DummyVariety:DummyMultiTextPolicy",
            };
            foreach (var uid in targetPolicies)
            {
                Assert.True(b.Policies.ContainsKey(uid), $"Policy not found: {uid}");
                var pol = b.Policies[uid];
                Assert.NotNull(pol.RawPolicy);
                Assert.NotNull(pol.Presentation);
                var elemIds =
                    pol.RawPolicy!.Elements?.Select(e => e.ID)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new();
                foreach (var pe in pol.Presentation!.Elements)
                {
                    if (string.IsNullOrEmpty(pe.ID))
                        continue; // labels etc. have no refId
                    Assert.Contains(pe.ID, elemIds);
                }
            }
        }
    }
}
