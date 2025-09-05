using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.IO;
using PolicyPlus.WinUI3.Services;

namespace PolicyPlus.WinUI3.ViewModels
{
    public enum QuickEditState { NotConfigured, Enabled, Disabled }

    public sealed class QuickEditRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public PolicyPlusPolicy Policy { get; }
        public bool SupportsUser => Policy.RawPolicy.Section == AdmxPolicySection.User || Policy.RawPolicy.Section == AdmxPolicySection.Both;
        public bool SupportsComputer => Policy.RawPolicy.Section == AdmxPolicySection.Machine || Policy.RawPolicy.Section == AdmxPolicySection.Both;

        private QuickEditState _userState; public QuickEditState UserState { get => _userState; set { if (_userState != value) { _userState = value; OnChanged(nameof(UserState)); QueuePending("User"); UpdateEnablement(); } } }
        private QuickEditState _computerState; public QuickEditState ComputerState { get => _computerState; set { if (_computerState != value) { _computerState = value; OnChanged(nameof(ComputerState)); QueuePending("Computer"); UpdateEnablement(); } } }

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
            LoadInitialStates();
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
                        if (num.HasValue) { options ??= new(); options[DecimalElement.ID] = num.Value; }
                    }
                }
                var action = desired switch { PolicyState.Enabled => "Enable", PolicyState.Disabled => "Disable", _ => "Clear" };
                PendingChangesService.Instance.Add(new PendingChange
                {
                    PolicyId = Policy.UniqueID,
                    PolicyName = Policy.DisplayName ?? Policy.UniqueID,
                    Scope = scope,
                    Action = action,
                    Details = action,
                    DetailsFull = action,
                    DesiredState = desired,
                    Options = options
                });
            }
            catch { }
        }

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
    }
}
