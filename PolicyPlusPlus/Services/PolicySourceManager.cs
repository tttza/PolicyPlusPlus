using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Logging;
using PolicyPlusPlus.Services;
using PolicyPlusCore.Admx; // AdmxBundle

namespace PolicyPlusPlus.Services
{
    /// <summary>
    /// Central manager for current policy source mode and active sources.
    /// Abstracts switching between Local GPO, temp POL, and custom POL files.
    /// </summary>
    internal sealed class PolicySourceManager
    {
        public enum PolicySourceMode { LocalGpo, TempPol, CustomPol }

        public static PolicySourceManager Instance { get; } = new();

        private PolicySourceManager() { }

        public PolicySourceMode Mode { get; private set; } = PolicySourceMode.LocalGpo;
        public IPolicySource? CompSource { get; private set; }
        public IPolicySource? UserSource { get; private set; }
        public string? CustomCompPath { get; private set; }
        public string? CustomUserPath { get; private set; }
        public string? TempCompPath { get; private set; }
        public string? TempUserPath { get; private set; }
        public bool UseTempPol => Mode == PolicySourceMode.TempPol;
        public bool UseCustomPol => Mode == PolicySourceMode.CustomPol;

        public event EventHandler? SourcesChanged;

        private void RaiseChanged() { try { SourcesChanged?.Invoke(this, EventArgs.Empty); } catch { } }

        public void EnsureLocalGpo()
        {
            try
            {
                var loader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
                CompSource = loader.OpenSource();
                UserSource = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource();
                Mode = PolicySourceMode.LocalGpo;
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "EnsureLocalGpo failed", ex); }
            RaiseChanged();
        }

        private void EnsureTempPaths()
        {
            try
            {
                var baseDir = Path.Combine(Path.GetTempPath(), "PolicyPlus");
                Directory.CreateDirectory(baseDir);
                if (string.IsNullOrEmpty(TempCompPath))
                {
                    TempCompPath = Path.Combine(baseDir, "machine.pol");
                    if (!File.Exists(TempCompPath)) new PolFile().Save(TempCompPath);
                }
                if (string.IsNullOrEmpty(TempUserPath))
                {
                    TempUserPath = Path.Combine(baseDir, "user.pol");
                    if (!File.Exists(TempUserPath)) new PolFile().Save(TempUserPath);
                }
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "EnsureTempPaths failed", ex); }
        }

        public void SwitchToTempPol()
        {
            EnsureTempPaths();
            try
            {
                var compLoader = new PolicyLoader(PolicyLoaderSource.PolFile, TempCompPath ?? string.Empty, false);
                var userLoader = new PolicyLoader(PolicyLoaderSource.PolFile, TempUserPath ?? string.Empty, true);
                CompSource = compLoader.OpenSource();
                UserSource = userLoader.OpenSource();
                Mode = PolicySourceMode.TempPol;
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "SwitchToTempPol failed", ex); }
            RaiseChanged();
        }

        private static void EnsurePolFileExists(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (!File.Exists(path)) new PolFile().Save(path); } catch (Exception ex) { Log.Warn("PolicySourceMgr", $"EnsurePolFileExists fail path={path}", ex); }
        }

        public void SwitchToCustomPol(string compPath, string userPath)
        {
            if (string.IsNullOrWhiteSpace(compPath) || string.IsNullOrWhiteSpace(userPath)) return;
            CustomCompPath = compPath; CustomUserPath = userPath;
            EnsurePolFileExists(CustomCompPath); EnsurePolFileExists(CustomUserPath);
            try
            {
                var compLoader = new PolicyLoader(PolicyLoaderSource.PolFile, CustomCompPath, false);
                var userLoader = new PolicyLoader(PolicyLoaderSource.PolFile, CustomUserPath, true);
                CompSource = compLoader.OpenSource();
                UserSource = userLoader.OpenSource();
                Mode = PolicySourceMode.CustomPol;
            }
            catch (Exception ex) { Log.Warn("PolicySourceMgr", "SwitchToCustomPol failed", ex); }
            RaiseChanged();
        }

        public void Refresh()
        {
            if (Mode == PolicySourceMode.LocalGpo) EnsureLocalGpo();
            else if (Mode == PolicySourceMode.TempPol) SwitchToTempPol();
            else if (Mode == PolicySourceMode.CustomPol && !string.IsNullOrEmpty(CustomCompPath) && !string.IsNullOrEmpty(CustomUserPath)) SwitchToCustomPol(CustomCompPath, CustomUserPath);
        }

        /// <summary>
        /// Apply pending changes to current sources according to mode.
        /// LocalGpo uses elevation pipeline; TempPol & CustomPol modify in-memory PolFile sources then save.
        /// </summary>
        public async System.Threading.Tasks.Task<(bool ok, string? error)> ApplyPendingAsync(AdmxBundle bundle, PendingChange[] items, IElevationService elevation)
        {
            if (items == null || items.Length == 0) return (true, null);
            if (bundle == null) return (false, "No ADMX bundle loaded");

            if (Mode == PolicySourceMode.LocalGpo)
            {
                var saveResult = await SaveChangesCoordinator.SaveAsync(bundle, items, elevation, TimeSpan.FromSeconds(8), triggerRefresh: true);
                return (saveResult.ok, saveResult.error);
            }

            // Temp or Custom => direct pol modification
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
                else if (string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase)) apply(userPol);
                else apply(compPol);
            }
            // Persist to disk
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
