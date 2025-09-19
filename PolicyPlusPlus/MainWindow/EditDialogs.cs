using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.Windows;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private void MarkDirty()
        {
            UpdateUnsavedIndicator();
        }

        private PolicyLoader CreateLoaderFor(AdmxPolicySection section)
        {
            var mode = PolicySourceManager.Instance.Mode;
            if (mode == PolicySourceMode.TempPol)
            {
                return new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, section == AdmxPolicySection.User);
            }
            if (mode == PolicySourceMode.CustomPol)
            {
                var path = section == AdmxPolicySection.User ? PolicySourceManager.Instance.CustomUserPath : PolicySourceManager.Instance.CustomCompPath;
                if (!string.IsNullOrEmpty(path))
                    return new PolicyLoader(PolicyLoaderSource.PolFile, path!, section == AdmxPolicySection.User);
            }
            return new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, section == AdmxPolicySection.User);
        }

        private async Task OpenEditDialogForPolicyAsync(PolicyPlusPolicy representative, bool ensureFront = false)
        {
            if (_bundle is null) return;
            var ctx = PolicySourceAccessor.Acquire();

            var displayName = representative.DisplayName;
            _nameGroups.TryGetValue(displayName, out var groupList);
            groupList ??= _allPolicies.Where(p => string.Equals(p.DisplayName, displayName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            PolicyPlusPolicy targetPolicy = groupList.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.User)
                                        ?? groupList.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Machine)
                                        ?? representative;

            var initialSection = targetPolicy.RawPolicy.Section == AdmxPolicySection.Both
                ? AdmxPolicySection.User
                : targetPolicy.RawPolicy.Section;

            if (App.TryActivateExistingEdit(targetPolicy.UniqueID))
                return;

            var compLoader = CreateLoaderFor(AdmxPolicySection.Machine);
            var userLoader = CreateLoaderFor(AdmxPolicySection.User);

            var win = new EditSettingWindow();
            win.Initialize(targetPolicy,
                initialSection,
                _bundle!, ctx.Comp, ctx.User,
                compLoader, userLoader,
                _compComments, _userComments);
            win.Saved += (s, e) => MarkDirty();
            win.Activate();
            WindowHelpers.BringToFront(win);

            if (ensureFront)
            {
                await Task.Delay(150);
                try { WindowHelpers.BringToFront(win); } catch { }
            }
        }

        public async Task OpenEditDialogForPolicyIdAsync(string policyId, bool ensureFront)
        {
            if (_bundle == null) return;
            var ctx = PolicySourceAccessor.Acquire();
            PolicyPlusPolicy? representative = _allPolicies.FirstOrDefault(p => p.UniqueID == policyId);
            if (representative == null)
            {
                if (!_bundle.Policies.TryGetValue(policyId, out var fromBundle)) return;
                representative = fromBundle;
            }
            await OpenEditDialogForPolicyAsync(representative, ensureFront);
        }

        public async Task OpenEditDialogForPolicyIdAsync(string policyId, AdmxPolicySection preferredSection, bool ensureFront)
        {
            if (_bundle == null) return;
            var ctx = PolicySourceAccessor.Acquire();

            if (!_bundle.Policies.TryGetValue(policyId, out var policy))
            {
                policy = _allPolicies.FirstOrDefault(p => p.UniqueID == policyId);
                if (policy == null) return;
            }

            var compLoader = CreateLoaderFor(AdmxPolicySection.Machine);
            var userLoader = CreateLoaderFor(AdmxPolicySection.User);

            var win = new EditSettingWindow();
            win.Initialize(policy, preferredSection, _bundle!, ctx.Comp, ctx.User, compLoader, userLoader, _compComments, _userComments);
            win.Saved += (s, e) => MarkDirty();
            win.Activate();
            WindowHelpers.BringToFront(win);
            if (ensureFront)
            {
                await Task.Delay(150);
                try { WindowHelpers.BringToFront(win); } catch { }
            }
        }
    }
}
