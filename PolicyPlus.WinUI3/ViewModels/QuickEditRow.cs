using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.IO;
using PolicyPlus.WinUI3.Services;
using System.Text; // added

namespace PolicyPlus.WinUI3.ViewModels
{
    public enum QuickEditState { NotConfigured, Enabled, Disabled }

    public sealed partial class QuickEditRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Suppress queuing pending changes while constructing / loading from sources
        private bool _initializing;

        // New: collection of all option elements (multiple supported)
        public List<OptionElementVM> OptionElements { get; } = new();

        // Adopt full state from another row (after saving / external refresh) without enqueuing changes
        internal void AdoptState(QuickEditRow other)
        {
            if (other == null || !string.Equals(other.Policy.UniqueID, Policy.UniqueID, StringComparison.OrdinalIgnoreCase)) return;
            _initializing = true; // suppress QueuePending during bulk copy
            try
            {
                _userState = other._userState; OnChanged(nameof(UserState));
                _computerState = other._computerState; OnChanged(nameof(ComputerState));
                // Adopt option values element-wise
                foreach (var elem in OptionElements)
                {
                    var otherElem = other.OptionElements.FirstOrDefault(e => e.Id == elem.Id);
                    if (otherElem == null) continue;
                    switch (elem.Type)
                    {
                        case OptionElementType.Enum:
                            elem.UserEnumIndex = otherElem.UserEnumIndex;
                            elem.ComputerEnumIndex = otherElem.ComputerEnumIndex;
                            break;
                        case OptionElementType.Boolean:
                            elem.UserBool = otherElem.UserBool;
                            elem.ComputerBool = otherElem.ComputerBool;
                            break;
                        case OptionElementType.Text:
                            elem.UserText = otherElem.UserText;
                            elem.ComputerText = otherElem.ComputerText;
                            break;
                        case OptionElementType.Decimal:
                            elem.UserNumber = otherElem.UserNumber;
                            elem.ComputerNumber = otherElem.ComputerNumber;
                            break;
                        case OptionElementType.List:
                            elem.ReplaceList(true, otherElem.UserListItems.ToList());
                            elem.ReplaceList(false, otherElem.ComputerListItems.ToList());
                            break;
                        case OptionElementType.MultiText:
                            elem.ReplaceMultiText(true, otherElem.UserMultiTextItems.ToList());
                            elem.ReplaceMultiText(false, otherElem.ComputerMultiTextItems.ToList());
                            break;
                    }
                }
            }
            finally { _initializing = false; }
        }

        public PolicyPlusPolicy Policy { get; }
        public bool SupportsUser => Policy.RawPolicy.Section == AdmxPolicySection.User || Policy.RawPolicy.Section == AdmxPolicySection.Both;
        public bool SupportsComputer => Policy.RawPolicy.Section == AdmxPolicySection.Machine || Policy.RawPolicy.Section == AdmxPolicySection.Both;

        // Expose only the portion of the ID after the first ':' for compact display.
        public string IdTail
        {
            get
            {
                try
                {
                    var id = Policy.UniqueID ?? string.Empty;
                    int idx = id.IndexOf(':');
                    if (idx >= 0 && idx + 1 < id.Length)
                        return id.Substring(idx + 1);
                    return id;
                }
                catch { return Policy.UniqueID; }
            }
        }

        private QuickEditState _userState; public QuickEditState UserState { get => _userState; set { if (_userState != value) { _userState = value; OnChanged(nameof(UserState)); if (!_initializing) QueuePending("User"); UpdateEnablement(); } } }
        private QuickEditState _computerState; public QuickEditState ComputerState { get => _computerState; set { if (_computerState != value) { _computerState = value; OnChanged(nameof(ComputerState)); if (!_initializing) QueuePending("Computer"); UpdateEnablement(); } } }

