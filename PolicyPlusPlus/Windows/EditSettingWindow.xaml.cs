using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WinRT.Interop;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.Services;
using Microsoft.UI.Xaml.Input;
using PolicyPlusCore.IO;
using PolicyPlusCore.Core;
using PolicyPlusCore.Admx;
using PolicyPlusPlus.ViewModels; // added for QuickEditRow
using PolicyPlusPlus.Logging; // logging

namespace PolicyPlusPlus.Windows
{
    public sealed partial class EditSettingWindow : Window
    {
        // Data
        private PolicyPlusPolicy _policy = null!;
        private AdmxPolicySection _currentSection;
        private AdmxBundle _bundle = null!;
        private IPolicySource _compSource = null!;
        private IPolicySource _userSource = null!;
        private PolicyLoader _compLoader = null!;
        private PolicyLoader _userLoader = null!;
        private Dictionary<string, string>? _compComments;
        private Dictionary<string, string>? _userComments;

        private readonly Dictionary<string, FrameworkElement> _elementControls = new();
        private readonly List<ListEditorWindow> _childEditors = new();
        private readonly List<Window> _childWindows = new();

        public event EventHandler? Saved;
        public event EventHandler<(string Scope, PolicyState State, Dictionary<string, object>? Options)>? SavedDetail; // added

