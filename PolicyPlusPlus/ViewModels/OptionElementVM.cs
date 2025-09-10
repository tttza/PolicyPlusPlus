using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core; // added for PolicyElement, EnumPolicyElement, etc.

namespace PolicyPlusPlus.ViewModels
{
    public enum OptionElementType { Enum, Boolean, Text, Decimal, List, MultiText }

    public sealed class OptionElementVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public QuickEditRow Parent { get; }
        public OptionElementType Type { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public PolicyElement RawElement { get; }

        // For list elements, indicates explicit name/value pairs (UserProvidesNames)
        public bool ProvidesNames { get; }

        // Defaults captured from presentation (if any)
        private readonly bool? _defaultBool;
        private readonly string? _defaultText;
        private readonly uint? _defaultDecimal;
        private readonly uint _decimalIncrement;
        private readonly int _textMaxLength;
        public bool HasSuggestions => Suggestions.Count > 0;
        public bool IsSuggestText => IsText && HasSuggestions; // for UI toggle
        public bool IsPlainText => IsText && !HasSuggestions;
        public List<string> Suggestions { get; } = new();
        public bool IsRequired { get; }

        public List<string> Choices { get; } = new();
        private int _userEnumIndex = -1; public int UserEnumIndex { get => _userEnumIndex; set { if (_userEnumIndex != value) { _userEnumIndex = value; OnChanged(nameof(UserEnumIndex)); if (Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User"); } } }
        private int _computerEnumIndex = -1; public int ComputerEnumIndex { get => _computerEnumIndex; set { if (_computerEnumIndex != value) { _computerEnumIndex = value; OnChanged(nameof(ComputerEnumIndex)); if (Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer"); } } }

        private bool _userBool; public bool UserBool { get => _userBool; set { if (_userBool != value) { _userBool = value; OnChanged(nameof(UserBool)); if (Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User"); } } }
        private bool _computerBool; public bool ComputerBool { get => _computerBool; set { if (_computerBool != value) { _computerBool = value; OnChanged(nameof(ComputerBool)); if (Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer"); } } }

        private string _userText = string.Empty; public string UserText { get => _userText; set { if (_userText != value) { _userText = value; OnChanged(nameof(UserText)); if (Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User"); } } }
        private string _computerText = string.Empty; public string ComputerText { get => _computerText; set { if (_computerText != value) { _computerText = value; OnChanged(nameof(ComputerText)); if (Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer"); } } }

        public uint DecimalMin { get; }
        public uint DecimalMax { get; }
        public uint DecimalDefault { get; }
        private uint? _userNumber; public uint? UserNumber { get => _userNumber; set { if (_userNumber != value) { _userNumber = value; OnChanged(nameof(UserNumber)); OnChanged(nameof(UserNumberDouble)); if (Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User"); } } }
        private uint? _computerNumber; public uint? ComputerNumber { get => _computerNumber; set { if (_computerNumber != value) { _computerNumber = value; OnChanged(nameof(ComputerNumber)); OnChanged(nameof(ComputerNumberDouble)); if (Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer"); } } }
        public double UserNumberDouble { get => _userNumber.HasValue ? _userNumber.Value : double.NaN; set { uint v = double.IsNaN(value) ? 0u : (uint)Math.Max(0, Math.Round(value)); if (_userNumber != v) { _userNumber = v; OnChanged(nameof(UserNumber)); OnChanged(nameof(UserNumberDouble)); if (Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User"); } } }
        public double ComputerNumberDouble { get => _computerNumber.HasValue ? _computerNumber.Value : double.NaN; set { uint v = double.IsNaN(value) ? 0u : (uint)Math.Max(0, Math.Round(value)); if (_computerNumber != v) { _computerNumber = v; OnChanged(nameof(ComputerNumber)); OnChanged(nameof(ComputerNumberDouble)); if (Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer"); } } }
        public double DecimalMinDouble => DecimalMin;
        public double DecimalMaxDouble => DecimalMax;
        public double DecimalIncrementDouble => _decimalIncrement == 0 ? 1 : _decimalIncrement; // for binding to NumberBox.SmallChange
        public int TextMaxLength => _textMaxLength <= 0 ? int.MaxValue : _textMaxLength;

        public List<string> UserListItems { get; private set; } = new();
        public List<string> ComputerListItems { get; private set; } = new();
        public List<KeyValuePair<string,string>> UserNamedListItems { get; private set; } = new();
        public List<KeyValuePair<string,string>> ComputerNamedListItems { get; private set; } = new();
        public string UserListSummary => Type == OptionElementType.List ? (ProvidesNames ? $"Edit... ({UserNamedListItems.Count})" : $"Edit... ({UserListItems.Count})") : string.Empty;
        public string ComputerListSummary => Type == OptionElementType.List ? (ProvidesNames ? $"Edit... ({ComputerNamedListItems.Count})" : $"Edit... ({ComputerListItems.Count})") : string.Empty;

        public List<string> UserMultiTextItems { get; private set; } = new();
        public List<string> ComputerMultiTextItems { get; private set; } = new();
        public string UserMultiTextSummary => Type == OptionElementType.MultiText ? $"Edit... ({UserMultiTextItems.Count})" : string.Empty;
        public string ComputerMultiTextSummary => Type == OptionElementType.MultiText ? $"Edit... ({ComputerMultiTextItems.Count})" : string.Empty;

        public bool IsEnum => Type == OptionElementType.Enum;
        public bool IsBoolean => Type == OptionElementType.Boolean;
        public bool IsText => Type == OptionElementType.Text;
        public bool IsDecimal => Type == OptionElementType.Decimal;
        public bool IsList => Type == OptionElementType.List;
        public bool IsMultiText => Type == OptionElementType.MultiText;

        internal OptionElementVM(QuickEditRow parent, PolicyElement element, OptionElementType type, string displayName,
            uint decMin = 0, uint decMax = 0, uint decDefault = 0, uint decIncrement = 1,
            bool? defaultBool = null, string? defaultText = null, int textMaxLength = 0, bool providesNames = false,
            IEnumerable<string>? suggestions = null)
        {
            Parent = parent; RawElement = element; Type = type; Id = element.ID; DisplayName = displayName; ProvidesNames = providesNames;
            if (type == OptionElementType.Decimal)
            { DecimalMin = decMin; DecimalMax = decMax; DecimalDefault = decDefault; _defaultDecimal = decDefault; _decimalIncrement = decIncrement; }
            if (type == OptionElementType.Boolean) _defaultBool = defaultBool;
            if (type == OptionElementType.Text) { _defaultText = defaultText; _textMaxLength = textMaxLength; if (suggestions != null) Suggestions.AddRange(suggestions); }
            if (element is TextPolicyElement tpe && tpe.Required) IsRequired = true;
        }

        public void ReplaceList(bool isUser, List<string> items)
        {
            if (Type != OptionElementType.List || ProvidesNames) return;
            if (isUser) UserListItems = items; else ComputerListItems = items;
            OnChanged(isUser ? nameof(UserListSummary) : nameof(ComputerListSummary));
            if (isUser && Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User");
            if (!isUser && Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer");
        }
        public void ReplaceNamedList(bool isUser, List<KeyValuePair<string,string>> items)
        {
            if (Type != OptionElementType.List || !ProvidesNames) return;
            if (isUser) UserNamedListItems = items; else ComputerNamedListItems = items;
            OnChanged(isUser ? nameof(UserListSummary) : nameof(ComputerListSummary));
            if (isUser && Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User");
            if (!isUser && Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer");
        }
        public void ReplaceMultiText(bool isUser, List<string> items)
        {
            if (Type != OptionElementType.MultiText) return;
            if (isUser) UserMultiTextItems = items; else ComputerMultiTextItems = items;
            OnChanged(isUser ? nameof(UserMultiTextSummary) : nameof(ComputerMultiTextSummary));
            if (isUser && Parent.UserState == QuickEditState.Enabled) Parent.QueuePending("User");
            if (!isUser && Parent.ComputerState == QuickEditState.Enabled) Parent.QueuePending("Computer");
        }

        internal object? GetUserValue()
        {
            return Type switch
            {
                OptionElementType.Enum => (UserEnumIndex >= 0 ? UserEnumIndex : null),
                OptionElementType.Boolean => UserBool,
                OptionElementType.Text => UserText,
                OptionElementType.Decimal => UserNumber,
                OptionElementType.List => ProvidesNames ? UserNamedListItems.ToList() : UserListItems.ToList(),
                OptionElementType.MultiText => UserMultiTextItems.ToList(),
                _ => null
            };
        }
        internal object? GetComputerValue()
        {
            return Type switch
            {
                OptionElementType.Enum => (ComputerEnumIndex >= 0 ? ComputerEnumIndex : null),
                OptionElementType.Boolean => ComputerBool,
                OptionElementType.Text => ComputerText,
                OptionElementType.Decimal => ComputerNumber,
                OptionElementType.List => ProvidesNames ? ComputerNamedListItems.ToList() : ComputerListItems.ToList(),
                OptionElementType.MultiText => ComputerMultiTextItems.ToList(),
                _ => null
            };
        }

        internal void LoadFrom(Dictionary<string, object> opts, bool isUser)
        {
            try
            {
                if (!opts.TryGetValue(Id, out var v)) return;
                switch (Type)
                {
                    case OptionElementType.Enum:
                        if (v is int ei)
                        { if (isUser) _userEnumIndex = ei; else _computerEnumIndex = ei; OnChanged(isUser ? nameof(UserEnumIndex) : nameof(ComputerEnumIndex)); }
                        break;
                    case OptionElementType.Boolean:
                        if (v is bool b)
                        { if (isUser) _userBool = b; else _computerBool = b; OnChanged(isUser ? nameof(UserBool) : nameof(ComputerBool)); }
                        break;
                    case OptionElementType.Text:
                        if (v is string s)
                        { if (isUser) _userText = s; else _computerText = s; OnChanged(isUser ? nameof(UserText) : nameof(ComputerText)); }
                        break;
                    case OptionElementType.Decimal:
                        if (v is uint u) { if (isUser) _userNumber = u; else _computerNumber = u; }
                        else if (v is int di && di >= 0) { if (isUser) _userNumber = (uint)di; else _computerNumber = (uint)di; }
                        OnChanged(isUser ? nameof(UserNumber) : nameof(ComputerNumber));
                        break;
                    case OptionElementType.List:
                        if (ProvidesNames)
                        {
                            if (v is Dictionary<string,string> dict)
                            {
                                var list = dict.Select(k => new KeyValuePair<string,string>(k.Key, k.Value)).ToList();
                                if (isUser) UserNamedListItems = list; else ComputerNamedListItems = list;
                                OnChanged(isUser ? nameof(UserListSummary) : nameof(ComputerListSummary));
                            }
                            else if (v is IEnumerable<KeyValuePair<string,string>> kvp)
                            {
                                var list = kvp.ToList(); if (isUser) UserNamedListItems = list; else ComputerNamedListItems = list; OnChanged(isUser ? nameof(UserListSummary) : nameof(ComputerListSummary));
                            }
                        }
                        else
                        {
                            if (v is List<string> ll)
                            { if (isUser) UserListItems = ll.ToList(); else ComputerListItems = ll.ToList(); OnChanged(isUser ? nameof(UserListSummary) : nameof(ComputerListSummary)); }
                            else if (v is IEnumerable<string> en)
                            { var list = en.ToList(); if (isUser) UserListItems = list; else ComputerListItems = list; OnChanged(isUser ? nameof(UserListSummary) : nameof(ComputerListSummary)); }
                        }
                        break;
                    case OptionElementType.MultiText:
                        if (v is List<string> ml)
                        { if (isUser) UserMultiTextItems = ml.ToList(); else ComputerMultiTextItems = ml.ToList(); OnChanged(isUser ? nameof(UserMultiTextSummary) : nameof(ComputerMultiTextSummary)); }
                        else if (v is IEnumerable<string> en2)
                        { var list2 = en2.ToList(); if (isUser) UserMultiTextItems = list2; else ComputerMultiTextItems = list2; OnChanged(isUser ? nameof(UserMultiTextSummary) : nameof(ComputerMultiTextSummary)); }
                        break;
                }
            }
            catch { }
        }

        internal void InitializeEnumChoices(EnumPolicyElement ee, Func<string, AdmxFile, string> resolve, AdmxFile definedIn)
        {
            try
            {
                Choices.Clear();
                foreach (var it in ee.Items)
                {
                    string txt;
                    try { txt = resolve(it.DisplayCode, definedIn); } catch { txt = it.DisplayCode; }
                    Choices.Add(string.IsNullOrWhiteSpace(txt) ? it.DisplayCode : txt);
                }
            }
            catch { }
        }

        internal void EnsureDefaultsAfterLoad()
        {
            if (Type == OptionElementType.Enum)
            {
                if (_userEnumIndex == -1 && Choices.Count > 0) _userEnumIndex = 0;
                if (_computerEnumIndex == -1 && Choices.Count > 0) _computerEnumIndex = 0;
                OnChanged(nameof(UserEnumIndex)); OnChanged(nameof(ComputerEnumIndex));
            }
            if (Type == OptionElementType.Decimal)
            {
                if (!_userNumber.HasValue) _userNumber = _defaultDecimal ?? DecimalDefault; OnChanged(nameof(UserNumber)); OnChanged(nameof(UserNumberDouble));
                if (!_computerNumber.HasValue) _computerNumber = _defaultDecimal ?? DecimalDefault; OnChanged(nameof(ComputerNumber)); OnChanged(nameof(ComputerNumberDouble));
            }
            if (Type == OptionElementType.Boolean)
            {
                if (_defaultBool.HasValue)
                {
                    if (!_userBool) { _userBool = _defaultBool.Value; OnChanged(nameof(UserBool)); }
                    if (!_computerBool) { _computerBool = _defaultBool.Value; OnChanged(nameof(ComputerBool)); }
                }
            }
            if (Type == OptionElementType.Text)
            {
                if (!string.IsNullOrEmpty(_defaultText))
                {
                    if (string.IsNullOrEmpty(_userText)) { _userText = _defaultText; OnChanged(nameof(UserText)); }
                    if (string.IsNullOrEmpty(_computerText)) { _computerText = _defaultText; OnChanged(nameof(ComputerText)); }
                }
            }
        }
    }
}
