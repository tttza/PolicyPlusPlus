using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace PolicyPlusPlus.Services
{
    public enum PolicySourceMode { LocalGpo, TempPol, CustomPol }

    public interface IPolicySourceManager
    {
        PolicySourceMode Mode { get; }
        IPolicySource? CompSource { get; }
        IPolicySource? UserSource { get; }
        string? CustomCompPath { get; }
        string? CustomUserPath { get; }
        string? TempCompPath { get; }
        string? TempUserPath { get; }
        event EventHandler? SourcesChanged;
        bool Switch(PolicySourceDescriptor descriptor);
        bool SwitchCustomPolFlexible(string? compPath, string? userPath, bool allowSingle);
        void Refresh();
        System.Threading.Tasks.Task<(bool ok, string? error)> ApplyPendingAsync(AdmxBundle bundle, PendingChange[] items, IElevationService elevation);
    }

    public readonly struct PolicySourceDescriptor
    {
        public PolicySourceMode Mode { get; }
        public string? ComputerPath { get; }
        public string? UserPath { get; }
        private PolicySourceDescriptor(PolicySourceMode mode, string? comp, string? user)
        { Mode = mode; ComputerPath = comp; UserPath = user; }
        public static PolicySourceDescriptor LocalGpo() => new(PolicySourceMode.LocalGpo, null, null);
        public static PolicySourceDescriptor TempPol() => new(PolicySourceMode.TempPol, null, null);
        public static PolicySourceDescriptor Custom(string compPath, string userPath) => new(PolicySourceMode.CustomPol, compPath, userPath);
    }

    internal interface IPolicySourceStrategy
    {
        bool Activate(PolicySourceManager mgr, PolicySourceDescriptor descriptor);
    }

    internal sealed class LocalGpoStrategy : IPolicySourceStrategy
    {
        public bool Activate(PolicySourceManager mgr, PolicySourceDescriptor d)
        {
            try
            {
                var loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                mgr.CompSource = loader.OpenSource();
                mgr.UserSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                mgr.Mode = PolicySourceMode.LocalGpo;
                return true;
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "LocalGpoStrategy", ex); return false; }
        }
    }

    internal sealed class TempPolStrategy : IPolicySourceStrategy
    {
        public bool Activate(PolicySourceManager mgr, PolicySourceDescriptor d)
        {
            try
            {
                mgr.EnsureTempPaths();
                var compLoader = new PolicyLoader(PolicyLoaderSource.PolFile, mgr.TempCompPath ?? string.Empty, false);
                var userLoader = new PolicyLoader(PolicyLoaderSource.PolFile, mgr.TempUserPath ?? string.Empty, true);
                mgr.CompSource = compLoader.OpenSource();
                mgr.UserSource = userLoader.OpenSource();
                mgr.Mode = PolicySourceMode.TempPol;
                return true;
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "TempPolStrategy", ex); return false; }
        }
    }

    internal sealed class CustomPolStrategy : IPolicySourceStrategy
    {
        public bool Activate(PolicySourceManager mgr, PolicySourceDescriptor d)
        {
            if (string.IsNullOrWhiteSpace(d.ComputerPath) || string.IsNullOrWhiteSpace(d.UserPath)) return false;
            return mgr.SwitchCustomPolFlexible(d.ComputerPath, d.UserPath, allowSingle: false);
        }
    }

    internal sealed class PolicySourceManager : IPolicySourceManager
    {
        public static IPolicySourceManager Instance { get; } = new PolicySourceManager();
        private PolicySourceManager()
        {
            _strategies = new Dictionary<PolicySourceMode, IPolicySourceStrategy>
        {
            { PolicySourceMode.LocalGpo, new LocalGpoStrategy() },
            { PolicySourceMode.TempPol, new TempPolStrategy() },
            { PolicySourceMode.CustomPol, new CustomPolStrategy() }
        };
        }

        private readonly Dictionary<PolicySourceMode, IPolicySourceStrategy> _strategies;

        public PolicySourceMode Mode { get; internal set; } = PolicySourceMode.LocalGpo;
        public IPolicySource? CompSource { get; internal set; }
        public IPolicySource? UserSource { get; internal set; }
        public string? CustomCompPath { get; internal set; }
        public string? CustomUserPath { get; internal set; }
        public string? TempCompPath { get; internal set; }
        public string? TempUserPath { get; internal set; }
        public event EventHandler? SourcesChanged;
        private void RaiseChanged() { try { SourcesChanged?.Invoke(this, EventArgs.Empty); } catch { } }

        public bool Switch(PolicySourceDescriptor descriptor)
        {
            if (!_strategies.TryGetValue(descriptor.Mode, out var strategy)) return false;
            var ok = strategy.Activate(this, descriptor);
            if (ok) RaiseChanged();
            return ok;
        }

        internal void EnsureTempPaths()
        {
            try
            {
                var baseDir = Path.Combine(Path.GetTempPath(), "PolicyPlus");
                Directory.CreateDirectory(baseDir);
                if (string.IsNullOrEmpty(TempCompPath))
                { TempCompPath = Path.Combine(baseDir, "machine.pol"); if (!File.Exists(TempCompPath)) new PolFile().Save(TempCompPath); }
                if (string.IsNullOrEmpty(TempUserPath))
                { TempUserPath = Path.Combine(baseDir, "user.pol"); if (!File.Exists(TempUserPath)) new PolFile().Save(TempUserPath); }
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "EnsureTempPaths", ex); }
        }

        private static void EnsurePolFileExists(string? path)
        { if (string.IsNullOrEmpty(path)) return; try { if (!File.Exists(path)) new PolFile().Save(path); } catch (Exception ex) { Log.Warn("PolicySourceMgr", "EnsurePolFileExists", ex); } }

        public bool SwitchCustomPolFlexible(string? compPath, string? userPath, bool allowSingle)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(compPath) && string.IsNullOrWhiteSpace(userPath)) return false;
                string? finalComp = compPath;
                string? finalUser = userPath;
                if ((string.IsNullOrWhiteSpace(finalComp) || string.IsNullOrWhiteSpace(finalUser)) && allowSingle)
                {
                    var baseDir = Path.Combine(Path.GetTempPath(), "PolicyPlus");
                    Directory.CreateDirectory(baseDir);
                    if (string.IsNullOrWhiteSpace(finalComp)) { finalComp = Path.Combine(baseDir, "_empty_machine.pol"); EnsurePolFileExists(finalComp); }
                    if (string.IsNullOrWhiteSpace(finalUser)) { finalUser = Path.Combine(baseDir, "_empty_user.pol"); EnsurePolFileExists(finalUser); }
                }
                if (string.IsNullOrWhiteSpace(finalComp) || string.IsNullOrWhiteSpace(finalUser)) return false;
                CustomCompPath = finalComp; CustomUserPath = finalUser;
                EnsurePolFileExists(CustomCompPath); EnsurePolFileExists(CustomUserPath);
                var compLoader = new PolicyLoader(PolicyLoaderSource.PolFile, CustomCompPath, false);
                var userLoader = new PolicyLoader(PolicyLoaderSource.PolFile, CustomUserPath, true);
                CompSource = compLoader.OpenSource();
                UserSource = userLoader.OpenSource();
                Mode = PolicySourceMode.CustomPol;
                RaiseChanged();
                return true;
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "SwitchCustomPolFlexible", ex); return false; }
        }

        public void Refresh()
        {
            if (Mode == PolicySourceMode.LocalGpo) Switch(PolicySourceDescriptor.LocalGpo());
            else if (Mode == PolicySourceMode.TempPol) Switch(PolicySourceDescriptor.TempPol());
            else if (Mode == PolicySourceMode.CustomPol && !string.IsNullOrEmpty(CustomCompPath) && !string.IsNullOrEmpty(CustomUserPath)) SwitchCustomPolFlexible(CustomCompPath, CustomUserPath, false);
        }

        public async System.Threading.Tasks.Task<(bool ok, string? error)> ApplyPendingAsync(AdmxBundle bundle, PendingChange[] items, IElevationService elevation)
        {
            if (items == null || items.Length == 0) return (true, null);
            if (bundle == null) return (false, "No ADMX bundle loaded");
            if (Mode == PolicySourceMode.LocalGpo)
            {
                var saveResult = await SaveChangesCoordinator.SaveAsync(bundle, items, elevation, TimeSpan.FromSeconds(8), triggerRefresh: true);
                return (saveResult.ok, saveResult.error);
            }
            var compPol = CompSource as PolFile;
            var userPol = UserSource as PolFile;
            if (compPol == null || userPol == null) return (false, "Current sources not writable POL files");
            foreach (var c in items)
            {
                if (!bundle.Policies.TryGetValue(c.PolicyId, out var pol)) continue;
                void apply(IPolicySource src)
                {
                    PolicyProcessing.ForgetPolicy(src, pol);
                    if (c.DesiredState == PolicyState.Enabled || c.DesiredState == PolicyState.Disabled)
                        PolicyProcessing.SetPolicyState(src, pol, c.DesiredState, c.Options ?? new Dictionary<string, object>());
                }
                if (string.Equals(c.Scope, "Both", StringComparison.OrdinalIgnoreCase)) { apply(compPol); apply(userPol); }
                else if (string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase)) apply(userPol); else apply(compPol);
            }
            try
            {
                if (Mode == PolicySourceMode.CustomPol)
                {
                    if (!string.IsNullOrEmpty(CustomCompPath)) compPol.Save(CustomCompPath);
                    if (!string.IsNullOrEmpty(CustomUserPath)) userPol.Save(CustomUserPath);
                }
                else if (Mode == PolicySourceMode.TempPol)
                {
                    if (!string.IsNullOrEmpty(TempCompPath)) compPol.Save(TempCompPath);
                    if (!string.IsNullOrEmpty(TempUserPath)) userPol.Save(TempUserPath);
                }
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "Persist POL failed", ex); return (false, ex.Message); }
            RaiseChanged();
            return (true, null);
        }
    }
}
