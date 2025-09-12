using PolicyPlusCore.Admx;
using PolicyPlusCore.IO;
using System.Diagnostics;

namespace PolicyPlusCore.Core
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
        private static void LogDebug(string msg)
        {
            try { Debug.WriteLine("[PolicySavePipeline] " + msg); } catch { }
        }
        private static void LogError(string msg, Exception ex)
        {
            try { Debug.WriteLine($"[PolicySavePipeline] ERROR {msg} :: {ex.GetType().Name} {ex.Message}"); } catch { }
        }

        private static PolFile LoadExistingOrNew(bool isUser)
        {
            try
            {
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string gpRoot = Path.Combine(win, "System32", "GroupPolicy");
                string path = Path.Combine(gpRoot, isUser ? "User" : "Machine", "Registry.pol");
                if (File.Exists(path))
                {
                    return PolFile.Load(path);
                }
            }
            catch (Exception ex)
            {
                LogError($"LoadExistingOrNew failed isUser={isUser}", ex);
            }
            return new PolFile();
        }

        // Build updated Local GPO POL buffers by applying the requested changes in-memory
        public static PolicySaveBuffers BuildLocalGpoBuffers(AdmxBundle bundle, IEnumerable<PolicyChangeRequest> changes)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            PolFile? compPol = null;
            PolFile? userPol = null;
            try
            {
                var compSrc = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource();
                compPol = compSrc as PolFile;
            }
            catch (Exception ex)
            {
                compPol = null; LogError("OpenSource machine failed", ex);
            }
            try
            {
                var userSrc = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                userPol = userSrc as PolFile;
            }
            catch (Exception ex)
            {
                userPol = null; LogError("OpenSource user failed", ex);
            }

            if (compPol == null) compPol = LoadExistingOrNew(isUser: false);
            if (userPol == null) userPol = LoadExistingOrNew(isUser: true);

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
                    // Not configured => ForgetPolicy already cleared state
                }
            }

            byte[]? compBytes = null;
            byte[]? userBytes = null;
            if (compPol != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    using (var bw = new BinaryWriter(ms, System.Text.Encoding.Unicode, true)) { compPol.Save(bw); }
                    compBytes = ms.ToArray();
                }
                catch (Exception ex)
                {
                    LogError("Serialize machine POL failed", ex);
                }
            }
            if (userPol != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    using (var bw = new BinaryWriter(ms, System.Text.Encoding.Unicode, true)) { userPol.Save(bw); }
                    userBytes = ms.ToArray();
                }
                catch (Exception ex)
                {
                    LogError("Serialize user POL failed", ex);
                }
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
