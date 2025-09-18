using PolicyPlusModTests.Testing;
using PolicyPlusCore.Core; // core models
using PolicyPlusCore.Admx; 
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingSpecs
{
    public class PolicyProcessingElementTests
    {
        /// <summary>
        /// Text element: enabling policy with string option writes REG_SZ with provided value.
        /// </summary>
        [Fact(DisplayName = "Text element writes REG_SZ value")]
        public void SetPolicyState_TextElement_WritesStringValueToPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateTextPolicy();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "TextElem", "TestString" } });
            PolAssert.HasStringValue(polFile, policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, "TestString");
        }

        /// <summary>
        /// List element: sequential list items are saved with expected value names/order.
        /// </summary>
        [Fact(DisplayName = "List element preserves order of values")]
        public void SetPolicyState_ListElement_WritesListValuesToPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateListPolicy();
            var values = new List<string> { "A", "B", "C" };
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "ListElem", values } });
            PolAssert.HasSequentialListValues(polFile, policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, values);
        }

        /// <summary>
        /// Enum element: selected index maps to correct underlying numeric value.
        /// </summary>
        [Fact(DisplayName = "Enum element writes expected numeric value")]
        public void SetPolicyState_EnumElement_WritesEnumValueToPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateEnumPolicy();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "EnumElem", 1 } });
            PolAssert.HasDwordValue(polFile, policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, 2U);
        }

        /// <summary>
        /// MultiText element: multiple lines stored as REG_MULTI_SZ.
        /// </summary>
        [Fact(DisplayName = "MultiText element writes REG_MULTI_SZ value")]
        public void SetPolicyState_MultiTextElement_WritesMultiStringToPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateMultiTextPolicy();
            var lines = new[] { "line1", "line2" };
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "MultiTextElem", lines } });
            PolAssert.HasMultiStringValue(polFile, policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, lines);
        }

        /// <summary>
        /// GetPolicyOptionStates should roundtrip previously set element values for all supported element types.
        /// </summary>
        [Fact(DisplayName = "GetPolicyOptionStates returns correct values for all element types")]
        public void GetPolicyOptionStates_ReturnsCorrectValues_ForTextListEnumMultiText()
        {
            var polFile = new PolFile();
            // Text
            var textPolicy = TestPolicyFactory.CreateTextPolicy();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, textPolicy, PolicyState.Enabled, new Dictionary<string, object> { { "TextElem", "TestString" } });
            var textStates = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyOptionStates(polFile, textPolicy);
            Assert.Equal("TestString", textStates["TextElem"]);
            // List
            var listPolicy = TestPolicyFactory.CreateListPolicy();
            var listValues = new List<string> { "A", "B" };
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, listPolicy, PolicyState.Enabled, new Dictionary<string, object> { { "ListElem", listValues } });
            var listStates = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyOptionStates(polFile, listPolicy);
            Assert.True(listValues.All(v => ((IEnumerable<string>)listStates["ListElem"]).Contains(v)));
            // Enum
            var enumPolicy = TestPolicyFactory.CreateEnumPolicy();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, enumPolicy, PolicyState.Enabled, new Dictionary<string, object> { { "EnumElem", 1 } });
            var enumStates = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyOptionStates(polFile, enumPolicy);
            Assert.Equal(1, (int)enumStates["EnumElem"]);
            // MultiText
            var multiTextPolicy = TestPolicyFactory.CreateMultiTextPolicy();
            var multiLines = new[] { "line1", "line2" };
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, multiTextPolicy, PolicyState.Enabled, new Dictionary<string, object> { { "MultiTextElem", multiLines } });
            var multiTextStates = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyOptionStates(polFile, multiTextPolicy);
            Assert.True(multiLines.SequenceEqual((string[])multiTextStates["MultiTextElem"]));
        }
    }
}
