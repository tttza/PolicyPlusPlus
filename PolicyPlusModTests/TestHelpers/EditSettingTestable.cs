using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;

namespace PolicyPlusModTests.TestHelpers
{
    // Lightweight test-only stand?in for the former UI edit dialog logic.
    // Provides just enough behavior for tests that assert presentation defaults
    // (boolean defaultChecked, decimal/text defaultValue) are applied when
    // enabling a policy without explicit user input.
    public class EditSettingTestable
    {
        private PolicyPlusPolicy _policy = null!;
        private IPolicySource _policySource = null!;
        private AdmxPolicySection _section;

        public RadioStub NotConfiguredOption { get; } = new();
        public RadioStub EnabledOption { get; } = new();
        public RadioStub DisabledOption { get; } = new();

        private PolicyState _currentState = PolicyState.NotConfigured;

        public void SetTestContext(PolicyPlusPolicy policy, AdmxPolicySection section, IPolicySource policySource)
        {
            _policy = policy;
            _section = section; // section not used yet but kept for parity
            _policySource = policySource;
        }

        public void InvokeStateRadiosChanged()
        {
            if (EnabledOption.Checked) _currentState = PolicyState.Enabled;
            else if (DisabledOption.Checked) _currentState = PolicyState.Disabled;
            else _currentState = PolicyState.NotConfigured;
        }

        public void ApplyToPolicySource_PublicForTest()
        {
            switch (_currentState)
            {
                case PolicyState.Enabled:
                    ApplyEnabled();
                    break;
                case PolicyState.Disabled:
                    PolicyProcessing.SetPolicyState(_policySource, _policy, PolicyState.Disabled, new Dictionary<string, object>());
                    break;
                default:
                    // NotConfigured => ForgetPolicy semantics (tests using this helper immediately switch to Enabled)
                    break;
            }
        }

        private void ApplyEnabled()
        {
            var optionValues = new Dictionary<string, object>();

            // Apply presentation defaults only for the element types covered by tests.
            var presentation = _policy.Presentation;
            if (presentation != null)
            {
                foreach (var elem in _policy.RawPolicy.Elements ?? Enumerable.Empty<PolicyElement>())
                {
                    var pres = presentation.Elements.FirstOrDefault(p => string.Equals(p.ID, elem.ID, StringComparison.OrdinalIgnoreCase));
                    if (pres == null) continue;
                    switch (elem.ElementType)
                    {
                        case "boolean":
                            if (pres is CheckBoxPresentationElement cb && cb.DefaultState)
                                optionValues[elem.ID] = true;
                            break;
                        case "decimal":
                            if (pres is NumericBoxPresentationElement nb)
                                optionValues[elem.ID] = nb.DefaultValue;
                            break;
                        case "text":
                            if (pres is TextBoxPresentationElement tb && tb.DefaultValue is { Length: > 0 })
                                optionValues[elem.ID] = tb.DefaultValue!;
                            break;
                    }
                }
            }

            PolicyProcessing.SetPolicyState(_policySource, _policy, PolicyState.Enabled, optionValues);
        }

        public class RadioStub
        {
            public bool Checked { get; set; }
        }
    }
}
