using Microsoft.UI.Xaml;

using PolicyPlus.Core.Core;
using PolicyPlus.Core.IO;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.Windows;

using System;
using System.Collections.Generic;
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

        private async Task OpenEditDialogForPolicyAsync(PolicyPlusPolicy representative, bool ensureFront = false)
        {
            if (_bundle is null) return;
            if (_compSource is null || _userSource is null || _loader is null)
            { _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false); _compSource = _loader.OpenSource(); _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(); }

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

            var compLoader = _useTempPol && _tempPolCompPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolCompPath, false)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            var userLoader = _useTempPol && _tempPolUserPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolUserPath, true)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true);

            var win = new EditSettingWindow();
            win.Initialize(targetPolicy,
                initialSection,
                _bundle!, _compSource!, _userSource!,
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
            if (_compSource is null || _userSource is null || _loader is null)
            { _loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false); _compSource = _loader.OpenSource(); _userSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(); }

            if (!_bundle.Policies.TryGetValue(policyId, out var policy))
            {
                policy = _allPolicies.FirstOrDefault(p => p.UniqueID == policyId);
                if (policy == null) return;
            }

            var compLoader = _useTempPol && _tempPolCompPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolCompPath, false)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            var userLoader = _useTempPol && _tempPolUserPath != null
                ? new PolicyLoader(PolicyLoaderSource.PolFile, _tempPolUserPath, true)
                : new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true);

            var win = new EditSettingWindow();
            win.Initialize(policy, preferredSection, _bundle!, _compSource!, _userSource!, compLoader, userLoader, _compComments, _userComments);
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