        // Backwards compatibility helpers (first element per type)
        private OptionElementVM? FirstOf(OptionElementType t) => OptionElements.FirstOrDefault(e => e.Type == t);
        public bool HasEnumElement => FirstOf(OptionElementType.Enum) != null;
        public bool HasListElement => FirstOf(OptionElementType.List) != null;
        public bool HasBooleanElement => FirstOf(OptionElementType.Boolean) != null;
        public bool HasTextElement => FirstOf(OptionElementType.Text) != null;
        public bool HasDecimalElement => FirstOf(OptionElementType.Decimal) != null;
        public bool HasMultiTextElement => FirstOf(OptionElementType.MultiText) != null;

        // Legacy single-element exposure removed: callers should migrate as needed (keep minimal compatibility surfaces if necessary)
        public List<string> EnumChoices => FirstOf(OptionElementType.Enum)?.Choices ?? new();
        public int UserEnumIndex { get => FirstOf(OptionElementType.Enum)?.UserEnumIndex ?? -1; set { var e = FirstOf(OptionElementType.Enum); if (e != null) e.UserEnumIndex = value; } }
        public int ComputerEnumIndex { get => FirstOf(OptionElementType.Enum)?.ComputerEnumIndex ?? -1; set { var e = FirstOf(OptionElementType.Enum); if (e != null) e.ComputerEnumIndex = value; } }
        public bool UserBool { get => FirstOf(OptionElementType.Boolean)?.UserBool ?? false; set { var e = FirstOf(OptionElementType.Boolean); if (e != null) e.UserBool = value; } }
        public bool ComputerBool { get => FirstOf(OptionElementType.Boolean)?.ComputerBool ?? false; set { var e = FirstOf(OptionElementType.Boolean); if (e != null) e.ComputerBool = value; } }
        public string UserText { get => FirstOf(OptionElementType.Text)?.UserText ?? string.Empty; set { var e = FirstOf(OptionElementType.Text); if (e != null) e.UserText = value; } }
        public string ComputerText { get => FirstOf(OptionElementType.Text)?.ComputerText ?? string.Empty; set { var e = FirstOf(OptionElementType.Text); if (e != null) e.ComputerText = value; } }
        public uint? UserNumber { get => FirstOf(OptionElementType.Decimal)?.UserNumber; set { var e = FirstOf(OptionElementType.Decimal); if (e != null) e.UserNumber = value; } }
        public uint? ComputerNumber { get => FirstOf(OptionElementType.Decimal)?.ComputerNumber; set { var e = FirstOf(OptionElementType.Decimal); if (e != null) e.ComputerNumber = value; } }
        public List<string> UserListItems { get => FirstOf(OptionElementType.List)?.UserListItems ?? new(); set { var e = FirstOf(OptionElementType.List); if (e != null) e.ReplaceList(true, value); } }
        public List<string> ComputerListItems { get => FirstOf(OptionElementType.List)?.ComputerListItems ?? new(); set { var e = FirstOf(OptionElementType.List); if (e != null) e.ReplaceList(false, value); } }
        public string UserListSummary => FirstOf(OptionElementType.List)?.UserListSummary ?? string.Empty;
        public string ComputerListSummary => FirstOf(OptionElementType.List)?.ComputerListSummary ?? string.Empty;
        public List<string> UserMultiTextItems { get => FirstOf(OptionElementType.MultiText)?.UserMultiTextItems ?? new(); set { var e = FirstOf(OptionElementType.MultiText); if (e != null) e.ReplaceMultiText(true, value); } }
        public List<string> ComputerMultiTextItems { get => FirstOf(OptionElementType.MultiText)?.ComputerMultiTextItems ?? new(); set { var e = FirstOf(OptionElementType.MultiText); if (e != null) e.ReplaceMultiText(false, value); } }
        public string UserMultiTextSummary => FirstOf(OptionElementType.MultiText)?.UserMultiTextSummary ?? string.Empty;
        public string ComputerMultiTextSummary => FirstOf(OptionElementType.MultiText)?.ComputerMultiTextSummary ?? string.Empty;
        public uint DecimalMin => FirstOf(OptionElementType.Decimal)?.DecimalMin ?? 0; public double DecimalMinDouble => DecimalMin;
        public uint DecimalMax => FirstOf(OptionElementType.Decimal)?.DecimalMax ?? 0; public double DecimalMaxDouble => DecimalMax;
        public uint DecimalDefault => FirstOf(OptionElementType.Decimal)?.DecimalDefault ?? 0;

