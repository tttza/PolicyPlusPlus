using System;
using System.Collections.Generic;
using System.IO;

namespace PolicyPlus
{
    public enum PolicyTargetScope
    {
        Machine,
        User
    }

    public sealed class PolicyChangeRequest
    {
        public string PolicyId { get; set; } = string.Empty;
        public PolicyTargetScope Scope { get; set; } = PolicyTargetScope.Machine;
        public PolicyState DesiredState { get; set; } = PolicyState.NotConfigured;
        public Dictionary<string, object>? Options { get; set; }
    }

    public sealed class PolicySaveBuffers
    {
        public byte[]? MachinePolBytes { get; set; }
        public byte[]? UserPolBytes { get; set; }
    }

    public static class PolicySavePipeline
    {
        // Build updated Local GPO POL buffers by applying the requested changes in-memory
        public static PolicySaveBuffers BuildLocalGpoBuffers(AdmxBundle bundle, IEnumerable<PolicyChangeRequest> changes)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            PolFile? compPol = null;
            PolFile? userPol = null;
            try { compPol = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource() as PolFile; } catch { compPol = new PolFile(); }
            try { userPol = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource() as PolFile; } catch { userPol = new PolFile(); }

            foreach (var c in changes)
            {
                if (c == null || string.IsNullOrEmpty(c.PolicyId)) continue;
                if (!bundle.Policies.TryGetValue(c.PolicyId, out var pol)) continue;
                IPolicySource? target = c.Scope == PolicyTargetScope.User ? userPol : compPol;
                if (target == null) continue;
                PolicyProcessing.ForgetPolicy(target, pol);
                if (c.DesiredState == PolicyState.Enabled)
                {
                    PolicyProcessing.SetPolicyState(target, pol, PolicyState.Enabled, c.Options ?? new Dictionary<string, object>());
                }
                else if (c.DesiredState == PolicyState.Disabled)
                {
                    PolicyProcessing.SetPolicyState(target, pol, PolicyState.Disabled, new Dictionary<string, object>());
                }
                else
                {
                    // Not configured => just ForgetPolicy already cleared state
                }
            }

            byte[]? compBytes = null;
            byte[]? userBytes = null;
            if (compPol != null)
            {
                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.Unicode, true)) { compPol.Save(bw); }
                compBytes = ms.ToArray();
            }
            if (userPol != null)
            {
                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, System.Text.Encoding.Unicode, true)) { userPol.Save(bw); }
                userBytes = ms.ToArray();
            }

            return new PolicySaveBuffers { MachinePolBytes = compBytes, UserPolBytes = userBytes };
        }

        public static (string? machineBase64, string? userBase64) BuildLocalGpoBase64(AdmxBundle bundle, IEnumerable<PolicyChangeRequest> changes)
        {
            var buffers = BuildLocalGpoBuffers(bundle, changes);
            string? m = buffers.MachinePolBytes != null ? Convert.ToBase64String(buffers.MachinePolBytes) : null;
            string? u = buffers.UserPolBytes != null ? Convert.ToBase64String(buffers.UserPolBytes) : null;
            return (m, u);
        }
    }
}
