using PolicyPlusPlus.Services;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.ComponentModel;
using PolicyPlusCore.IO;
using PolicyPlusCore.Core;

namespace PolicyPlusPlus.Models
{
    public sealed class PolicyListRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly bool _isGroup;
        private readonly IEnumerable<PolicyPlusPolicy>? _variants; // when group
        private IPolicySource? _comp;
        private IPolicySource? _user;

        public string DisplayName { get; }
        public string SecondName { get; } = string.Empty;
        public bool IsCategory { get; }
        public PolicyPlusPolicy? Policy { get; }
        public PolicyPlusCategory? Category { get; }

        private bool _userConfigured; public bool UserConfigured { get => _userConfigured; private set { if (_userConfigured != value) { _userConfigured = value; OnPropertyChanged(nameof(UserConfigured)); } } }
        private bool _computerConfigured; public bool ComputerConfigured { get => _computerConfigured; private set { if (_computerConfigured != value) { _computerConfigured = value; OnPropertyChanged(nameof(ComputerConfigured)); } } }

        private bool _userEnabled; public bool UserEnabled { get => _userEnabled; private set { if (_userEnabled != value) { _userEnabled = value; OnPropertyChanged(nameof(UserEnabled)); } } }
        private bool _userDisabled; public bool UserDisabled { get => _userDisabled; private set { if (_userDisabled != value) { _userDisabled = value; OnPropertyChanged(nameof(UserDisabled)); } } }
        private bool _computerEnabled; public bool ComputerEnabled { get => _computerEnabled; private set { if (_computerEnabled != value) { _computerEnabled = value; OnPropertyChanged(nameof(ComputerEnabled)); } } }
        private bool _computerDisabled; public bool ComputerDisabled { get => _computerDisabled; private set { if (_computerDisabled != value) { _computerDisabled = value; OnPropertyChanged(nameof(ComputerDisabled)); } } }

        private string _userGlyph = string.Empty; public string UserGlyph { get => _userGlyph; private set { if (_userGlyph != value) { _userGlyph = value; OnPropertyChanged(nameof(UserGlyph)); } } }
        private string _computerGlyph = string.Empty; public string ComputerGlyph { get => _computerGlyph; private set { if (_computerGlyph != value) { _computerGlyph = value; OnPropertyChanged(nameof(ComputerGlyph)); } } }

        private Brush? _userBrush; public Brush? UserBrush { get => _userBrush; private set { if (!ReferenceEquals(_userBrush, value)) { _userBrush = value; OnPropertyChanged(nameof(UserBrush)); } } }
        private Brush? _computerBrush; public Brush? ComputerBrush { get => _computerBrush; private set { if (!ReferenceEquals(_computerBrush, value)) { _computerBrush = value; OnPropertyChanged(nameof(ComputerBrush)); } } }

        private bool _userPending; public bool UserPending { get => _userPending; private set { if (_userPending != value) { _userPending = value; OnPropertyChanged(nameof(UserPending)); } } }
        private bool _computerPending; public bool ComputerPending { get => _computerPending; private set { if (_computerPending != value) { _computerPending = value; OnPropertyChanged(nameof(ComputerPending)); } } }

        public string UniqueId { get; }
        public string CategoryName { get; } // immediate category (Parent Category column)
        public string TopCategoryName { get; } = string.Empty; // root-most category (Top Category column)
        public string CategoryFullPath { get; } = string.Empty; // full path root -> immediate
        public string AppliesText { get; }
        public string SupportedText { get; }
        private string _userStateText = string.Empty; public string UserStateText { get => _userStateText; private set { if (_userStateText != value) { _userStateText = value; OnPropertyChanged(nameof(UserStateText)); } } }
        private string _computerStateText = string.Empty; public string ComputerStateText { get => _computerStateText; private set { if (_computerStateText != value) { _computerStateText = value; OnPropertyChanged(nameof(ComputerStateText)); } } }

        private bool _isBookmarked; public bool IsBookmarked { get => _isBookmarked; private set { if (_isBookmarked != value) { _isBookmarked = value; OnPropertyChanged(nameof(IsBookmarked)); } } }

        public string ShortId
        {
            get
            {
                if (string.IsNullOrEmpty(UniqueId)) return string.Empty;
                int idx = UniqueId.LastIndexOf(':');
                return idx >= 0 && idx + 1 < UniqueId.Length ? UniqueId[(idx + 1)..] : UniqueId;
            }
        }

        private static string ComputeTopCategory(PolicyPlusCategory? cat)
        {
            if (cat == null) return string.Empty;
            while (cat.Parent != null) cat = cat.Parent;
            return cat.DisplayName ?? string.Empty;
        }

        private static string ComputeFullPath(PolicyPlusCategory? cat)
        {
            if (cat == null) return string.Empty;
            var stack = new Stack<string>();
            var cur = cat;
            while (cur != null)
            {
                if (!string.IsNullOrEmpty(cur.DisplayName)) stack.Push(cur.DisplayName);
                cur = cur.Parent;
            }
            return string.Join(" / ", stack); // delimiter
        }

