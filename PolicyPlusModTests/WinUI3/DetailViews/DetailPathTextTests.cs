using System.Collections.Generic;
using System.Linq;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.ViewModels;
using Xunit;

namespace PolicyPlusModTests.WinUI3.DetailViews
{
    public class DetailPathTextTests
    {
        private static PolicyPlusPolicy MakePolicyWithCategoryChain()
        {
            var root = new PolicyPlusCategory
            {
                DisplayName = "Root",
                RawCategory = new AdmxCategory(),
            };
            var mid = new PolicyPlusCategory
            {
                DisplayName = "Mid",
                RawCategory = new AdmxCategory(),
                Parent = root,
            };
            var leaf = new PolicyPlusCategory
            {
                DisplayName = "Leaf",
                RawCategory = new AdmxCategory(),
                Parent = mid,
            };

            return new PolicyPlusPolicy
            {
                UniqueID = "MACHINE:Sample",
                DisplayName = "SamplePolicy",
                Category = leaf,
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.Machine,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
                },
            };
        }

        [Fact(DisplayName = "Path text contains configuration scope and full category chain")]
        public void PathText_Includes_Scope_And_Categories()
        {
            var p = MakePolicyWithCategoryChain();
            var text = DetailPathFormatter.BuildPathText(p).Replace("\r\n", "\n");

            // Accept any translation for supported locales to make this test locale-agnostic.
            static HashSet<string> AllTranslations(string key)
            {
                var locales = new[]
                {
                    "en-US",
                    "de-DE",
                    "el-GR",
                    "es-ES",
                    "fi-FI",
                    "fr-FR",
                    "hu-HU",
                    "it-IT",
                    "ja-JP",
                    "ko-KR",
                    "nb-NO",
                    "nl-NL",
                    "pl-PL",
                    "pt-BR",
                    "pt-PT",
                    "ru-RU",
                    "sv-SE",
                    "tr-TR",
                    "zh-CN",
                    "zh-TW",
                    "cs-CZ",
                    "da-DK",
                };
                var set = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var loc in locales)
                {
                    var t = LocalizationService.Instance.Translate(key, loc);
                    if (!string.IsNullOrEmpty(t))
                        set.Add(t);
                }
                // Always include the English key as a last-resort fallback.
                set.Add(key);
                return set;
            }

            var configLabels = AllTranslations("Computer Configuration");
            Assert.True(
                configLabels.Any(label => text.Contains(label)),
                "Configuration scope label missing"
            );

            var templatesLabels = AllTranslations("Administrative Templates");
            Assert.True(
                templatesLabels.Any(label => text.Contains(label)),
                "Administrative Templates label missing"
            );

            Assert.Contains("+ Root", text);
            Assert.Contains("+ Mid", text);
            Assert.Contains("+ Leaf", text);
            Assert.Contains("SamplePolicy", text);
        }
    }
}