        private static readonly Regex UrlRegex = new Regex(@"(https?://[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private bool _useSecondLanguage = false;
        private ToggleButton? _secondLangToggle;

        private PolicyState _lastLoadedState = PolicyState.Unknown; // track last loaded state to know when enabling fresh

        public EditSettingWindow()
        {
            InitializeComponent();
            Title = "Edit Policy Setting";

            // Centralized common window setup
            ChildWindowCommon.Initialize(this, 800, 600, ApplyWindowTheme);

            RootShell.Loaded += (s, e) =>
            {
                try
                {
                    _secondLangToggle = RootShell.FindName("SecondLangToggle") as ToggleButton;
                    if (_secondLangToggle != null)
                    {
                        _secondLangToggle.Checked += SecondLangToggle_Checked;
                        _secondLangToggle.Unchecked += SecondLangToggle_Checked;
                        try
                        {
                            var st = SettingsService.Instance.LoadSettings();
                            bool enabled = st.SecondLanguageEnabled ?? false;
                            _secondLangToggle.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
                            string code = st.SecondLanguage ?? "en-US";
                            ToolTipService.SetToolTip(_secondLangToggle, enabled ? $"Toggle 2nd language ({code})" : "2nd language disabled in preferences");
                        }
                        catch { }
                    }
                }
                catch { }
            };

            SectionSelector.SelectionChanged += SectionSelector_SelectionChanged;
            OptNotConfigured.Checked += StateRadio_Checked;
            OptEnabled.Checked += StateRadio_Checked;
            OptDisabled.Checked += StateRadio_Checked;
            ViewDetailApplyBtn.Click += ViewDetailApplyBtn_Click;
            ApplyBtn.Click += ApplyBtn_Click;
            OkBtn.Click += OkBtn_Click;
            CancelBtn.Click += (s, e) => Close();
        }

        // Overlay current QuickEditRow (unsaved) values into this window (both scopes).
        internal void OverlayFromQuickEdit(QuickEditRow row)
        {
            if (row == null || _policy == null || !string.Equals(row.Policy.UniqueID, _policy.UniqueID, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                void ApplyScope(bool isUser)
                {
                    _currentSection = isUser ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                    if (SectionSelector != null)
                    {
                        SectionSelector.SelectedIndex = isUser ? 1 : 0; // 0=Computer,1=User
                    }
                    LoadStateFromSource();
                    StateRadio_Checked(this, new RoutedEventArgs());
                    var state = isUser ? row.UserState : row.ComputerState;
                    if (OptEnabled != null) OptEnabled.IsChecked = state == QuickEditState.Enabled;
                    if (OptDisabled != null) OptDisabled.IsChecked = state == QuickEditState.Disabled;
                    if (OptNotConfigured != null) OptNotConfigured.IsChecked = state == QuickEditState.NotConfigured;
                    StateRadio_Checked(this, new RoutedEventArgs());
                    if (state != QuickEditState.Enabled) return;

                    // Map each option element dynamically (new multi-element model)
                    foreach (var opt in row.OptionElements)
                    {
                        if (!_elementControls.TryGetValue(opt.Id, out var ctrl) || ctrl == null) continue;
                        switch (opt.Type)
                        {
                            case OptionElementType.Enum:
                                if (ctrl is ComboBox cb)
                                    cb.SelectedIndex = isUser ? opt.UserEnumIndex : opt.ComputerEnumIndex;
                                break;
                            case OptionElementType.Boolean:
                                if (ctrl is CheckBox chk)
                                    chk.IsChecked = isUser ? opt.UserBool : opt.ComputerBool;
                                break;
                            case OptionElementType.Text:
                                string txt = isUser ? opt.UserText : opt.ComputerText;
                                if (ctrl is TextBox tb) tb.Text = txt;
                                else if (ctrl is AutoSuggestBox asb) asb.Text = txt;
                                break;
                            case OptionElementType.Decimal:
                                var val = isUser ? opt.UserNumber : opt.ComputerNumber;
                                if (val.HasValue && ctrl is NumberBox nb) nb.Value = val.Value;
                                break;
                            case OptionElementType.List:
                                if (ctrl is Button btn)
                                {
                                    var items = isUser ? opt.UserListItems : opt.ComputerListItems;
                                    btn.Tag = items.ToList();
                                    btn.Content = $"Edit... ({items.Count})";
                                }
                                break;
                            case OptionElementType.MultiText:
                                if (ctrl is TextBox mtb)
                                {
                                    var lines = isUser ? opt.UserMultiTextItems : opt.ComputerMultiTextItems;
                                    mtb.Text = string.Join("\r\n", lines);
                                }
                                break;
                        }
                    }
                }

                if (_policy.RawPolicy.Section == AdmxPolicySection.Both)
                {
                    ApplyScope(false);
                    ApplyScope(true);
                    var preferUser = row.UserState == QuickEditState.Enabled && row.ComputerState != QuickEditState.Enabled;
                    SectionSelector.SelectedIndex = preferUser ? 1 : 0;
                    _currentSection = SectionSelector.SelectedIndex == 1 ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                }
                else
                {
                    ApplyScope(_policy.RawPolicy.Section == AdmxPolicySection.User);
                }
            }
            catch { }
        }

        private void SecondLangToggle_Checked(object sender, RoutedEventArgs e)
        {
            var t = sender as ToggleButton;
            _useSecondLanguage = t?.IsChecked == true;
            RefreshLocalizedTexts();
        }

        private void RefreshLocalizedTexts()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                bool enabled = s.SecondLanguageEnabled ?? false;
                string lang = s.SecondLanguage ?? "en-US";
                bool useSecond = _useSecondLanguage && enabled;

                // Title and explanation
                if (useSecond)
                {
                    SettingTitle.Text = LocalizedTextService.GetPolicyNameIn(_policy, lang);
                    SetExplanationText(LocalizedTextService.GetPolicyExplanationIn(_policy, lang));
                }
                else
                {
                    SettingTitle.Text = _policy.DisplayName;
                    SetExplanationText(_policy.DisplayExplanation ?? string.Empty);
                }

                // Supported on
                if (useSecond)
                {
                    var sup = LocalizedTextService.GetSupportedDisplayIn(_policy, lang);
                    SupportedBox.Text = string.IsNullOrEmpty(sup) ? (_policy.SupportedOn?.DisplayName ?? string.Empty) : sup;
                }
                else
                {
                    SupportedBox.Text = _policy.SupportedOn is null ? string.Empty : _policy.SupportedOn.DisplayName;
                }

                // Update tooltip to reflect current language code
                try
                {
                    if (_secondLangToggle != null)
                        ToolTipService.SetToolTip(_secondLangToggle, enabled ? $"Toggle 2nd language ({lang})" : "2nd language disabled in preferences");
                }
                catch { }

                // Rebuild option labels/controls for second language when toggled
                if (_policy.RawPolicy.Elements != null)
                {
                    var pres = useSecond ? LocalizedTextService.GetPresentationIn(_policy, lang) : null;
                    if (pres != null)
                    {
                        var original = _policy.Presentation;
                        _policy.Presentation = pres;
                        BuildElements();
                        LoadStateFromSource();
                        StateRadio_Checked(this, null!);
                        _policy.Presentation = original; // restore
                    }
                    else
                    {
                        BuildElements();
                        LoadStateFromSource();
                        StateRadio_Checked(this, null!);
                    }
                }
            }
            catch { }
        }