        private PolicyListRow(string displayName, string secondName, bool isCategory, PolicyPlusPolicy? policy, PolicyPlusCategory? category,
            IPolicySource? comp, IPolicySource? user, IEnumerable<PolicyPlusPolicy>? variants,
            string uniqueId, string categoryName, string topCategoryName, string categoryFullPath, string appliesText, string supportedText)
        {
            DisplayName = displayName;
            SecondName = secondName;
            IsCategory = isCategory;
            Policy = policy;
            Category = category;
            _comp = comp;
            _user = user;
            _variants = variants;
            _isGroup = variants != null;
            UniqueId = uniqueId;
            CategoryName = categoryName;
            TopCategoryName = topCategoryName;
            CategoryFullPath = categoryFullPath;
            AppliesText = appliesText;
            SupportedText = supportedText;

            IsBookmarked = policy != null && BookmarkService.Instance.IsBookmarked(policy.UniqueID);
            BookmarkService.Instance.Changed += (_, __) =>
            {
                if (Policy != null)
                {
                    IsBookmarked = BookmarkService.Instance.IsBookmarked(Policy.UniqueID);
                }
            };

            RefreshStateFromSourcesAndPending(comp, user);
        }

        public static PolicyListRow FromCategory(PolicyPlusCategory c)
            => new PolicyListRow(c.DisplayName, string.Empty, true, null, c, null, null, null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        private static Brush? TryGetResourceBrush(string key)
        {
            try { var obj = Application.Current?.Resources?[key]; if (obj is Brush b) return b; } catch { }
            return null;
        }

        private static string AppliesOf(PolicyPlusPolicy p) => p.RawPolicy.Section switch
        {
            AdmxPolicySection.Machine => "Computer",
            AdmxPolicySection.User => "User",
            _ => "Both"
        };

        private static string StateText(bool enabled, bool disabled, bool configured)
        {
            if (!configured) return "Not configured";
            if (enabled) return "Enabled";
            if (disabled) return "Disabled";
            return string.Empty;
        }

        private static string GetSecondName(PolicyPlusPolicy p)
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                if (!(s.SecondLanguageEnabled ?? false)) return string.Empty;
                var lang = s.SecondLanguage ?? "en-US";
                return LocalizedTextService.GetPolicyNameIn(p, lang);
            }
            catch { return string.Empty; }
        }

        public static PolicyListRow FromPolicy(PolicyPlusPolicy p, IPolicySource? comp, IPolicySource? user)
        {
            string categoryName = p.Category?.DisplayName ?? string.Empty; // immediate
            string topCategoryName = ComputeTopCategory(p.Category);
            string fullPath = ComputeFullPath(p.Category);
            string appliesText = AppliesOf(p);
            string supportedText = p.SupportedOn?.DisplayName ?? string.Empty;
            string second = GetSecondName(p);
            return new PolicyListRow(p.DisplayName, second, false, p, null, comp, user, null,
                p.UniqueID, categoryName, topCategoryName, fullPath, appliesText, supportedText);
        }

        public static PolicyListRow FromGroup(PolicyPlusPolicy representative, IEnumerable<PolicyPlusPolicy> variants, IPolicySource? comp, IPolicySource? user)
        {
            string categoryName = representative.Category?.DisplayName ?? string.Empty;
            string topCategoryName = ComputeTopCategory(representative.Category);
            string fullPath = ComputeFullPath(representative.Category);
            string appliesText = AppliesOf(representative);
            string supportedText = representative.SupportedOn?.DisplayName ?? string.Empty;
            string second = GetSecondName(representative);
            return new PolicyListRow(representative.DisplayName, second, false, representative, null, comp, user, variants,
                representative.UniqueID, categoryName, topCategoryName, fullPath, appliesText, supportedText);
        }

        public void RefreshStateFromSourcesAndPending(IPolicySource? compUpdate, IPolicySource? userUpdate)
        {
            if (compUpdate != null) _comp = compUpdate;
            if (userUpdate != null) _user = userUpdate;

            Brush? apply = TryGetResourceBrush("ApplyBrush");
            Brush? danger = TryGetResourceBrush("DangerBrush");

            if (_isGroup)
            {
                bool anyUserConfigured = false, anyCompConfigured = false;
                bool anyUserEnabled = false, anyUserDisabled = false;
                bool anyCompEnabled = false, anyCompDisabled = false;
                bool anyUserPending = false, anyCompPending = false;

                var pending = PendingChangesService.Instance.Pending;
                if (_variants != null)
                {
                    foreach (var v in _variants)
                    {
                        if (v.RawPolicy.Section == AdmxPolicySection.User || v.RawPolicy.Section == AdmxPolicySection.Both)
                        {
                            bool cfg = false, en = false, dis = false, pend = false;
                            var pendUser = pending.FirstOrDefault(pc => pc.PolicyId == v.UniqueID && pc.Scope == "User");
                            if (pendUser != null)
                            { en = pendUser.DesiredState == PolicyState.Enabled; dis = pendUser.DesiredState == PolicyState.Disabled; cfg = en || dis; pend = cfg; }
                            else if (_user != null)
                            {
                                try
                                {
                                    var st = PolicyProcessing.GetPolicyState(_user, v);
                                    cfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                                    en = st == PolicyState.Enabled; dis = st == PolicyState.Disabled;
                                }
                                catch { }
                            }
                            anyUserConfigured |= cfg; anyUserEnabled |= en; anyUserDisabled |= dis; anyUserPending |= pend;
                        }
                        if (v.RawPolicy.Section == AdmxPolicySection.Machine || v.RawPolicy.Section == AdmxPolicySection.Both)
                        {
                            bool cfg = false, en = false, dis = false, pend = false;
                            var pendComp = pending.FirstOrDefault(pc => pc.PolicyId == v.UniqueID && pc.Scope == "Computer");
                            if (pendComp != null)
                            { en = pendComp.DesiredState == PolicyState.Enabled; dis = pendComp.DesiredState == PolicyState.Disabled; cfg = en || dis; pend = cfg; }
                            else if (_comp != null)
                            {
                                try
                                {
                                    var st = PolicyProcessing.GetPolicyState(_comp, v);
                                    cfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                                    en = st == PolicyState.Enabled; dis = st == PolicyState.Disabled;
                                }
                                catch { }
                            }
                            anyCompConfigured |= cfg; anyCompEnabled |= en; anyCompDisabled |= dis; anyCompPending |= pend;
                        }
                    }
                }

                UserConfigured = anyUserConfigured; ComputerConfigured = anyCompConfigured;
                UserEnabled = anyUserEnabled; UserDisabled = anyUserDisabled;
                ComputerEnabled = anyCompEnabled; ComputerDisabled = anyCompDisabled;

                UserGlyph = anyUserEnabled ? "\uE73E" : (anyUserDisabled ? "\uE711" : string.Empty);
                ComputerGlyph = anyCompEnabled ? "\uE73E" : (anyCompDisabled ? "\uE711" : string.Empty);

                UserBrush = anyUserEnabled ? apply : (anyUserDisabled ? danger : null);
                ComputerBrush = anyCompEnabled ? apply : (anyCompDisabled ? danger : null);

                UserStateText = StateText(anyUserEnabled, anyUserDisabled, anyUserConfigured);
                ComputerStateText = StateText(anyCompEnabled, anyCompDisabled, anyCompConfigured);

                UserPending = anyUserPending;
                ComputerPending = anyCompPending;
            }
            else if (Policy != null)
            {
                bool userCfg = false, compCfg = false;
                bool userEn = false, userDis = false, compEn = false, compDis = false;
                bool userPend = false, compPend = false;
                try
                {
                    if (_user != null)
                    {
                        var st = PolicyProcessing.GetPolicyState(_user, Policy);
                        userCfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                        userEn = st == PolicyState.Enabled; userDis = st == PolicyState.Disabled;
                    }
                    if (_comp != null)
                    {
                        var st = PolicyProcessing.GetPolicyState(_comp, Policy);
                        compCfg = st == PolicyState.Enabled || st == PolicyState.Disabled;
                        compEn = st == PolicyState.Enabled; compDis = st == PolicyState.Disabled;
                    }
                }
                catch { }

                try
                {
                    var pend = PendingChangesService.Instance.Pending;
                    var pendUser = pend.FirstOrDefault(pc => pc.PolicyId == Policy.UniqueID && pc.Scope == "User");
                    if (pendUser != null)
                    {
                        userCfg = pendUser.DesiredState == PolicyState.Enabled || pendUser.DesiredState == PolicyState.Disabled;
                        userEn = pendUser.DesiredState == PolicyState.Enabled; userDis = pendUser.DesiredState == PolicyState.Disabled;
                        userPend = userCfg;
                    }
                    var pendComp = pend.FirstOrDefault(pc => pc.PolicyId == Policy.UniqueID && pc.Scope == "Computer");
                    if (pendComp != null)
                    {
                        compCfg = pendComp.DesiredState == PolicyState.Enabled || pendComp.DesiredState == PolicyState.Disabled;
                        compEn = pendComp.DesiredState == PolicyState.Enabled; compDis = pendComp.DesiredState == PolicyState.Disabled;
                        compPend = compCfg;
                    }
                }
                catch { }

                UserConfigured = userCfg; ComputerConfigured = compCfg;
                UserEnabled = userEn; UserDisabled = userDis;
                ComputerEnabled = compEn; ComputerDisabled = compDis;

                UserGlyph = userEn ? "\uE73E" : (userDis ? "\uE711" : string.Empty);
                ComputerGlyph = compEn ? "\uE73E" : (compDis ? "\uE711" : string.Empty);

                Brush? apply2 = apply; Brush? danger2 = danger;
                UserBrush = userEn ? (apply2 ?? null) : (userDis ? (danger2 ?? null) : null);
                ComputerBrush = compEn ? (apply2 ?? null) : (compDis ? (danger2 ?? null) : null);

                UserStateText = StateText(userEn, userDis, userCfg);
                ComputerStateText = StateText(compEn, compDis, compCfg);

                UserPending = userPend; ComputerPending = compPend;
            }
        }
    }
}
