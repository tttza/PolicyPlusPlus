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

    public sealed class QuickEditRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Suppress queuing pending changes while constructing / loading from sources
        private bool _initializing;

        // Adopt full state from another row (after saving / external refresh) without enqueuing changes
        internal void AdoptState(QuickEditRow other)
        {
            if (other == null || !string.Equals(other.Policy.UniqueID, Policy.UniqueID, StringComparison.OrdinalIgnoreCase)) return;
            _initializing = true; // suppress QueuePending during bulk copy
            try
            {
                _userState = other._userState; OnChanged(nameof(UserState));
                _computerState = other._computerState; OnChanged(nameof(ComputerState));
                _userEnumIndex = other._userEnumIndex; OnChanged(nameof(UserEnumIndex));
                _computerEnumIndex = other._computerEnumIndex; OnChanged(nameof(ComputerEnumIndex));
                _userBool = other._userBool; OnChanged(nameof(UserBool));
                _computerBool = other._computerBool; OnChanged(nameof(ComputerBool));
                _userText = other._userText; OnChanged(nameof(UserText));
                _computerText = other._computerText; OnChanged(nameof(ComputerText));
                _userNumber = other._userNumber; OnChanged(nameof(UserNumber));
                _computerNumber = other._computerNumber; OnChanged(nameof(ComputerNumber));
                UserListItems = other.UserListItems.ToList();
                ComputerListItems = other.ComputerListItems.ToList();
                UserMultiTextItems = other.UserMultiTextItems.ToList();
                ComputerMultiTextItems = other.ComputerMultiTextItems.ToList();
                RefreshListSummaries();
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

        public bool HasEnumElement => EnumElement != null;
        public bool HasListElement => ListElement != null;
        public bool HasBooleanElement => BooleanElement != null;
        public bool HasTextElement => TextElement != null;
        public bool HasDecimalElement => DecimalElement != null;
        public bool HasMultiTextElement => MultiTextElement != null;

        public EnumPolicyElement? EnumElement { get; }
        public ListPolicyElement? ListElement { get; }
        public BooleanPolicyElement? BooleanElement { get; }
        public TextPolicyElement? TextElement { get; }
        public DecimalPolicyElement? DecimalElement { get; }
        public MultiTextPolicyElement? MultiTextElement { get; }

        // Decimal metadata for UI binding
        public uint DecimalMin { get; }
        public uint DecimalMax { get; }
        public uint DecimalDefault { get; }

        public List<string> EnumChoices { get; } = new();
        private int _userEnumIndex = -1; public int UserEnumIndex { get => _userEnumIndex; set { if (_userEnumIndex != value) { _userEnumIndex = value; OnChanged(nameof(UserEnumIndex)); if (UserState == QuickEditState.Enabled) QueuePending("User"); } } }
        private int _computerEnumIndex = -1; public int ComputerEnumIndex { get => _computerEnumIndex; set { if (_computerEnumIndex != value) { _computerEnumIndex = value; OnChanged(nameof(ComputerEnumIndex)); if (ComputerState == QuickEditState.Enabled) QueuePending("Computer"); } } }

        public List<string> UserListItems { get; set; } = new();
        public List<string> ComputerListItems { get; set; } = new();
        public string UserListSummary => HasListElement ? $"Edit... ({UserListItems.Count})" : string.Empty;
        public string ComputerListSummary => HasListElement ? $"Edit... ({ComputerListItems.Count})" : string.Empty;

        public List<string> UserMultiTextItems { get; set; } = new();
        public List<string> ComputerMultiTextItems { get; set; } = new();
        public string UserMultiTextSummary => HasMultiTextElement ? $"Edit... ({UserMultiTextItems.Count})" : string.Empty;
        public string ComputerMultiTextSummary => HasMultiTextElement ? $"Edit... ({ComputerMultiTextItems.Count})" : string.Empty;

        private bool _userBool; public bool UserBool { get => _userBool; set { if (_userBool != value) { _userBool = value; OnChanged(nameof(UserBool)); if (UserState == QuickEditState.Enabled) QueuePending("User"); } } }
        private bool _computerBool; public bool ComputerBool { get => _computerBool; set { if (_computerBool != value) { _computerBool = value; OnChanged(nameof(ComputerBool)); if (ComputerState == QuickEditState.Enabled) QueuePending("Computer"); } } }

        private string _userText = string.Empty; public string UserText { get => _userText; set { if (_userText != value) { _userText = value; OnChanged(nameof(UserText)); if (UserState == QuickEditState.Enabled) QueuePending("User"); } } }
        private string _computerText = string.Empty; public string ComputerText { get => _computerText; set { if (_computerText != value) { _computerText = value; OnChanged(nameof(ComputerText)); if (ComputerState == QuickEditState.Enabled) QueuePending("Computer"); } } }

        private uint? _userNumber; public uint? UserNumber { get => _userNumber; set { if (_userNumber != value) { _userNumber = value; OnChanged(nameof(UserNumber)); if (UserState == QuickEditState.Enabled) QueuePending("User"); } } }
        private uint? _computerNumber; public uint? ComputerNumber { get => _computerNumber; set { if (_computerNumber != value) { _computerNumber = value; OnChanged(nameof(ComputerNumber)); if (ComputerState == QuickEditState.Enabled) QueuePending("Computer"); } } }

        private readonly AdmxBundle _bundle;
        private readonly IPolicySource? _compSource;
        private readonly IPolicySource? _userSource;

        public QuickEditRow(PolicyPlusPolicy policy, AdmxBundle bundle, IPolicySource? comp, IPolicySource? user)
        {
            _initializing = true;
            Policy = policy; _bundle = bundle; _compSource = comp; _userSource = user;
            // discover first element of each supported type
            if (policy.RawPolicy.Elements != null)
            {
                foreach (var e in policy.RawPolicy.Elements)
                {
                    if (EnumElement == null && e is EnumPolicyElement ee) { EnumElement = ee; continue; }
                    if (ListElement == null && e is ListPolicyElement le && !le.UserProvidesNames) { ListElement = le; continue; }
                    if (BooleanElement == null && e is BooleanPolicyElement be) { BooleanElement = be; continue; }
                    if (DecimalElement == null && e is DecimalPolicyElement de) { DecimalElement = de; continue; }
                    if (TextElement == null && e is TextPolicyElement te) { TextElement = te; continue; }
                    if (MultiTextElement == null && e is MultiTextPolicyElement mte) { MultiTextElement = mte; continue; }
                }
            }
            if (EnumElement != null)
            {
                foreach (var it in EnumElement.Items)
                {
                    string text;
                    try { text = bundle.ResolveString(it.DisplayCode, policy.RawPolicy.DefinedIn); } catch { text = it.DisplayCode; }
                    EnumChoices.Add(string.IsNullOrWhiteSpace(text) ? it.DisplayCode : text);
                }
            }

            // Decimal bounds & default (presentation)
            if (DecimalElement != null)
            {
                DecimalMin = DecimalElement.Minimum;
                DecimalMax = DecimalElement.Maximum;
                uint def = DecimalElement.Minimum; // fallback if no presentation element
                try
                {
                    if (policy.Presentation != null)
                    {
                        foreach (var pe in policy.Presentation.Elements)
                        {
                            if (pe.ElementType == "decimalTextBox" && string.Equals(pe.ID, DecimalElement.ID, StringComparison.OrdinalIgnoreCase))
                            {
                                if (pe is NumericBoxPresentationElement nbpe)
                                {
                                    def = nbpe.DefaultValue;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
                if (def < DecimalMin) def = DecimalMin;
                if (def > DecimalMax) def = DecimalMax;
                DecimalDefault = def;
            }

            // Load existing states and option values
            LoadInitialStates();
            // After loading, if enum index still unset, select first item as default (do not queue pending while _initializing)
            if (EnumElement != null)
            {
                if (_userEnumIndex == -1 && EnumChoices.Count > 0) _userEnumIndex = 0;
                if (_computerEnumIndex == -1 && EnumChoices.Count > 0) _computerEnumIndex = 0;
            }
            // Initialize decimal numbers with default if not set (NotConfigured display)
            if (DecimalElement != null)
            {
                if (!_userNumber.HasValue) _userNumber = DecimalDefault; // show default even when not configured
                if (!_computerNumber.HasValue) _computerNumber = DecimalDefault;
            }
            _initializing = false; // from now on, user modifications create pending changes
        }

        private void UpdateEnablement()
        {
            OnChanged(nameof(UserEnabledForOptions));
            OnChanged(nameof(ComputerEnabledForOptions));
        }

        public bool UserEnabledForOptions => UserState == QuickEditState.Enabled;
        public bool ComputerEnabledForOptions => ComputerState == QuickEditState.Enabled;

        public void RefreshListSummaries()
        { OnChanged(nameof(UserListSummary)); OnChanged(nameof(ComputerListSummary)); OnChanged(nameof(UserMultiTextSummary)); OnChanged(nameof(ComputerMultiTextSummary)); }

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
                        LoadOptionValues(opts, true);
                    }
                }
                if (SupportsComputer && _compSource != null)
                {
                    var st = PolicyProcessing.GetPolicyState(_compSource, Policy);
                    ComputerState = st switch { PolicyState.Enabled => QuickEditState.Enabled, PolicyState.Disabled => QuickEditState.Disabled, _ => QuickEditState.NotConfigured };
                    if (ComputerState == QuickEditState.Enabled)
                    {
                        var opts = PolicyProcessing.GetPolicyOptionStates(_compSource, Policy);
                        LoadOptionValues(opts, false);
                    }
                }
            }
            catch { }
        }

        private void LoadOptionValues(Dictionary<string, object> opts, bool isUser)
        {
            try
            {
                if (EnumElement != null && opts.TryGetValue(EnumElement.ID, out var ev) && ev is int ei) { if (isUser) _userEnumIndex = ei; else _computerEnumIndex = ei; }
                if (BooleanElement != null && opts.TryGetValue(BooleanElement.ID, out var bv) && bv is bool b) { if (isUser) _userBool = b; else _computerBool = b; }
                if (TextElement != null && opts.TryGetValue(TextElement.ID, out var tv) && tv is string ts) { if (isUser) _userText = ts; else _computerText = ts; }
                if (DecimalElement != null && opts.TryGetValue(DecimalElement.ID, out var dv) && dv is uint du) { if (isUser) _userNumber = du; else _computerNumber = du; }
                if (ListElement != null && opts.TryGetValue(ListElement.ID, out var lv) && lv is List<string> ll) { if (isUser) UserListItems = ll.ToList(); else ComputerListItems = ll.ToList(); }
                if (MultiTextElement != null && opts.TryGetValue(MultiTextElement.ID, out var mv) && mv is List<string> ml) { if (isUser) UserMultiTextItems = ml.ToList(); else ComputerMultiTextItems = ml.ToList(); }
            }
            catch { }
        }

        private void QueuePending(string scope)
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
                    if (EnumElement != null)
                    {
                        int idx = isUser ? UserEnumIndex : ComputerEnumIndex;
                        if (idx >= 0) { options ??= new(); options[EnumElement.ID] = idx; }
                    }
                    if (ListElement != null)
                    {
                        var list = isUser ? UserListItems : ComputerListItems;
                        options ??= new(); options[ListElement.ID] = list.ToList();
                    }
                    if (MultiTextElement != null)
                    {
                        var list = isUser ? UserMultiTextItems : ComputerMultiTextItems;
                        options ??= new(); options[MultiTextElement.ID] = list.ToList();
                    }
                    if (BooleanElement != null)
                    {
                        options ??= new(); options[BooleanElement.ID] = isUser ? UserBool : ComputerBool;
                    }
                    if (TextElement != null)
                    {
                        options ??= new(); options[TextElement.ID] = isUser ? UserText : ComputerText;
                    }
                    if (DecimalElement != null)
                    {
                        var num = isUser ? UserNumber : ComputerNumber;
                        if (num.HasValue)
                        {
                            uint v = num.Value;
                            if (v < DecimalMin) v = DecimalMin; if (v > DecimalMax) v = DecimalMax; // clamp
                            options ??= new(); options[DecimalElement.ID] = v;
                        }
                    }
                }
                var action = desired switch { PolicyState.Enabled => "Enable", PolicyState.Disabled => "Disable", _ => "Clear" };

                // Build details (short + full) so PendingChanges shows option values (e.g. text input)
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
                if (desired != PolicyState.Enabled || options == null || options.Count == 0)
                {
                    // For Disable / Clear or no options just echo the action
                    return (action, action);
                }
                var shortSb = new StringBuilder();
                shortSb.Append(action);
                var optionPairs = new List<string>();
                int shown = 0;
                foreach (var kv in options)
                {
                    if (shown >= 4) break; // cap short summary
                    optionPairs.Add(kv.Key + "=" + FormatOpt(kv.Value));
                    shown++;
                }
                if (optionPairs.Count > 0)
                {
                    shortSb.Append(": ");
                    shortSb.Append(string.Join(", ", optionPairs));
                    if (options.Count > optionPairs.Count)
                        shortSb.Append($" (+{options.Count - optionPairs.Count} more)");
                }

                var longSb = new StringBuilder();
                longSb.AppendLine(action);
                try
                {
                    longSb.AppendLine("Registry values:");
                    foreach (var kv in PolicyProcessing.GetReferencedRegistryValues(Policy))
                    {
                        longSb.AppendLine("  ? " + kv.Key + (string.IsNullOrEmpty(kv.Value) ? string.Empty : $" ({kv.Value})"));
                    }
                }
                catch { }
                longSb.AppendLine("Options:");
                foreach (var kv in options)
                {
                    longSb.AppendLine("  - " + kv.Key + " = " + FormatOpt(kv.Value));
                }
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
            if (v is IEnumerable<string> strList) return string.Join("|", strList); // use '|' to keep commas clear in list
            if (v is System.Collections.IEnumerable en && v is not string)
            {
                try { return string.Join(", ", en.Cast<object>().Select(o => Convert.ToString(o) ?? string.Empty)); } catch { }
            }
            return Convert.ToString(v) ?? string.Empty;
        }

        // Restored methods
        public void ReplaceList(bool isUser, List<string> newItems)
        {
            if (isUser) UserListItems = newItems; else ComputerListItems = newItems;
            RefreshListSummaries();
            if (isUser && UserState == QuickEditState.Enabled) QueuePending("User");
            if (!isUser && ComputerState == QuickEditState.Enabled) QueuePending("Computer");
        }

        public void ReplaceMultiText(bool isUser, List<string> newItems)
        {
            if (isUser) UserMultiTextItems = newItems; else ComputerMultiTextItems = newItems;
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
                        ApplyOptionSet(options, true);
                    else if (newState != QuickEditState.Enabled)
                        ClearOptions(true);
                }
                else
                {
                    ComputerState = newState;
                    if (newState == QuickEditState.Enabled && options != null)
                        ApplyOptionSet(options, false);
                    else if (newState != QuickEditState.Enabled)
                        ClearOptions(false);
                }
                RefreshListSummaries();
            }
            catch { }
            finally { _initializing = false; }
        }

        private void ClearOptions(bool isUser)
        {
            if (EnumElement != null)
            {
                if (isUser) _userEnumIndex = -1; else _computerEnumIndex = -1; OnChanged(isUser ? nameof(UserEnumIndex) : nameof(ComputerEnumIndex));
            }
            if (BooleanElement != null)
            { if (isUser) _userBool = false; else _computerBool = false; OnChanged(isUser ? nameof(UserBool) : nameof(ComputerBool)); }
            if (TextElement != null)
            { if (isUser) _userText = string.Empty; else _computerText = string.Empty; OnChanged(isUser ? nameof(UserText) : nameof(ComputerText)); }
            if (DecimalElement != null)
            { if (isUser) _userNumber = DecimalDefault; else _computerNumber = DecimalDefault; OnChanged(isUser ? nameof(UserNumber) : nameof(ComputerNumber)); }
            if (ListElement != null)
            { if (isUser) UserListItems = new(); else ComputerListItems = new(); }
            if (MultiTextElement != null)
            { if (isUser) UserMultiTextItems = new(); else ComputerMultiTextItems = new(); }
        }

        private void ApplyOptionSet(Dictionary<string, object> opts, bool isUser)
        {
            try
            {
                if (EnumElement != null && opts.TryGetValue(EnumElement.ID, out var ev) && ev is int ei)
                { if (isUser) _userEnumIndex = ei; else _computerEnumIndex = ei; OnChanged(isUser ? nameof(UserEnumIndex) : nameof(ComputerEnumIndex)); }
                if (BooleanElement != null && opts.TryGetValue(BooleanElement.ID, out var bv) && bv is bool b)
                { if (isUser) _userBool = b; else _computerBool = b; OnChanged(isUser ? nameof(UserBool) : nameof(ComputerBool)); }
                if (TextElement != null && opts.TryGetValue(TextElement.ID, out var tv) && tv is string ts)
                { if (isUser) _userText = ts; else _computerText = ts; OnChanged(isUser ? nameof(UserText) : nameof(ComputerText)); }
                if (DecimalElement != null && opts.TryGetValue(DecimalElement.ID, out var dv))
                {
                    if (dv is uint du) { if (isUser) _userNumber = du; else _computerNumber = du; }
                    else if (dv is int di && di >= 0) { if (isUser) _userNumber = (uint)di; else _computerNumber = (uint)di; }
                    // Clamp
                    if (_userNumber.HasValue && _userNumber.Value < DecimalMin) _userNumber = DecimalMin;
                    if (_userNumber.HasValue && _userNumber.Value > DecimalMax) _userNumber = DecimalMax;
                    if (_computerNumber.HasValue && _computerNumber.Value < DecimalMin) _computerNumber = DecimalMin;
                    if (_computerNumber.HasValue && _computerNumber.Value > DecimalMax) _computerNumber = DecimalMax;
                    OnChanged(isUser ? nameof(UserNumber) : nameof(ComputerNumber));
                }
                if (ListElement != null && opts.TryGetValue(ListElement.ID, out var lv))
                {
                    if (lv is List<string> ll) { if (isUser) UserListItems = ll.ToList(); else ComputerListItems = ll.ToList(); }
                    else if (lv is IEnumerable<string> enumerable) { var list = enumerable.ToList(); if (isUser) UserListItems = list; else ComputerListItems = list; }
                }
                if (MultiTextElement != null && opts.TryGetValue(MultiTextElement.ID, out var mv))
                {
                    if (mv is List<string> ml) { if (isUser) UserMultiTextItems = ml.ToList(); else ComputerMultiTextItems = ml.ToList(); }
                    else if (mv is IEnumerable<string> en) { var list = en.ToList(); if (isUser) UserMultiTextItems = list; else ComputerMultiTextItems = list; }
                }
            }
            catch { }
        }
    }
}
