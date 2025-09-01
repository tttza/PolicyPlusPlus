using PolicyPlus;
using PolicyPlus.WinUI3.Services;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace PolicyPlus.WinUI3.Models
{
    public sealed class PolicyListRow
    {
        public string DisplayName { get; }
        public bool IsCategory { get; }
        public PolicyPlusPolicy? Policy { get; }
        public PolicyPlusCategory? Category { get; }
        public bool UserConfigured { get; }
        public bool ComputerConfigured { get; }

        // Detailed state flags for icons
        public bool UserEnabled { get; }
        public bool UserDisabled { get; }
        public bool ComputerEnabled { get; }
        public bool ComputerDisabled { get; }

        // Glyphs
        public string UserGlyph { get; }
        public string ComputerGlyph { get; }

        // Brushes for glyph coloring (nullable for test environments without WinUI runtime)
        public Brush? UserBrush { get; }
        public Brush? ComputerBrush { get; }

        // Optional column values
        public string UniqueId { get; }
        public string CategoryName { get; }
        public string AppliesText { get; }
        public string SupportedText { get; }
        public string UserStateText { get; }
        public string ComputerStateText { get; }

        // Display-only convenience: part after ':' of UniqueId
        public string ShortId
        {
            get
            {
                if (string.IsNullOrEmpty(UniqueId)) return string.Empty;
                int idx = UniqueId.LastIndexOf(':');
                return idx >= 0 && idx + 1 < UniqueId.Length ? UniqueId.Substring(idx + 1) : UniqueId;
            }
        }

        private PolicyListRow(string displayName, bool isCategory, PolicyPlusPolicy? policy, PolicyPlusCategory? category,
            bool userConfigured, bool computerConfigured, bool userEnabled, bool userDisabled, bool compEnabled, bool compDisabled,
            string userGlyph, string compGlyph, Brush? userBrush, Brush? compBrush,
            string uniqueId, string categoryName, string appliesText, string supportedText, string userStateText, string computerStateText)
        {
            DisplayName = displayName;
            IsCategory = isCategory;
            Policy = policy;
            Category = category;
            UserConfigured = userConfigured;
            ComputerConfigured = computerConfigured;
            UserEnabled = userEnabled;
            UserDisabled = userDisabled;
            ComputerEnabled = compEnabled;
            ComputerDisabled = compDisabled;
            UserGlyph = userGlyph;
            ComputerGlyph = compGlyph;
            UserBrush = userBrush;
            ComputerBrush = compBrush;
            UniqueId = uniqueId;
            CategoryName = categoryName;
            AppliesText = appliesText;
            SupportedText = supportedText;
            UserStateText = userStateText;
            ComputerStateText = computerStateText;
        }

        public static PolicyListRow FromCategory(PolicyPlusCategory c)
            => new PolicyListRow(c.DisplayName, true, null, c,
                false, false, false, false, false, false,
                string.Empty, string.Empty, null, null,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        private static Brush? TryGetResourceBrush(string key)
        {
            try
            {
                var obj = Application.Current?.Resources?[key];
                if (obj is Brush b) return b;
            }
            catch { }
            return null;
        }

        private static string AppliesOf(PolicyPlusPolicy p)
        {
            return p.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "Computer",
                AdmxPolicySection.User => "User",
                _ => "Both"
            };
        }

        private static string StateText(bool enabled, bool disabled, bool configured)
        {
            if (!configured) return "Not configured";
            if (enabled) return "Enabled";
            if (disabled) return "Disabled";
            return string.Empty;
        }

        public static PolicyListRow FromPolicy(PolicyPlusPolicy p, IPolicySource? comp, IPolicySource? user)
        {
            // Start with actual state
            bool userCfg = false, compCfg = false;
            bool userEn = false, userDis = false, compEn = false, compDis = false;
            try
            {
                if (user != null)
                {
                    var st = PolicyProcessing.GetPolicyState(user, p);
                    userCfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                    userEn = st == PolicyState.Enabled; userDis = st == PolicyState.Disabled;
                }
                if (comp != null)
                {
                    var st = PolicyProcessing.GetPolicyState(comp, p);
                    compCfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                    compEn = st == PolicyState.Enabled; compDis = st == PolicyState.Disabled;
                }
            }
            catch { }

            // Overlay pending desired states if present
            try
            {
                var pend = PendingChangesService.Instance.Pending;
                var pendUser = pend.FirstOrDefault(pc => pc.PolicyId == p.UniqueID && pc.Scope == "User");
                if (pendUser != null)
                {
                    userCfg = pendUser.DesiredState == PolicyState.Enabled || pendUser.DesiredState == PolicyState.Disabled;
                    userEn = pendUser.DesiredState == PolicyState.Enabled; userDis = pendUser.DesiredState == PolicyState.Disabled;
                }
                var pendComp = pend.FirstOrDefault(pc => pc.PolicyId == p.UniqueID && pc.Scope == "Computer");
                if (pendComp != null)
                {
                    compCfg = pendComp.DesiredState == PolicyState.Enabled || pendComp.DesiredState == PolicyState.Disabled;
                    compEn = pendComp.DesiredState == PolicyState.Enabled; compDis = pendComp.DesiredState == PolicyState.Disabled;
                }
            }
            catch { }

            string userGlyph = userEn ? "\uE73E" : (userDis ? "\uE711" : string.Empty);
            string compGlyph = compEn ? "\uE73E" : (compDis ? "\uE711" : string.Empty);

            // Choose colors from resources when available; otherwise leave null (use default)
            Brush? apply = TryGetResourceBrush("ApplyBrush");
            Brush? danger = TryGetResourceBrush("DangerBrush");
            Brush? userBrush = userEn ? (apply ?? null) : (userDis ? (danger ?? null) : null);
            Brush? compBrush = compEn ? (apply ?? null) : (compDis ? (danger ?? null) : null);

            string categoryName = p.Category?.DisplayName ?? string.Empty;
            string appliesText = AppliesOf(p);
            string supportedText = p.SupportedOn?.DisplayName ?? string.Empty;
            string userStateText = StateText(userEn, userDis, userCfg);
            string compStateText = StateText(compEn, compDis, compCfg);

            return new PolicyListRow(p.DisplayName, false, p, null,
                userCfg, compCfg, userEn, userDis, compEn, compDis,
                userGlyph, compGlyph, userBrush, compBrush,
                p.UniqueID, categoryName, appliesText, supportedText, userStateText, compStateText);
        }

        // Aggregate variant states (User/Machine/Both) for a display-name group
        public static PolicyListRow FromGroup(PolicyPlusPolicy representative, IEnumerable<PolicyPlusPolicy> variants, IPolicySource? comp, IPolicySource? user)
        {
            bool anyUserConfigured = false, anyCompConfigured = false;
            bool anyUserEnabled = false, anyUserDisabled = false;
            bool anyCompEnabled = false, anyCompDisabled = false;

            // Evaluate actual states
            foreach (var v in variants)
            {
                try
                {
                    if (user != null && (v.RawPolicy.Section == AdmxPolicySection.User || v.RawPolicy.Section == AdmxPolicySection.Both))
                    {
                        var st = PolicyProcessing.GetPolicyState(user, v);
                        bool cfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                        anyUserConfigured |= cfg;
                        anyUserEnabled |= st == PolicyState.Enabled;
                        anyUserDisabled |= st == PolicyState.Disabled;
                    }
                    if (comp != null && (v.RawPolicy.Section == AdmxPolicySection.Machine || v.RawPolicy.Section == AdmxPolicySection.Both))
                    {
                        var st = PolicyProcessing.GetPolicyState(comp, v);
                        bool cfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                        anyCompConfigured |= cfg;
                        anyCompEnabled |= st == PolicyState.Enabled;
                        anyCompDisabled |= st == PolicyState.Disabled;
                    }
                }
                catch { }
            }

            // Overlay pending changes per variant
            try
            {
                var pending = PendingChangesService.Instance.Pending;
                foreach (var v in variants)
                {
                    var pendUser = pending.FirstOrDefault(pc => pc.PolicyId == v.UniqueID && pc.Scope == "User");
                    if (pendUser != null)
                    {
                        bool cfg = pendUser.DesiredState == PolicyState.Enabled || pendUser.DesiredState == PolicyState.Disabled;
                        anyUserConfigured |= cfg;
                        anyUserEnabled |= pendUser.DesiredState == PolicyState.Enabled;
                        anyUserDisabled |= pendUser.DesiredState == PolicyState.Disabled;
                    }
                    var pendComp = pending.FirstOrDefault(pc => pc.PolicyId == v.UniqueID && pc.Scope == "Computer");
                    if (pendComp != null)
                    {
                        bool cfg = pendComp.DesiredState == PolicyState.Enabled || pendComp.DesiredState == PolicyState.Disabled;
                        anyCompConfigured |= cfg;
                        anyCompEnabled |= pendComp.DesiredState == PolicyState.Enabled;
                        anyCompDisabled |= pendComp.DesiredState == PolicyState.Disabled;
                    }
                }
            }
            catch { }

            // Prefer Enabled over Disabled when both exist
            string userGlyph = anyUserEnabled ? "\uE73E" : (anyUserDisabled ? "\uE711" : string.Empty);
            string compGlyph = anyCompEnabled ? "\uE73E" : (anyCompDisabled ? "\uE711" : string.Empty);

            Brush? apply = TryGetResourceBrush("ApplyBrush");
            Brush? danger = TryGetResourceBrush("DangerBrush");
            Brush? userBrush = anyUserEnabled ? (apply ?? null) : (anyUserDisabled ? (danger ?? null) : null);
            Brush? compBrush = anyCompEnabled ? (apply ?? null) : (anyCompDisabled ? (danger ?? null) : null);

            string categoryName = representative.Category?.DisplayName ?? string.Empty;
            string appliesText = AppliesOf(representative);
            string supportedText = representative.SupportedOn?.DisplayName ?? string.Empty;
            string userStateText = StateText(anyUserEnabled, anyUserDisabled, anyUserConfigured);
            string compStateText = StateText(anyCompEnabled, anyCompDisabled, anyCompConfigured);

            return new PolicyListRow(representative.DisplayName, false, representative, null,
                anyUserConfigured, anyCompConfigured, anyUserEnabled, anyUserDisabled, anyCompEnabled, anyCompDisabled,
                userGlyph, compGlyph, userBrush, compBrush,
                representative.UniqueID, categoryName, appliesText, supportedText, userStateText, compStateText);
        }
    }
}