        private readonly AdmxBundle _bundle;
        private readonly IPolicySource? _compSource;
        private readonly IPolicySource? _userSource;

        public QuickEditRow(PolicyPlusPolicy policy, AdmxBundle bundle, IPolicySource? comp, IPolicySource? user)
        {
            _initializing = true;
            Policy = policy; _bundle = bundle; _compSource = comp; _userSource = user;

            if (policy.RawPolicy.Elements != null)
            {
                foreach (var e in policy.RawPolicy.Elements)
                {
                    try
                    {
                        OptionElementVM? vm = null;
                        string display = e.ID;
                        try
                        {
                            if (policy.Presentation != null)
                            {
                                foreach (var pe in policy.Presentation.Elements)
                                {
                                    if (string.Equals(pe.ID, e.ID, StringComparison.OrdinalIgnoreCase))
                                    {
                                        display = pe.ID; // use ID; could map to label resolved elsewhere
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                        if (e is EnumPolicyElement ee)
                        {
                            vm = new OptionElementVM(this, e, OptionElementType.Enum, display);
                            vm.InitializeEnumChoices(ee, (code, definedIn) => _bundle.ResolveString(code, definedIn), policy.RawPolicy.DefinedIn);
                        }
                        else if (e is ListPolicyElement le)
                        {
                            vm = new OptionElementVM(this, e, OptionElementType.List, display, providesNames: le.UserProvidesNames);
                        }
                        else if (e is BooleanPolicyElement)
                        {
                            // Look up defaultChecked from presentation if available
                            bool? def = null;
                            try
                            {
                                if (policy.Presentation != null)
                                {
                                    var pe = policy.Presentation.Elements.FirstOrDefault(p => p.ElementType == "checkBox" && string.Equals(p.ID, e.ID, StringComparison.OrdinalIgnoreCase)) as CheckBoxPresentationElement;
                                    if (pe != null) def = pe.DefaultState;
                                }
                            }
                            catch { }
                            vm = new OptionElementVM(this, e, OptionElementType.Boolean, display, defaultBool: def);
                        }
                        else if (e is DecimalPolicyElement de)
                        {
                            uint min = de.Minimum; uint max = de.Maximum; uint def = de.Minimum; uint inc = 1;
                            try
                            {
                                if (policy.Presentation != null)
                                {
                                    foreach (var pe in policy.Presentation.Elements)
                                    {
                                        if (pe.ElementType == "decimalTextBox" && string.Equals(pe.ID, de.ID, StringComparison.OrdinalIgnoreCase) && pe is NumericBoxPresentationElement nbpe)
                                        { def = nbpe.DefaultValue; inc = nbpe.SpinnerIncrement; break; }
                                    }
                                }
                            }
                            catch { }
                            if (def < min) def = min; if (def > max) def = max;
                            vm = new OptionElementVM(this, e, OptionElementType.Decimal, display, min, max, def, inc);
                        }
                        else if (e is TextPolicyElement te)
                        {
                            string? defText = null; int maxLen = te.MaxLength;
                            try
                            {
                                if (policy.Presentation != null)
                                {
                                    var pe = policy.Presentation.Elements.FirstOrDefault(p => p.ElementType == "textBox" && string.Equals(p.ID, te.ID, StringComparison.OrdinalIgnoreCase)) as TextBoxPresentationElement;
                                    if (pe != null) defText = pe.DefaultValue;
                                }
                            }
                            catch { }
                            vm = new OptionElementVM(this, e, OptionElementType.Text, display, defaultText: defText, textMaxLength: maxLen);
                        }
                        else if (e is MultiTextPolicyElement)
                        { vm = new OptionElementVM(this, e, OptionElementType.MultiText, display); }
                        if (vm != null) OptionElements.Add(vm);
                    }
                    catch { }
                }
            }

            // Load existing states and option values
            LoadInitialStates();
            foreach (var vm in OptionElements) vm.EnsureDefaultsAfterLoad();
            _initializing = false; // from now on, user modifications create pending changes
        }

        private void UpdateEnablement()
        { OnChanged(nameof(UserEnabledForOptions)); OnChanged(nameof(ComputerEnabledForOptions)); }
        public bool UserEnabledForOptions => UserState == QuickEditState.Enabled;
        public bool ComputerEnabledForOptions => ComputerState == QuickEditState.Enabled;
        public void RefreshListSummaries() { OnChanged(nameof(UserListSummary)); OnChanged(nameof(ComputerListSummary)); OnChanged(nameof(UserMultiTextSummary)); OnChanged(nameof(ComputerMultiTextSummary)); }

        private void LoadInitialStates()
        {
            try
            {
                if (SupportsUser && _userSource != null)
                {
                    var st = PolicyProcessing.GetPolicyState(_userSource, Policy);
                    UserState = st switch { PolicyState.Enabled => QuickEditState.Enabled, PolicyState.Disabled => QuickEditState.Disabled, _ => QuickEditState.NotConfigured };
                    if (UserState == QuickEditState.Enabled)
                    {
                        var opts = PolicyProcessing.GetPolicyOptionStates(_userSource, Policy);
                        foreach (var vm in OptionElements) vm.LoadFrom(opts, true);
                    }
                }
                if (SupportsComputer && _compSource != null)
                {
                    var st = PolicyProcessing.GetPolicyState(_compSource, Policy);
                    ComputerState = st switch { PolicyState.Enabled => QuickEditState.Enabled, PolicyState.Disabled => QuickEditState.Disabled, _ => QuickEditState.NotConfigured };
                    if (ComputerState == QuickEditState.Enabled)
                    {
                        var opts = PolicyProcessing.GetPolicyOptionStates(_compSource, Policy);
                        foreach (var vm in OptionElements) vm.LoadFrom(opts, false);
                    }
                }
            }
            catch { }
        }

        internal void QueuePending(string scope)
        {
            if (_initializing) return; // suppress during initial load
            try
            {
                bool isUser = scope.Equals("User", StringComparison.OrdinalIgnoreCase);
                var state = isUser ? UserState : ComputerState;
                PolicyState desired = state switch { QuickEditState.Enabled => PolicyState.Enabled, QuickEditState.Disabled => PolicyState.Disabled, _ => PolicyState.NotConfigured };
                Dictionary<string, object>? options = null;
                if (desired == PolicyState.Enabled)
                {
                    foreach (var vm in OptionElements)
                    {
                        var val = isUser ? vm.GetUserValue() : vm.GetComputerValue();
                        if (val == null) continue;
                        options ??= new();
                        // Decimal clamp
                        if (vm.Type == OptionElementType.Decimal && val is uint u)
                        { if (u < vm.DecimalMin) u = vm.DecimalMin; if (u > vm.DecimalMax) u = vm.DecimalMax; val = u; }
                        options[vm.Id] = val;
                    }
                }
                var action = desired switch { PolicyState.Enabled => "Enable", PolicyState.Disabled => "Disable", _ => "Clear" };
                (string details, string detailsFull) = BuildDetails(action, desired, options);
                PendingChangesService.Instance.Add(new PendingChange
                {
                    PolicyId = Policy.UniqueID,
                    PolicyName = Policy.DisplayName ?? Policy.UniqueID,
                    Scope = scope,
                    Action = action,
                    Details = details,
                    DetailsFull = detailsFull,
                    DesiredState = desired,
                    Options = options
                });
            }
            catch { }
        }

        private (string shortText, string longText) BuildDetails(string action, PolicyState desired, Dictionary<string, object>? options)
        {
            try
            {
                if (desired != PolicyState.Enabled || options == null || options.Count == 0) return (action, action);
                var shortSb = new StringBuilder();
                shortSb.Append(action);
                var optionPairs = new List<string>(); int shown = 0;
                foreach (var kv in options)
                { if (shown >= 4) break; optionPairs.Add(kv.Key + "=" + FormatOpt(kv.Value)); shown++; }
                if (optionPairs.Count > 0)
                {
                    shortSb.Append(": "); shortSb.Append(string.Join(", ", optionPairs));
                    if (options.Count > optionPairs.Count) shortSb.Append($" (+{options.Count - optionPairs.Count} more)");
                }
                var longSb = new StringBuilder();
                longSb.AppendLine(action);
                try
                {
                    longSb.AppendLine("Registry values:");
                    foreach (var kv in PolicyProcessing.GetReferencedRegistryValues(Policy))
                        longSb.AppendLine("  ? " + kv.Key + (string.IsNullOrEmpty(kv.Value) ? string.Empty : $" ({kv.Value})"));
                }
                catch { }
                longSb.AppendLine("Options:");
                foreach (var kv in options) longSb.AppendLine("  - " + kv.Key + " = " + FormatOpt(kv.Value));
                return (shortSb.ToString(), longSb.ToString());
            }
            catch { return (action, action); }
        }

        private static string FormatOpt(object v)
        {
            if (v == null) return string.Empty;
            if (v is string s) return s;
            if (v is bool b) return b ? "true" : "false";
            if (v is int i) return i.ToString();
            if (v is uint ui) return ui.ToString();
            if (v is IEnumerable<string> strList) return string.Join("|", strList);
            if (v is System.Collections.IEnumerable en && v is not string)
            { try { return string.Join(", ", en.Cast<object>().Select(o => Convert.ToString(o) ?? string.Empty)); } catch { } }
            return Convert.ToString(v) ?? string.Empty;
        }

        public void ReplaceList(bool isUser, List<string> newItems)
        {
            var first = FirstOf(OptionElementType.List); if (first != null) first.ReplaceList(isUser, newItems);
            RefreshListSummaries();
            if (isUser && UserState == QuickEditState.Enabled) QueuePending("User");
            if (!isUser && ComputerState == QuickEditState.Enabled) QueuePending("Computer");
        }
        public void ReplaceMultiText(bool isUser, List<string> newItems)
        {
            var first = FirstOf(OptionElementType.MultiText); if (first != null) first.ReplaceMultiText(isUser, newItems);
            RefreshListSummaries();
            if (isUser && UserState == QuickEditState.Enabled) QueuePending("User");
            if (!isUser && ComputerState == QuickEditState.Enabled) QueuePending("Computer");
        }

        public void ApplyExternal(string scope, PolicyState desired, Dictionary<string, object>? options)
        {
            bool isUser = scope.Equals("User", StringComparison.OrdinalIgnoreCase);
            _initializing = true; // prevent queueing while we synchronize
            try
            {
                var newState = desired switch { PolicyState.Enabled => QuickEditState.Enabled, PolicyState.Disabled => QuickEditState.Disabled, _ => QuickEditState.NotConfigured };
                if (isUser)
                {
                    UserState = newState;
                    if (newState == QuickEditState.Enabled && options != null)
                        foreach (var vm in OptionElements) vm.LoadFrom(options, true);
                }
                else
                {
                    ComputerState = newState;
                    if (newState == QuickEditState.Enabled && options != null)
                        foreach (var vm in OptionElements) vm.LoadFrom(options, false);
                }
                RefreshListSummaries();
            }
            catch { }
            finally { _initializing = false; }
        }
    }
}
