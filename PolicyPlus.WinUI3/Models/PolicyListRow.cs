using PolicyPlus;
using PolicyPlus.WinUI3.Services;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI; // Colors

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

        // Brushes for glyph coloring
        public Brush UserBrush { get; }
        public Brush ComputerBrush { get; }

        private PolicyListRow(string displayName, bool isCategory, PolicyPlusPolicy? policy, PolicyPlusCategory? category,
            bool userConfigured, bool computerConfigured, bool userEnabled, bool userDisabled, bool compEnabled, bool compDisabled,
            string userGlyph, string compGlyph, Brush userBrush, Brush compBrush)
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
        }

        public static PolicyListRow FromCategory(PolicyPlusCategory c)
            => new PolicyListRow(c.DisplayName, true, null, c, false, false, false, false, false, false, string.Empty, string.Empty, new SolidColorBrush(Colors.Transparent), new SolidColorBrush(Colors.Transparent));

        private static Brush GetBrush(string key, global::Windows.UI.Color fallback)
        {
            try
            {
                var obj = Application.Current?.Resources?[key];
                if (obj is Brush b) return b;
            }
            catch { }
            return new SolidColorBrush(fallback);
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

            // Choose colors: Enabled -> ApplyBrush (green), Disabled -> DangerBrush (red)
            Brush apply = GetBrush("ApplyBrush", Colors.SeaGreen);
            Brush danger = GetBrush("DangerBrush", Colors.IndianRed);
            Brush userBrush = userEn ? apply : danger;
            Brush compBrush = compEn ? apply : danger;

            return new PolicyListRow(p.DisplayName, false, p, null, userCfg, compCfg, userEn, userDis, compEn, compDis, userGlyph, compGlyph, userBrush, compBrush);
        }
    }
}
