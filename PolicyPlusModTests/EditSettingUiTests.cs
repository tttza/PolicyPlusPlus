using System.Collections.Generic;
using System.Windows.Forms;
using PolicyPlus.UI.PolicyDetail;
using PolicyPlus;
using Xunit;
using PolicyPlusModTests.TestHelpers;

namespace PolicyPlusModTests
{
    public class EditSettingUiTests
    {
        /// <summary>
        /// Applying a text element through the EditSetting UI should persist a REG_SZ to the backing policy source.
        /// </summary>
        [Fact(DisplayName = "UI Text element applies REG_SZ value")]
        public void EditSetting_ApplyTextElement_UpdatesPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateTextPolicy();
            var editSetting = new EditSettingTestable();
            editSetting.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            editSetting.SetTextElementValue("TextElem", "UiTestString");
            editSetting.EnableAndApply();
            Assert.True(polFile.ContainsValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
            Assert.Equal("UiTestString", polFile.GetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
        }

        /// <summary>
        /// Applying a list element should create the sequential list entries in the policy source.
        /// </summary>
        [Fact(DisplayName = "UI List element applies ordered values")]
        public void EditSetting_ApplyListElement_UpdatesPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateListPolicy();
            var editSetting = new EditSettingTestable();
            editSetting.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            editSetting.ElementControls["ListElem"].Tag = new List<string> { "A", "B" };
            editSetting.EnableAndApply();
            PolAssert.HasSequentialListValues(polFile, policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, new List<string> { "A", "B" });
        }

        /// <summary>
        /// Selecting an enum item in the UI should map to the correct numeric registry data.
        /// </summary>
        [Fact(DisplayName = "UI Enum element applies numeric value")]
        public void EditSetting_ApplyEnumElement_UpdatesPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateEnumPolicy();
            var editSetting = new EditSettingTestable();
            editSetting.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            ((ComboBox)editSetting.ElementControls["EnumElem"]).SelectedIndex = 1;
            editSetting.EnableAndApply();
            PolAssert.HasDwordValue(polFile, policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, 2U);
        }

        /// <summary>
        /// MultiText input (lines) should be written as a REG_MULTI_SZ value.
        /// </summary>
        [Fact(DisplayName = "UI MultiText element applies multi-string value")]
        public void EditSetting_ApplyMultiTextElement_UpdatesPolFile()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateMultiTextPolicy();
            var editSetting = new EditSettingTestable();
            editSetting.SetTestContext(policy, AdmxPolicySection.Machine, polFile);
            ((TextBox)editSetting.ElementControls["MultiTextElem"]).Lines = new[] { "line1", "line2" };
            editSetting.EnableAndApply();
            PolAssert.HasMultiStringValue(polFile, policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, new[] { "line1", "line2" });
        }
    }

    public class EditSettingTestable : EditSetting
    {
        public new Dictionary<string, Control> ElementControls => base.ElementControls;
        public new RadioButton EnabledOption => base.EnabledOption;
        public new RadioButton NotConfiguredOption => base.NotConfiguredOption;
        public new RadioButton DisabledOption => base.DisabledOption;

        /// <summary>
        /// Injects a minimal context (policy, sources, loaders, comments) so tests can drive the form logic without UI.
        /// </summary>
        public void SetTestContext(PolicyPlusPolicy policy, AdmxPolicySection section, IPolicySource polFile)
        {
            var workspace = new AdmxBundle();
            var loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            var comments = new Dictionary<string, string>();
            if (policy.Presentation == null)
            {
                policy.Presentation = new Presentation { Elements = new List<PresentationElement>() };
                policy.Presentation.Elements.Add(new TextBoxPresentationElement
                {
                    ID = "TextElem",
                    ElementType = "textBox",
                    Label = "Test Label",
                    DefaultValue = string.Empty
                });
            }
            if (policy.RawPolicy.AffectedValues == null)
                policy.RawPolicy.AffectedValues = new PolicyRegistryList();
            if (policy.RawPolicy.Elements == null)
            {
                policy.RawPolicy.Elements = new List<PolicyElement>();
                policy.RawPolicy.Elements.Add(new TextPolicyElement
                {
                    ID = "TextElem",
                    ElementType = "text",
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "TextValue",
                    MaxLength = 100
                });
            }
            var editSettingType = typeof(EditSetting);
            editSettingType.GetField("CurrentSetting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, policy);
            editSettingType.GetField("CurrentSection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, section);
            editSettingType.GetField("AdmxWorkspace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, workspace);
            editSettingType.GetField("CompPolSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, polFile);
            editSettingType.GetField("UserPolSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, polFile);
            editSettingType.GetField("CompPolLoader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, loader);
            editSettingType.GetField("UserPolLoader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, loader);
            editSettingType.GetField("CompComments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, comments);
            editSettingType.GetField("UserComments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, comments);
            editSettingType.GetField("languageCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(this, "en-US");
            PreparePolicyElements();
            PreparePolicyState();
            EnabledOption.Checked = true;
            NotConfiguredOption.Checked = false;
            DisabledOption.Checked = false;
            InvokeStateRadiosChanged();
        }

        public void SetTextElementValue(string elemId, string value)
        {
            if (ElementControls.ContainsKey(elemId) && ElementControls[elemId] is TextBox tb)
                tb.Text = value;
            else if (!ElementControls.ContainsKey(elemId))
                ElementControls.Add(elemId, new TextBox { Text = value });
        }

        public void EnableAndApply()
        {
            EnabledOption.Checked = true;
            InvokeStateRadiosChanged();
            ApplyToPolicySource();
        }

        public void InvokeStateRadiosChanged()
        {
            var method = typeof(EditSetting).GetMethod("StateRadiosChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(this, new object[] { null, null });
        }

        public void ApplyToPolicySource_PublicForTest() => ApplyToPolicySource();
    }
}