        private void Accel_Apply(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { ApplyBtn_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }
        private void Accel_Ok(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { OkBtn_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }
        private void Accel_Preview(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { ViewDetailApplyBtn_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }
        private void Accel_SectionComp(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { if (SectionSelector!=null) SectionSelector.SelectedIndex = 0; } catch { } args.Handled = true; }
        private void Accel_SectionUser(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { if (SectionSelector!=null) SectionSelector.SelectedIndex = 1; } catch { } args.Handled = true; }
        private void Accel_Close(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { Close(); } catch { } args.Handled = true; }

        private void ApplyWindowTheme()
        {
            try
            {
                if (Content is FrameworkElement fe)
                    fe.RequestedTheme = App.CurrentTheme;
                var textBg = Application.Current.Resources["TextControlBackground"] as Brush;
                var textFg = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
                CommentBox.Background = textBg; CommentBox.Foreground = textFg;
                SupportedBox.Background = textBg; SupportedBox.Foreground = textFg;
            }
            catch { }
        }

        public void Initialize(PolicyPlusPolicy policy, AdmxPolicySection section, AdmxBundle bundle,
            IPolicySource compSource, IPolicySource userSource, PolicyLoader compLoader, PolicyLoader userLoader,
            Dictionary<string, string>? compComments, Dictionary<string, string>? userComments)
        {
            _policy = policy; _currentSection = section; _bundle = bundle;
            _compSource = compSource; _userSource = userSource; _compLoader = compLoader; _userLoader = userLoader;
            _compComments = compComments; _userComments = userComments;

            SettingTitle.Text = policy.DisplayName;
            SupportedBox.Text = policy.SupportedOn is null ? string.Empty : policy.SupportedOn.DisplayName;
            SetExplanationText(policy.DisplayExplanation ?? string.Empty);

            // Determine initial section selection. If the policy supports both and the Computer scope is already configured,
            // prefer Computer regardless of the requested section.
            var initialSection = section;
            try
            {
                if (policy.RawPolicy.Section == AdmxPolicySection.Both)
                {
                    var compState = PolicyProcessing.GetPolicyState(_compSource, _policy);
                    bool compConfigured = compState == PolicyState.Enabled || compState == PolicyState.Disabled;
                    if (compConfigured)
                    {
                        initialSection = AdmxPolicySection.Machine;
                    }
                }
            }
            catch { }
            _currentSection = initialSection;

            SectionSelector.IsEnabled = policy.RawPolicy.Section == AdmxPolicySection.Both;
            SectionSelector.SelectedIndex = (_currentSection == AdmxPolicySection.Machine) ? 0 : 1;

            BuildElements();
            LoadStateFromSource();
            ApplyPendingOverlayFromQueue();
            StateRadio_Checked(this, null!);

            WindowHelpers.BringToFront(this);
            var tmr = this.DispatcherQueue.CreateTimer();
            tmr.Interval = System.TimeSpan.FromMilliseconds(180);
            tmr.IsRepeating = false;
            tmr.Tick += (s, e) => { try { WindowHelpers.BringToFront(this); this.Activate(); } catch { } }; tmr.Start();
            App.RegisterEditWindow(_policy.UniqueID, this);
        }

        private void ApplyPendingOverlayFromQueue()
        {
            try
            {
                var scope = (_currentSection == AdmxPolicySection.User) ? "User" : "Computer";
                var change = PendingChangesService.Instance.Pending.FirstOrDefault(p => string.Equals(p.PolicyId, _policy.UniqueID, StringComparison.OrdinalIgnoreCase)
                                                                                    && string.Equals(p.Scope, scope, StringComparison.OrdinalIgnoreCase));
                if (change == null) return;

                // Apply desired state
                if (change.DesiredState == PolicyState.Enabled)
                {
                    OptEnabled.IsChecked = true;
                }
                else if (change.DesiredState == PolicyState.Disabled)
                {
                    OptDisabled.IsChecked = true;
                }
                else
                {
                    OptNotConfigured.IsChecked = true;
                }

                // Apply option values if any
                if (change.Options != null)
                    ApplyOptionsToControls(change.Options);
            }
            catch { }
        }

        private void ApplyOptionsToControls(Dictionary<string, object> options)
        {
            foreach (var kv in options)
            {
                if (!_elementControls.TryGetValue(kv.Key, out var ctrl)) continue;
                var val = kv.Value;
                switch (ctrl)
                {
                    case NumberBox nb:
                        if (val is uint u) nb.Value = u;
                        else if (val is int i) nb.Value = i;
                        else if (val is string s && double.TryParse(s, out var d)) nb.Value = d;
                        break;
                    case TextBox tb:
                        if (val is string s1) tb.Text = s1;
                        else if (val is string[] arr) tb.Text = string.Join("\r\n", arr);
                        break;
                    case AutoSuggestBox asb:
                        asb.Text = Convert.ToString(val) ?? string.Empty;
                        break;
                    case ComboBox cb:
                        if (val is int idx && idx >= -1 && idx < cb.Items.Count) cb.SelectedIndex = idx;
                        break;
                    case CheckBox ch:
                        if (val is bool b) ch.IsChecked = b; else ch.IsChecked = (Convert.ToString(val)?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);
                        break;
                    case Button btn:
                        if (val is List<string> list)
                        { btn.Tag = list; btn.Content = $"Edit... ({list.Count})"; }
                        else if (val is IEnumerable<KeyValuePair<string, string>> kvpList)
                        { var l = kvpList.ToList(); btn.Tag = l; btn.Content = $"Edit... ({l.Count})"; }
                        break;
                }
            }
        }

        private void SetExplanationText(string text)
        {
            ExplainText.Blocks.Clear();
            var para = new Paragraph();
            if (string.IsNullOrEmpty(text)) { ExplainText.Blocks.Add(para); return; }

            int lastIndex = 0;
            foreach (Match m in UrlRegex.Matches(text))
            {
                if (m.Index > lastIndex)
                {
                    var before = text.Substring(lastIndex, m.Index - lastIndex);
                    para.Inlines.Add(new Run { Text = before });
                }

                string url = m.Value;
                var link = new Hyperlink();
                link.Inlines.Add(new Run { Text = url });
                link.Click += async (s, e) =>
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        try { await global::Windows.System.Launcher.LaunchUriAsync(uri); } catch { }
                    }
                };
                para.Inlines.Add(link);
                lastIndex = m.Index + m.Length;
            }
            if (lastIndex < text.Length)
            {
                para.Inlines.Add(new Run { Text = text.Substring(lastIndex) });
            }
            ExplainText.Blocks.Add(para);
        }

        private (IPolicySource src, PolicyLoader loader, Dictionary<string, string>? comments) GetCurrent()
        {
            bool user = _currentSection == AdmxPolicySection.User;
            return (user ? _userSource : _compSource, user ? _userLoader : _compLoader, user ? _userComments : _compComments);
        }

        private void BuildElements()
        {
            _elementControls.Clear();
            OptionsPanel.Children.Clear();
            if (_policy.RawPolicy.Elements is null || _policy.Presentation is null)
            {
                return;
            }

            // Decide language context
            var settings = SettingsService.Instance.LoadSettings();
            bool useSecond = _useSecondLanguage && (settings.SecondLanguageEnabled ?? false);
            string lang = useSecond ? (settings.SecondLanguage ?? "en-US") : (settings.Language ?? System.Globalization.CultureInfo.CurrentUICulture.Name);

            // Choose presentation (localized when available)
            Presentation presentationToUse = _policy.Presentation;
            Presentation? originalPresentation = _policy.Presentation; // keep reference
            try
            {
                if (useSecond)
                {
                    var lp = LocalizedTextService.GetPresentationIn(_policy, lang);
                    if (lp != null)
                    {
                        // Merge missing numeric defaults: some localized ADMLs omit defaultValue so it becomes 0.
                        try
                        {
                            foreach (var locElem in lp.Elements)
                            {
                                if (locElem.ElementType == "decimalTextBox" && locElem is NumericBoxPresentationElement nbLoc && nbLoc.DefaultValue == 0 && originalPresentation != null)
                                {
                                    var baseElem = originalPresentation.Elements.FirstOrDefault(e => e.ElementType == "decimalTextBox" && string.Equals(e.ID, locElem.ID, StringComparison.OrdinalIgnoreCase)) as NumericBoxPresentationElement;
                                    if (baseElem != null && baseElem.DefaultValue > 0)
                                    {
                                        nbLoc.DefaultValue = baseElem.DefaultValue; // copy over intended default (e.g., 1400)
                                    }
                                }
                            }
                        }
                        catch { }
                        presentationToUse = lp;
                    }
                }
            }
            catch { }

            string ResolvePresString(PresentationElement pres, string? fallback)
            {
                if (!string.IsNullOrWhiteSpace(fallback)) return fallback!;
                if (pres == null || string.IsNullOrEmpty(pres.ID)) return fallback ?? string.Empty;
                var viaString = LocalizedTextService.ResolveString("$(string." + pres.ID + ")", _policy.RawPolicy.DefinedIn, lang);
                if (!string.IsNullOrEmpty(viaString)) return viaString;
                return fallback ?? string.Empty;
            }

            var elemDict = _policy.RawPolicy.Elements.ToDictionary(e => e.ID);
            foreach (var pres in presentationToUse.Elements)
            {
                FrameworkElement control = null!;
                string label = string.Empty;
                switch (pres.ElementType)
                {
                    case "text":
                        {
                            var p = (LabelPresentationElement)pres;
                            label = ResolvePresString(pres, p.Text);
                            control = new TextBlock { Text = label };
                            label = string.Empty; // already placed as a TextBlock
                            break;
                        }
                    case "decimalTextBox":
                        {
                            var p = (NumericBoxPresentationElement)pres;
                            var e = (DecimalPolicyElement)elemDict[pres.ID];
                            // Always use NumberBox; show spin buttons so it is visually distinct from TextBox
                            var nb = new NumberBox { Minimum = e.Minimum, Maximum = e.Maximum, Value = p.DefaultValue, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
                            if (p.HasSpinner) nb.SmallChange = p.SpinnerIncrement;
                            else nb.SmallChange = 1; // sensible default
                            control = nb;
                            label = ResolvePresString(pres, p.Label); break;
                        }
                    case "textBox":
                        {
                            var p = (TextBoxPresentationElement)pres;
                            var e = (TextPolicyElement)elemDict[pres.ID];
                            var tb = new TextBox { Text = p.DefaultValue ?? string.Empty, MaxLength = e.MaxLength };
                            control = tb; label = ResolvePresString(pres, p.Label); break;
                        }
                    case "checkBox":
                        {
                            var p = (CheckBoxPresentationElement)pres;
                            var text = ResolvePresString(pres, p.Text);
                            var cb = new CheckBox { Content = text, IsChecked = p.DefaultState };
                            control = cb; label = string.Empty; break;
                        }
                    case "comboBox":
                        {
                            var p = (ComboBoxPresentationElement)pres;
                            var e = (TextPolicyElement)elemDict[pres.ID];
                            var acb = new AutoSuggestBox { Text = p.DefaultText ?? string.Empty };
                            // Suggestions may be in ADML; attempt to resolve each if they look like IDs
                            var list = new List<string>();
                            foreach (var s in p.Suggestions)
                            {
                                var resolved = LocalizedTextService.ResolveString(s, _policy.RawPolicy.DefinedIn, lang);
                                list.Add(string.IsNullOrEmpty(resolved) ? s : resolved);
                            }
                            acb.ItemsSource = list;
                            control = acb; label = ResolvePresString(pres, p.Label); break;
                        }
                    case "dropdownList":
                        {
                            var p = (DropDownPresentationElement)pres;
                            var e = (EnumPolicyElement)elemDict[pres.ID];
                            label = ResolvePresString(pres, p.Label);
                            var cb = new ComboBox { MinWidth = 160 };
                            int selectedIdx = 0;
                            int curIdx = 0;
                            foreach (var enumItem in e.Items)
                            {
                                string text;
                                try
                                {
                                    text = useSecond
                                        ? LocalizedTextService.ResolveString(enumItem.DisplayCode, _policy.RawPolicy.DefinedIn, lang)
                                        : _bundle.ResolveString(enumItem.DisplayCode, _policy.RawPolicy.DefinedIn);
                                }
                                catch
                                {
                                    text = _bundle.ResolveString(enumItem.DisplayCode, _policy.RawPolicy.DefinedIn);
                                }
                                var cbi = new ComboBoxItem { Content = string.IsNullOrWhiteSpace(text) ? enumItem.DisplayCode : text };
                                cb.Items.Add(cbi);
                                if (p.DefaultItemID.HasValue && curIdx == p.DefaultItemID.Value)
                                    selectedIdx = curIdx;
                                curIdx++;
                            }
                            if (cb.Items.Count > 0)
                                cb.SelectedIndex = selectedIdx;
                            cb.Loaded += (s, e2) =>
                            {
                                if (cb.SelectedIndex < 0 && cb.Items.Count > 0)
                                    cb.SelectedIndex = selectedIdx;
                            };
                            control = cb; break;
                        }
                    case "listBox":
                        {
                            var p = (ListPresentationElement)pres;
                            var e = (ListPolicyElement)elemDict[pres.ID];
                            var btn = new Button { Content = "Edit..." };
                            btn.Click += (s, e2) =>
                            {
                                string key = _policy.UniqueID + ":" + pres.ID;
                                if (ListEditorWindow.TryActivateExisting(key)) return;
                                var win = new ListEditorWindow();
                                ListEditorWindow.Register(key, win);
                                _childEditors.Add(win); _childWindows.Add(win);
                                win.Closed += (ss, ee) => { _childEditors.Remove(win); _childWindows.Remove(win); };
                                win.Initialize(ResolvePresString(pres, p.Label), e.UserProvidesNames, btn.Tag);
                                win.Finished += (ss, ok) =>
                                {
                                    if (!ok) return;
                                    btn.Tag = win.Result;
                                    if (win.CountText != null) btn.Content = win.CountText;
                                };
                                win.Activate();
                                WindowHelpers.BringToFront(win);
                                var t = win.DispatcherQueue.CreateTimer();
                                t.Interval = System.TimeSpan.FromMilliseconds(180);
                                t.IsRepeating = false;
                                t.Tick += (sss, eee) => { try { WindowHelpers.BringToFront(win); win.Activate(); } catch { } }; t.Start();
                            };
                            control = btn; label = ResolvePresString(pres, p.Label); break;
                        }
                    case "multiTextBox":
                        {
                            var p = (MultiTextPresentationElement)pres;
                            var tb = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 120, Text = string.Empty };
                            control = tb; label = ResolvePresString(pres, p.Label); break;
                        }
                }
                if (control != null)
                {
                    if (!string.IsNullOrEmpty(label))
                    {
                        var lbl = new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                        OptionsPanel.Children.Add(lbl);
                    }
                    OptionsPanel.Children.Add(control);
                    _elementControls[pres.ID] = control;
                }
            }
        }

        private void LoadStateFromSource()
        {
            var (src, loader, comments) = GetCurrent();
            var state = PolicyProcessing.GetPolicyState(src, _policy);
            _lastLoadedState = state;
            OptEnabled.IsChecked = state == PolicyState.Enabled;
            OptDisabled.IsChecked = state == PolicyState.Disabled;
            OptNotConfigured.IsChecked = state == PolicyState.NotConfigured || state == PolicyState.Unknown;

            Dictionary<string, object> optionStates = new();
            if (state == PolicyState.Enabled)
            {
                try { optionStates = PolicyProcessing.GetPolicyOptionStates(src, _policy); } catch { optionStates = new(); }
            }

            foreach (var elem in _policy.RawPolicy.Elements ?? new List<PolicyElement>())
            {
                if (!_elementControls.TryGetValue(elem.ID, out var ctrl)) continue;

                if (state == PolicyState.Enabled && optionStates.TryGetValue(elem.ID, out var val))
                {
                    switch (ctrl)
                    {
                        case NumberBox nb when val is uint u: nb.Value = u; continue;
                        case TextBox tb when val is string s: tb.Text = s; continue;
                        case ComboBox cb when val is int index: cb.SelectedIndex = index; continue;
                        case CheckBox ch when val is bool b: ch.IsChecked = b; continue;
                        case TextBox mtb when val is string[] arr: mtb.Text = string.Join("\r\n", arr); continue;
                        case Button btn when val is Dictionary<string, string> dict: btn.Tag = dict.ToList(); btn.Content = $"Edit... ({dict.Count})"; continue;
                        case Button btn2 when val is List<string> list: btn2.Tag = list; btn2.Content = $"Edit... ({list.Count})"; continue;
                    }
                }

                // Not Enabled OR no stored value: presentation defaults
                if (elem.ElementType == "decimal" && ctrl is NumberBox nb2)
                {
                    var def = GetDecimalDefault(elem.ID);
                    nb2.Value = ClampDecimal(elem, def);
                }
                else if (elem is TextPolicyElement && ctrl is TextBox tb2)
                {
                    if (string.IsNullOrEmpty(tb2.Text))
                    {
                        var defText = GetTextDefault(elem.ID);
                        if (defText != null) tb2.Text = defText;
                    }
                }
                else if (elem is BooleanPolicyElement && ctrl is CheckBox ch2)
                {
                    // BuildElements already applied the presentation default; nothing further if already set.
                    // (We keep this branch for symmetry and potential future logic.)
                    _ = ch2; // no-op
                }
            }

            if (comments == null)
            {
                CommentBox.IsEnabled = false; CommentBox.Text = "Comments unavailable for this policy source";
            }
            else
            {
                CommentBox.IsEnabled = true; CommentBox.Text = comments.TryGetValue(_policy.UniqueID, out var c) ? c : string.Empty;
            }
        }

        private string? GetTextDefault(string id)
        {
            try
            {
                if (_policy.Presentation != null)
                {
                    foreach (var pe in _policy.Presentation.Elements)
                    {
                        if (pe.ElementType == "textBox" && string.Equals(pe.ID, id, StringComparison.OrdinalIgnoreCase) && pe is TextBoxPresentationElement tpe)
                            return tpe.DefaultValue;
                    }
                }
            }
            catch { }
            return null;
        }

        private uint GetDecimalDefault(string id)
        {
            try
            {
                if (_policy.Presentation != null)
                {
                    foreach (var pe in _policy.Presentation.Elements)
                    {
                        if (pe.ElementType == "decimalTextBox" && string.Equals(pe.ID, id, StringComparison.OrdinalIgnoreCase))
                        {
                            if (pe is NumericBoxPresentationElement nbpe)
                                return nbpe.DefaultValue;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        private uint ClampDecimal(PolicyElement elem, double value)
        {
            try
            {
                if (elem is DecimalPolicyElement de)
                {
                    if (value < de.Minimum) value = de.Minimum;
                    if (value > de.Maximum) value = de.Maximum; // fixed typo
                    return (uint)Math.Round(value);
                }
            }
            catch { }
            return (uint)Math.Max(0, Math.Round(value));
        }

        private Dictionary<string, object> CollectOptions()
        {
            var options = new Dictionary<string, object>();
            if (_policy.RawPolicy.Elements is null) return options;
            foreach (var elem in _policy.RawPolicy.Elements)
            {
                if (!_elementControls.TryGetValue(elem.ID, out var ctrl)) continue;
                switch (elem.ElementType)
                {
                    case "decimal":
                        if (ctrl is NumberBox nb)
                        {
                            options[elem.ID] = ClampDecimal(elem, nb.Value);
                        }
                        else if (ctrl is TextBox tbx && uint.TryParse(tbx.Text, out var u)) options[elem.ID] = ClampDecimal(elem, u); else options[elem.ID] = 0u; break;
                    case "text":
                        if (ctrl is AutoSuggestBox asb) options[elem.ID] = asb.Text; else if (ctrl is TextBox tbx) options[elem.ID] = tbx.Text; break;
                    case "boolean": options[elem.ID] = (ctrl as CheckBox)?.IsChecked == true; break;
                    case "enum": options[elem.ID] = (ctrl as ComboBox)?.SelectedIndex ?? -1; break;
                    case "list":
                        var tag = (ctrl as Button)?.Tag; if (tag is List<string> l) options[elem.ID] = l; else if (tag is List<KeyValuePair<string, string>> kvpList) options[elem.ID] = kvpList; break;
                    case "multiText":
                        var text = (ctrl as TextBox)?.Text ?? string.Empty; options[elem.ID] = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None); break;
                }
            }
            return options;
        }

        private void SaveToSource()
        {
            var (src, loader, comments) = GetCurrent();

            string action = "Clear";
            string details = string.Empty;
            string detailsFull = string.Empty;
            PolicyState desired = PolicyState.NotConfigured;
            Dictionary<string, object>? options = null;

            if (OptEnabled.IsChecked == true)
            {
                options = CollectOptions();
                desired = PolicyState.Enabled;
                action = "Enable";
                (details, detailsFull) = BuildDetailsForPending(options);
            }
            else if (OptDisabled.IsChecked == true)
            {
                desired = PolicyState.Disabled;
                action = "Disable";
                details = "Disabled";
                detailsFull = $"Disabled";
            }
            else
            {
                desired = PolicyState.NotConfigured;
                action = "Clear";
                details = "Not Configured";
                detailsFull = $"Not Configured";
            }

            if (comments != null)
            {
                var text = CommentBox.Text ?? string.Empty;
                if (string.IsNullOrEmpty(text)) comments.Remove(_policy.UniqueID); else comments[_policy.UniqueID] = text;
            }

            try
            {
                var scope = (_currentSection == AdmxPolicySection.User) ? "User" : "Computer";
                PendingChangesService.Instance.Add(new PendingChange
                {
                    PolicyId = _policy.UniqueID,
                    PolicyName = _policy.DisplayName ?? _policy.UniqueID,
                    Scope = scope,
                    Action = action,
                    Details = details,
                    DetailsFull = detailsFull,
                    DesiredState = desired,
                    Options = options
                });
            }
            catch { }

            try
            {
                // If a PendingChangesWindow is open, refresh it proactively
                if (App.Window is MainWindow main)
                {
                    foreach (var field in typeof(App).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
                    {
                        // no direct list, but we can find a window by type among current windows is not straightforward in WinUI
                    }
                }
            }
            catch { }

            Saved?.Invoke(this, EventArgs.Empty);
            try
            {
                var scope = (_currentSection == AdmxPolicySection.User) ? "User" : "Computer";
                SavedDetail?.Invoke(this, (scope, desired, options));
            }
            catch { }
        }

        private (string shortText, string longText) BuildDetailsForPending(Dictionary<string, object> options)
        {
            try
            {
                var shortSb = new StringBuilder();
                shortSb.Append("Enable");
                if (options != null && options.Count > 0)
                {
                    var pairs = options.Select(kv => kv.Key + "=" + FormatOpt(kv.Value));
                    shortSb.Append(": ");
                    shortSb.Append(string.Join(", ", pairs.Take(4)));
                    if (options.Count > 4) shortSb.Append($" (+{options.Count - 4} more)");
                }

                var longSb = new StringBuilder();
                // Omit policy name and scope per request
                longSb.AppendLine("Registry values:");
                foreach (var kv in PolicyProcessing.GetReferencedRegistryValues(_policy))
                {
                    longSb.AppendLine("  ? " + kv.Key + (string.IsNullOrEmpty(kv.Value) ? string.Empty : $" ({kv.Value})"));
                }
                if (options != null && options.Count > 0)
                {
                    longSb.AppendLine("Options:");
                    foreach (var kv in options)
                        longSb.AppendLine("  - " + kv.Key + " = " + FormatOpt(kv.Value));
                }
                return (shortSb.ToString(), longSb.ToString());
            }
            catch { return (string.Empty, string.Empty); }
        }

        private static string FormatOpt(object v)
        {
            if (v == null) return string.Empty;
            if (v is string s) return s;
            if (v is bool b) return b ? "true" : "false";

            // Common list and map shapes coming from CollectOptions
            if (v is IEnumerable<string> strList) return string.Join(", ", strList);
            if (v is IEnumerable<KeyValuePair<string, string>> kvList)
                return string.Join(", ", kvList.Select(kv => kv.Key + "=" + kv.Value));

            if (v is Array arr)
            {
                var items = arr.Cast<object>().Select(o => Convert.ToString(o) ?? string.Empty);
                return string.Join(", ", items);
            }

            if (v is System.Collections.IEnumerable en && v is not string)
            {
                var items = en.Cast<object>().Select(FormatOpt);
                return string.Join(", ", items);
            }

            return Convert.ToString(v) ?? string.Empty;
        }

        private void SectionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentSection = SectionSelector.SelectedIndex == 1 ? AdmxPolicySection.User : AdmxPolicySection.Machine;
            LoadStateFromSource();
        }

        private void StateRadio_Checked(object sender, RoutedEventArgs e)
        {
            bool enableOptions = OptEnabled.IsChecked == true;
            ToggleChildrenEnabled(OptionsPanel, enableOptions);

            // If user just switched to Enabled from a non-enabled state, populate default values for decimal boxes that have not been set yet.
            if (enableOptions && _lastLoadedState != PolicyState.Enabled)
            {
                try
                {
                    var (src, _, _) = GetCurrent();
                    var existing = PolicyProcessing.GetPolicyOptionStates(src, _policy);
                    foreach (var elem in _policy.RawPolicy.Elements ?? new List<PolicyElement>())
                    {
                        if (existing.ContainsKey(elem.ID)) continue; // already had a value
                        if (_elementControls.TryGetValue(elem.ID, out var ctrl))
                        {
                            if (elem.ElementType == "decimal" && ctrl is NumberBox nb)
                            {
                                var def = GetDecimalDefault(elem.ID);
                                nb.Value = ClampDecimal(elem, def);
                            }
                            else if (elem.ElementType == "enum")
                            {
                                // leave enum at presentation default selection already set
                            }
                            else if (elem.ElementType == "boolean" && ctrl is CheckBox ch)
                            {
                                // Do nothing: presentation default already applied during BuildElements
                                _ = ch;
                            }
                            // text/combobox defaults already set in BuildElements
                        }
                    }
                }
                catch { }
            }
        }

        private static void ToggleChildrenEnabled(Panel panel, bool enabled)
        {
            if (panel == null) return;
            foreach (var child in panel.Children)
            {
                if (child is Control ctrl) ctrl.IsEnabled = enabled;
                else if (child is Panel p) ToggleChildrenEnabled(p, enabled);
            }
        }

        private void ViewDetailApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var preview = new PolFile();
                var desired = OptEnabled.IsChecked == true ? PolicyState.Enabled
                              : OptDisabled.IsChecked == true ? PolicyState.Disabled
                              : PolicyState.NotConfigured;
                Dictionary<string, object>? opts = null;
                if (desired == PolicyState.Enabled)
                    opts = CollectOptions();

                PolicyProcessing.SetPolicyState(preview, _policy, desired, opts ?? new Dictionary<string, object>());

                var win = new DetailPolicyFormattedWindow();
                win.Initialize(_policy, _bundle, preview, preview, _currentSection);
                win.Activate();
            }
            catch { }
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            // Validate required text elements when enabling
            if (OptEnabled.IsChecked == true && !ValidateRequiredElements())
            {
                try { if (App.Window is MainWindow mw) mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { "Required value missing.", InfoBarSeverity.Error }); } catch (Exception ex) { Log.Warn("EditSetting", "ShowInfo failed (Apply validation)", ex); }
                return;
            }
            try { SaveToSource(); } catch (Exception ex) { Log.Error("EditSetting", "SaveToSource failed in Apply", ex); }
            try
            {
                if (App.Window is MainWindow mw)
                {
                    mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { "Queued.", InfoBarSeverity.Informational });
                }
            }
            catch (Exception ex) { Log.Warn("EditSetting", "ShowInfo failed (Apply queued)", ex); }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (OptEnabled.IsChecked == true && !ValidateRequiredElements())
            {
                try { if (App.Window is MainWindow mw) mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { "Required value missing.", InfoBarSeverity.Error }); } catch (Exception ex) { Log.Warn("EditSetting", "ShowInfo failed (OK validation)", ex); }
                return;
            }
            try { SaveToSource(); } catch (Exception ex) { Log.Error("EditSetting", "SaveToSource failed in OK", ex); }
            Close();
            try
            {
                if (App.Window is MainWindow mw)
                {
                    mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { "Queued.", InfoBarSeverity.Informational });
                }
            }
            catch (Exception ex) { Log.Warn("EditSetting", "ShowInfo failed (OK queued)", ex); }
        }

        private bool ValidateRequiredElements()
        {
            try
            {
                if (_policy?.RawPolicy?.Elements == null) return true;
                foreach (var elem in _policy.RawPolicy.Elements)
                {
                    if (elem is TextPolicyElement t && t.Required)
                    {
                        if (_elementControls.TryGetValue(elem.ID, out var ctrl) && ctrl is TextBox tb)
                        {
                            if (string.IsNullOrWhiteSpace(tb.Text)) return false;
                        }
                        else if (_elementControls.TryGetValue(elem.ID, out var ctrl2) && ctrl2 is AutoSuggestBox asb)
                        {
                            if (string.IsNullOrWhiteSpace(asb.Text)) return false;
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Warn("EditSetting", "ValidateRequiredElements failed", ex); }
            return true;
        }
    }
}
