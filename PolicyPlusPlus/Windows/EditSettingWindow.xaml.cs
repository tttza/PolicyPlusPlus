using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusCore.Utilities; // culture preference
using PolicyPlusPlus.Logging; // logging
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.ViewModels; // added for QuickEditRow

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
        public event EventHandler<(
            string Scope,
            PolicyState State,
            Dictionary<string, object>? Options
        )>? SavedDetail; // added
        public event EventHandler<(
            string Scope,
            PolicyState State,
            Dictionary<string, object>? Options
        )>? LiveChanged; // fires on in-place modifications before saving

        private static readonly Regex UrlRegex = new Regex(
            @"(https?://[^\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private bool _useSecondLanguage = false;
        private ToggleButton? _secondLangToggle;

        private PolicyState _lastLoadedState = PolicyState.Unknown; // track last loaded state to know when enabling fresh
        private bool _suppressLiveChange; // suppress LiveChanged during initialization / overlay

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
                        UpdateSecondLangToggle();
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("EditSetting", "SecondLangToggle init failed: " + ex.Message);
                }
            };

            SectionSelector.SelectionChanged += SectionSelector_SelectionChanged;
            OptNotConfigured.Checked += StateRadio_Checked;
            OptEnabled.Checked += StateRadio_Checked;
            OptDisabled.Checked += StateRadio_Checked;
            ViewDetailApplyBtn.Click += ViewDetailApplyBtn_Click;
            ApplyBtn.Click += ApplyBtn_Click;
            OkBtn.Click += OkBtn_Click;
            CancelBtn.Click += (s, e) => Close();

            try
            {
                EventHub.PolicyChangeQueued += OnExternalPolicyChangeQueued;
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Hook PolicyChangeQueued failed: " + ex.Message);
            }
            Closed += (s, e) =>
            {
                try
                {
                    EventHub.PolicyChangeQueued -= OnExternalPolicyChangeQueued;
                }
                catch (Exception ex)
                {
                    Log.Debug("EditSetting", "Unhook PolicyChangeQueued failed: " + ex.Message);
                }
            };
        }

        // Centralized toggle state logic so initial state reflects ADML availability before first click.
        private void UpdateSecondLangToggle()
        {
            try
            {
                if (_secondLangToggle == null)
                    return;
                var st = SettingsService.Instance.LoadSettings();
                bool prefEnabled = st.SecondLanguageEnabled ?? false;
                string lang = st.SecondLanguage ?? "en-US";
                bool hasAdml =
                    prefEnabled
                    && _policy != null
                    && LocalizedTextService.HasAdml(_policy, lang, useFallback: false);

                _secondLangToggle.Visibility = prefEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                _secondLangToggle.IsEnabled = hasAdml;
                ToolTipService.SetToolTip(
                    _secondLangToggle,
                    prefEnabled
                        ? (
                            hasAdml
                                ? $"Toggle 2nd language ({lang})"
                                : $"{lang} language resources not found"
                        )
                        : "2nd language disabled in preferences"
                );
                if (!hasAdml)
                {
                    // Ensure off when resources missing
                    _useSecondLanguage = false;
                    _secondLangToggle.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "UpdateSecondLangToggle failed: " + ex.Message);
            }
        }

        // Overlay current QuickEditRow (unsaved) values into this window (both scopes).
        internal void OverlayFromQuickEdit(QuickEditRow row)
        {
            if (
                row == null
                || _policy == null
                || !string.Equals(
                    row.Policy.UniqueID,
                    _policy.UniqueID,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return;
            try
            {
                _suppressLiveChange = true;
                void ApplyScope(bool isUser)
                {
                    _currentSection = isUser ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                    if (SectionSelector != null)
                        SectionSelector.SelectedIndex = isUser ? 1 : 0;
                    LoadStateFromSource();
                    StateRadio_Checked(this, new RoutedEventArgs());
                    var state = isUser ? row.UserState : row.ComputerState;
                    if (OptEnabled != null)
                        OptEnabled.IsChecked = state == QuickEditState.Enabled;
                    if (OptDisabled != null)
                        OptDisabled.IsChecked = state == QuickEditState.Disabled;
                    if (OptNotConfigured != null)
                        OptNotConfigured.IsChecked = state == QuickEditState.NotConfigured;
                    StateRadio_Checked(this, new RoutedEventArgs());
                    if (state != QuickEditState.Enabled)
                        return;

                    foreach (var opt in row.OptionElements)
                    {
                        if (!_elementControls.TryGetValue(opt.Id, out var ctrl) || ctrl == null)
                            continue;
                        switch (opt.Type)
                        {
                            case OptionElementType.Enum:
                                if (ctrl is ComboBox cb)
                                    cb.SelectedIndex = isUser
                                        ? opt.UserEnumIndex
                                        : opt.ComputerEnumIndex;
                                break;
                            case OptionElementType.Boolean:
                                if (ctrl is CheckBox chk)
                                    chk.IsChecked = isUser ? opt.UserBool : opt.ComputerBool;
                                break;
                            case OptionElementType.Text:
                                string txt = isUser ? opt.UserText : opt.ComputerText;
                                if (ctrl is TextBox tb)
                                    tb.Text = txt;
                                else if (ctrl is AutoSuggestBox asb)
                                    asb.Text = txt;
                                break;
                            case OptionElementType.Decimal:
                                var val = isUser ? opt.UserNumber : opt.ComputerNumber;
                                if (val.HasValue && ctrl is NumberBox nb)
                                    nb.Value = val.Value;
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
                                    var lines = isUser
                                        ? opt.UserMultiTextItems
                                        : opt.ComputerMultiTextItems;
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
                    var preferUser =
                        row.UserState == QuickEditState.Enabled
                        && row.ComputerState != QuickEditState.Enabled;
                    SectionSelector.SelectedIndex = preferUser ? 1 : 0;
                    _currentSection =
                        SectionSelector.SelectedIndex == 1
                            ? AdmxPolicySection.User
                            : AdmxPolicySection.Machine;
                }
                else
                {
                    ApplyScope(_policy.RawPolicy.Section == AdmxPolicySection.User);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Initial section detect failed: " + ex.Message);
            }
            finally
            {
                _suppressLiveChange = false;
                RaiseLiveChanged();
            }
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
                if (_policy == null)
                    return;

                // Always ensure toggle reflects current availability before changing texts
                UpdateSecondLangToggle();

                var s = SettingsService.Instance.LoadSettings();
                string secondLang = s.SecondLanguage ?? "en-US";
                bool secondEnabled = s.SecondLanguageEnabled ?? false;
                // useFallback:false -> disable toggle when the specific second language ADML is not present
                bool hasAdml =
                    secondEnabled
                    && LocalizedTextService.HasAdml(_policy, secondLang, useFallback: false);
                bool useSecond = _useSecondLanguage && hasAdml;

                // Primary text first (in-memory); will attempt cache fallback only if missing.
                string primaryTitle = _policy.DisplayName ?? string.Empty;
                string primaryExplain = _policy.DisplayExplanation ?? string.Empty;
                string primarySupported = _policy.SupportedOn?.DisplayName ?? string.Empty;

                if (useSecond)
                {
                    // Second language direct from loaded bundle if available.
                    SettingTitle.Text = LocalizedTextService.GetPolicyNameIn(_policy, secondLang);
                    SetExplanationText(
                        LocalizedTextService.GetPolicyExplanationIn(_policy, secondLang)
                    );
                    var sup = LocalizedTextService.GetSupportedDisplayIn(_policy, secondLang);
                    SupportedBox.Text = string.IsNullOrEmpty(sup) ? primarySupported : sup;
                }
                else
                {
                    SettingTitle.Text = primaryTitle;
                    SetExplanationText(primaryExplain);
                    SupportedBox.Text = primarySupported;
                }

                // If primary language explanation is empty and fallback to en-US is enabled in settings, attempt cache multi-culture retrieval.
                // This covers scenario where ADML not present at scan time (not persisted) but later available cultures exist.
                if (!useSecond && string.IsNullOrWhiteSpace(primaryExplain))
                {
                    try
                    {
                        var cultures = BuildOrderedCulturesForDetailFallback();
                        if (cultures.Count > 1)
                        {
                            _ = TryFillFromCacheAsync(cultures); // fire and forget; UI updates when done
                        }
                    }
                    catch (Exception exFallback)
                    {
                        Log.Debug(
                            "EditSetting",
                            "detail cache fallback start failed: " + exFallback.Message
                        );
                    }
                }

                if (_policy.RawPolicy.Elements != null)
                {
                    var pres = useSecond
                        ? LocalizedTextService.GetPresentationIn(_policy, secondLang)
                        : null;
                    if (pres != null)
                    {
                        var original = _policy.Presentation;
                        _policy.Presentation = pres;
                        BuildElements();
                        LoadStateFromSource();
                        StateRadio_Checked(this, null!);
                        _policy.Presentation = original;
                    }
                    else
                    {
                        BuildElements();
                        LoadStateFromSource();
                        StateRadio_Checked(this, null!);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(
                    "EditSetting",
                    "LoadStateFromSource failed (option retrieval): " + ex.Message
                );
            }
        }

        private List<string> BuildOrderedCulturesForDetailFallback()
        {
            var st = SettingsService.Instance.LoadSettings();
            var slots = CulturePreference.Build(
                new CulturePreference.BuildOptions(
                    Primary: string.IsNullOrWhiteSpace(st.Language)
                        ? CultureInfo.CurrentUICulture.Name
                        : st.Language!,
                    Second: st.SecondLanguage,
                    SecondEnabled: st.SecondLanguageEnabled ?? false,
                    OsUiCulture: CultureInfo.CurrentUICulture.Name,
                    EnablePrimaryFallback: st.PrimaryLanguageFallbackEnabled ?? false
                )
            );
            return CulturePreference.FlattenNames(slots);
        }

        private async Task TryFillFromCacheAsync(IReadOnlyList<string> cultures)
        {
            try
            {
                var cache = AdmxCacheHostService.Instance.Cache;
                // unique id is namespace:policyName inside _policy.UniqueID; split
                var uid = _policy.UniqueID;
                int colon = uid.IndexOf(':');
                if (colon <= 0 || colon >= uid.Length - 1)
                    return;
                var ns = uid.Substring(0, colon);
                var name = uid.Substring(colon + 1);
                var detail = await cache
                    .GetByPolicyNameAsync(ns, name, cultures)
                    .ConfigureAwait(false);
                if (detail == null)
                    return;
                // Only update fields if UI still shows empty primary explanation (avoid overwriting user toggled view)
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(_policy.DisplayExplanation))
                        {
                            if (string.IsNullOrWhiteSpace(SettingTitle.Text))
                                SettingTitle.Text = detail.DisplayName;
                            bool explanationEmpty = ExplainText.Blocks.Count == 0;
                            if (explanationEmpty)
                                SetExplanationText(detail.ExplainText ?? string.Empty);
                            if (string.IsNullOrWhiteSpace(SupportedBox.Text))
                                SupportedBox.Text = detail.ProductHint ?? string.Empty; // fallback minimal
                        }
                    }
                    catch (Exception exUi)
                    {
                        Log.Debug("EditSetting", "cache detail UI apply failed: " + exUi.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "cache detail retrieval failed: " + ex.Message);
            }
        }

        private void Accel_Apply(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                ApplyBtn_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "GetTextDefault enumeration failed: " + ex.Message);
            }
            args.Handled = true;
        }

        private void Accel_Ok(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            try
            {
                OkBtn_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "GetDecimalDefault enumeration failed: " + ex.Message);
            }
            args.Handled = true;
        }

        private void Accel_Preview(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                ViewDetailApplyBtn_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "ClampDecimal bounds logic failed: " + ex.Message);
            }
            args.Handled = true;
        }

        private void Accel_SectionComp(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                if (SectionSelector != null)
                    SectionSelector.SelectedIndex = 0;
            }
            catch { }
            args.Handled = true;
        }

        private void Accel_SectionUser(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                if (SectionSelector != null)
                    SectionSelector.SelectedIndex = 1;
            }
            catch { }
            args.Handled = true;
        }

        private void Accel_Close(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                Close();
            }
            catch { }
            args.Handled = true;
        }

        private void ApplyWindowTheme()
        {
            try
            {
                if (Content is FrameworkElement fe)
                    fe.RequestedTheme = App.CurrentTheme;
                var textBg = Application.Current.Resources["TextControlBackground"] as Brush;
                var textFg = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
                CommentBox.Background = textBg;
                CommentBox.Foreground = textFg;
                SupportedBox.Background = textBg;
                SupportedBox.Foreground = textFg;
            }
            catch { }
        }

        public void Initialize(
            PolicyPlusPolicy policy,
            AdmxPolicySection section,
            AdmxBundle bundle,
            IPolicySource compSource,
            IPolicySource userSource,
            PolicyLoader compLoader,
            PolicyLoader userLoader,
            Dictionary<string, string>? compComments,
            Dictionary<string, string>? userComments
        )
        {
            _policy = policy;
            _currentSection = section;
            _bundle = bundle;
            _compSource = compSource;
            _userSource = userSource;
            _compLoader = compLoader;
            _userLoader = userLoader;
            _compComments = compComments;
            _userComments = userComments;

            SettingTitle.Text = policy.DisplayName;
            SupportedBox.Text = policy.SupportedOn is null
                ? string.Empty
                : policy.SupportedOn.DisplayName;
            SetExplanationText(policy.DisplayExplanation ?? string.Empty);

            try
            {
                UpdateSecondLangToggle();
            }
            catch { }

            var initialSection = section;
            try
            {
                if (policy.RawPolicy.Section == AdmxPolicySection.Both)
                {
                    // Prefer pending scope (Computer over User) before inspecting committed source states.
                    try
                    {
                        var pend = PendingChangesService
                            .Instance.Pending.Where(p =>
                                string.Equals(
                                    p.PolicyId,
                                    policy.UniqueID,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .ToList();
                        bool hasCompPending = pend.Any(p =>
                            string.Equals(p.Scope, "Computer", StringComparison.OrdinalIgnoreCase)
                        );
                        bool hasUserPending = pend.Any(p =>
                            string.Equals(p.Scope, "User", StringComparison.OrdinalIgnoreCase)
                        );
                        if (hasCompPending)
                        {
                            initialSection = AdmxPolicySection.Machine;
                        }
                        else if (hasUserPending)
                        {
                            initialSection = AdmxPolicySection.User;
                        }
                        else
                        {
                            // Fall through to committed state detection below
                        }
                    }
                    catch { }

                    // Only inspect committed state when no pending scope decided yet.
                    if (initialSection == section) // unchanged so far
                    {
                        var compState = PolicyProcessing.GetPolicyState(_compSource, _policy);
                        bool compConfigured =
                            compState == PolicyState.Enabled || compState == PolicyState.Disabled;
                        if (compConfigured)
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
            RaiseLiveChanged();

            WindowHelpers.BringToFront(this);
            var tmr = this.DispatcherQueue.CreateTimer();
            tmr.Interval = TimeSpan.FromMilliseconds(180);
            tmr.IsRepeating = false;
            tmr.Tick += (s, e) =>
            {
                try
                {
                    WindowHelpers.BringToFront(this);
                    Activate();
                }
                catch (Exception ex)
                {
                    Log.Debug("EditSetting", "Activate timer bring-to-front failed: " + ex.Message);
                }
            };
            tmr.Start();
            App.RegisterEditWindow(_policy.UniqueID, this);
        }

        private void ApplyPendingOverlayFromQueue()
        {
            try
            {
                var scope = (_currentSection == AdmxPolicySection.User) ? "User" : "Computer";
                var change = PendingChangesService.Instance.Pending.FirstOrDefault(p =>
                    string.Equals(p.PolicyId, _policy.UniqueID, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Scope, scope, StringComparison.OrdinalIgnoreCase)
                );
                if (change == null)
                    return;

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

                if (change.Options != null)
                    ApplyOptionsToControls(change.Options);
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "ApplyPendingOverlayFromQueue failed: " + ex.Message);
            }
        }

        private void ApplyOptionsToControls(Dictionary<string, object> options)
        {
            foreach (var kv in options)
            {
                if (!_elementControls.TryGetValue(kv.Key, out var ctrl))
                    continue;
                var val = kv.Value;
                switch (ctrl)
                {
                    case NumberBox nb:
                        if (val is uint u)
                            nb.Value = u;
                        else if (val is int i)
                            nb.Value = i;
                        else if (val is string s && double.TryParse(s, out var d))
                            nb.Value = d;
                        break;
                    case TextBox tb:
                        if (val is string s1)
                            tb.Text = s1;
                        else if (val is string[] arr)
                            tb.Text = string.Join("\r\n", arr);
                        break;
                    case AutoSuggestBox asb:
                        asb.Text = Convert.ToString(val) ?? string.Empty;
                        break;
                    case ComboBox cb:
                        if (val is int idx && idx >= -1 && idx < cb.Items.Count)
                            cb.SelectedIndex = idx;
                        break;
                    case CheckBox ch:
                        if (val is bool b)
                            ch.IsChecked = b;
                        else
                            ch.IsChecked = (
                                Convert
                                    .ToString(val)
                                    ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
                            );
                        break;
                    case Button btn:
                        if (val is List<string> list)
                        {
                            btn.Tag = list;
                            btn.Content = $"Edit... ({list.Count})";
                        }
                        else if (val is IEnumerable<KeyValuePair<string, string>> kvpList)
                        {
                            var l = kvpList.ToList();
                            btn.Tag = l;
                            btn.Content = $"Edit... ({l.Count})";
                        }
                        break;
                }
            }
        }

        private void SetExplanationText(string text)
        {
            ExplainText.Blocks.Clear();
            var para = new Paragraph();
            if (string.IsNullOrEmpty(text))
            {
                ExplainText.Blocks.Add(para);
                return;
            }

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
                        try
                        {
                            await global::Windows.System.Launcher.LaunchUriAsync(uri);
                        }
                        catch (Exception ex2)
                        {
                            Log.Debug("EditSetting", "LaunchUriAsync failed: " + ex2.Message);
                        }
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

        private (
            IPolicySource src,
            PolicyLoader loader,
            Dictionary<string, string>? comments
        ) GetCurrent()
        {
            bool user = _currentSection == AdmxPolicySection.User;
            return (
                user ? _userSource : _compSource,
                user ? _userLoader : _compLoader,
                user ? _userComments : _compComments
            );
        }

        private void BuildElements()
        {
            _suppressLiveChange = true;
            _elementControls.Clear();
            OptionsPanel.Children.Clear();
            if (_policy.RawPolicy.Elements is null || _policy.Presentation is null)
            {
                _suppressLiveChange = false;
                return;
            }

            var settings = SettingsService.Instance.LoadSettings();
            bool useSecond = _useSecondLanguage && (settings.SecondLanguageEnabled ?? false);
            string lang = useSecond
                ? (settings.SecondLanguage ?? "en-US")
                : (settings.Language ?? System.Globalization.CultureInfo.CurrentUICulture.Name);

            Presentation presentationToUse = _policy.Presentation;
            Presentation? originalPresentation = _policy.Presentation;
            try
            {
                if (useSecond)
                {
                    var lp = LocalizedTextService.GetPresentationIn(_policy, lang);
                    if (lp != null)
                    {
                        try
                        {
                            foreach (var locElem in lp.Elements)
                            {
                                if (
                                    locElem.ElementType == "decimalTextBox"
                                    && locElem is NumericBoxPresentationElement nbLoc
                                    && nbLoc.DefaultValue == 0
                                    && originalPresentation != null
                                )
                                {
                                    var baseElem =
                                        originalPresentation.Elements.FirstOrDefault(e =>
                                            e.ElementType == "decimalTextBox"
                                            && string.Equals(
                                                e.ID,
                                                locElem.ID,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        ) as NumericBoxPresentationElement;
                                    if (baseElem != null && baseElem.DefaultValue > 0)
                                    {
                                        nbLoc.DefaultValue = baseElem.DefaultValue;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(
                                "EditSetting",
                                "ListEditorWindow bring-to-front retry failed: " + ex.Message
                            );
                        }
                        presentationToUse = lp;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "RefreshLocalizedTexts failed: " + ex.Message);
            }

            string ResolvePresString(PresentationElement pres, string? fallback)
            {
                if (!string.IsNullOrWhiteSpace(fallback))
                    return fallback!;
                if (pres == null || string.IsNullOrEmpty(pres.ID))
                    return fallback ?? string.Empty;
                var viaString = LocalizedTextService.ResolveString(
                    "$(string." + pres.ID + ")",
                    _policy.RawPolicy.DefinedIn,
                    lang
                );
                if (!string.IsNullOrEmpty(viaString))
                    return viaString;
                return fallback ?? string.Empty;
            }

            // Build a case-insensitive map of element definitions for robust lookup from presentation.
            var elemDict = _policy.RawPolicy.Elements.ToDictionary(
                e => e.ID,
                StringComparer.OrdinalIgnoreCase
            );
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
                        DecimalPolicyElement? e = null;
                        if (
                            !elemDict.TryGetValue(pres.ID, out var elem)
                            || elem is not DecimalPolicyElement de
                        )
                        {
                            // Element mapping missing; degrade gracefully with wide defaults.
                            var nbFallback = new NumberBox
                            {
                                Minimum = 0,
                                Maximum = uint.MaxValue,
                                Value = p.DefaultValue,
                                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                                IsEnabled = true,
                            };
                            if (p.HasSpinner)
                                nbFallback.SmallChange = p.SpinnerIncrement;
                            else
                                nbFallback.SmallChange = 1;
                            control = nbFallback;
                            label = ResolvePresString(pres, p.Label);
                            nbFallback.ValueChanged += (_, __) => RaiseLiveChanged();
                            break;
                        }
                        e = de;
                        var nb = new NumberBox
                        {
                            Minimum = e.Minimum,
                            Maximum = e.Maximum,
                            Value = p.DefaultValue,
                            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                        };
                        if (p.HasSpinner)
                            nb.SmallChange = p.SpinnerIncrement;
                        else
                            nb.SmallChange = 1; // sensible default
                        control = nb;
                        label = ResolvePresString(pres, p.Label);
                        nb.ValueChanged += (_, __) => RaiseLiveChanged();
                        break;
                    }
                    case "textBox":
                    {
                        var p = (TextBoxPresentationElement)pres;
                        TextPolicyElement? e = null;
                        if (
                            !elemDict.TryGetValue(pres.ID, out var elem)
                            || elem is not TextPolicyElement te
                        )
                        {
                            // Element missing: no explicit max length available; use control defaults (no limit).
                            var tbFallback = new TextBox { Text = p.DefaultValue ?? string.Empty };
                            control = tbFallback;
                            label = ResolvePresString(pres, p.Label);
                            tbFallback.TextChanged += (_, __) => RaiseLiveChanged();
                            break;
                        }
                        e = te;
                        var tb = new TextBox
                        {
                            Text = p.DefaultValue ?? string.Empty,
                            MaxLength = e.MaxLength,
                        };
                        control = tb;
                        label = ResolvePresString(pres, p.Label);
                        tb.TextChanged += (_, __) => RaiseLiveChanged();
                        break;
                    }
                    case "checkBox":
                    {
                        var p = (CheckBoxPresentationElement)pres;
                        var text = ResolvePresString(pres, p.Text);
                        var cb = new CheckBox { Content = text, IsChecked = p.DefaultState };
                        control = cb;
                        label = string.Empty;
                        cb.Checked += (_, __) => RaiseLiveChanged();
                        cb.Unchecked += (_, __) => RaiseLiveChanged();
                        break;
                    }
                    case "comboBox":
                    {
                        var p = (ComboBoxPresentationElement)pres;
                        // Element definition is not required for rendering suggestions; proceed even if missing.
                        var acb = new AutoSuggestBox { Text = p.DefaultText ?? string.Empty };
                        var list = new List<string>();
                        foreach (var s in p.Suggestions)
                        {
                            var resolved = LocalizedTextService.ResolveString(
                                s,
                                _policy.RawPolicy.DefinedIn,
                                lang
                            );
                            list.Add(string.IsNullOrEmpty(resolved) ? s : resolved);
                        }
                        acb.ItemsSource = list;
                        control = acb;
                        label = ResolvePresString(pres, p.Label);
                        acb.TextChanged += (_, __) => RaiseLiveChanged();
                        break;
                    }
                    case "dropdownList":
                    {
                        var p = (DropDownPresentationElement)pres;
                        if (
                            !elemDict.TryGetValue(pres.ID, out var elem)
                            || elem is not EnumPolicyElement e
                        )
                        {
                            // Without element, we cannot enumerate items. Show disabled empty dropdown.
                            label = ResolvePresString(pres, p.Label);
                            var cbEmpty = new ComboBox { MinWidth = 160, IsEnabled = false };
                            control = cbEmpty;
                            break;
                        }
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
                                    ? LocalizedTextService.ResolveString(
                                        enumItem.DisplayCode,
                                        _policy.RawPolicy.DefinedIn,
                                        lang
                                    )
                                    : _bundle.ResolveString(
                                        enumItem.DisplayCode,
                                        _policy.RawPolicy.DefinedIn
                                    );
                            }
                            catch
                            {
                                text = _bundle.ResolveString(
                                    enumItem.DisplayCode,
                                    _policy.RawPolicy.DefinedIn
                                );
                            }
                            var cbi = new ComboBoxItem
                            {
                                Content = string.IsNullOrWhiteSpace(text)
                                    ? enumItem.DisplayCode
                                    : text,
                            };
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
                        cb.SelectionChanged += (_, __) => RaiseLiveChanged();
                        control = cb;
                        break;
                    }
                    case "listBox":
                    {
                        var p = (ListPresentationElement)pres;
                        if (
                            !elemDict.TryGetValue(pres.ID, out var elem)
                            || elem is not ListPolicyElement e
                        )
                        {
                            // Cannot edit without element schema; show disabled button with label.
                            var disabledBtn = new Button { Content = "Edit...", IsEnabled = false };
                            control = disabledBtn;
                            label = ResolvePresString(pres, p.Label);
                            break;
                        }
                        var btn = new Button { Content = "Edit..." };
                        btn.Click += (s, e2) =>
                        {
                            string key = _policy.UniqueID + ":" + pres.ID;
                            if (ListEditorWindow.TryActivateExisting(key))
                                return;
                            var win = new ListEditorWindow();
                            ListEditorWindow.Register(key, win);
                            _childEditors.Add(win);
                            _childWindows.Add(win);
                            win.Closed += (ss, ee) =>
                            {
                                _childEditors.Remove(win);
                                _childWindows.Remove(win);
                            };
                            win.Initialize(
                                ResolvePresString(pres, p.Label),
                                e.UserProvidesNames,
                                btn.Tag
                            );
                            win.Finished += (ss, ok) =>
                            {
                                if (!ok)
                                    return;
                                btn.Tag = win.Result;
                                if (win.CountText != null)
                                    btn.Content = win.CountText;
                                RaiseLiveChanged();
                            };
                            win.Activate();
                            WindowHelpers.BringToFront(win);
                            var t = win.DispatcherQueue.CreateTimer();
                            t.Interval = System.TimeSpan.FromMilliseconds(180);
                            t.IsRepeating = false;
                            t.Tick += (sss, eee) =>
                            {
                                try
                                {
                                    WindowHelpers.BringToFront(win);
                                    win.Activate();
                                }
                                catch (Exception ex)
                                {
                                    Log.Debug(
                                        "EditSetting",
                                        "ListEditorWindow nested bring-to-front failed: "
                                            + ex.Message
                                    );
                                }
                            };
                            t.Start();
                        };
                        control = btn;
                        label = ResolvePresString(pres, p.Label);
                        break;
                    }
                    case "multiTextBox":
                    {
                        var p = (MultiTextPresentationElement)pres;
                        var tb = new TextBox
                        {
                            AcceptsReturn = true,
                            TextWrapping = TextWrapping.NoWrap,
                            Height = 120,
                            Text = string.Empty,
                        };
                        control = tb;
                        label = ResolvePresString(pres, p.Label);
                        tb.TextChanged += (_, __) => RaiseLiveChanged();
                        break;
                    }
                }
                if (control != null)
                {
                    if (!string.IsNullOrEmpty(label))
                    {
                        var lbl = new TextBlock
                        {
                            Text = label,
                            FontWeight = FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                        };
                        OptionsPanel.Children.Add(lbl);
                    }
                    OptionsPanel.Children.Add(control);
                    _elementControls[pres.ID] = control;
                }
            }
            _suppressLiveChange = false;
        }

        private void LoadStateFromSource()
        {
            var (src, loader, comments) = GetCurrent();
            var state = PolicyProcessing.GetPolicyState(src, _policy);
            _lastLoadedState = state;
            OptEnabled.IsChecked = state == PolicyState.Enabled;
            OptDisabled.IsChecked = state == PolicyState.Disabled;
            OptNotConfigured.IsChecked =
                state == PolicyState.NotConfigured || state == PolicyState.Unknown;

            Dictionary<string, object> optionStates = new();
            if (state == PolicyState.Enabled)
            {
                try
                {
                    optionStates = PolicyProcessing.GetPolicyOptionStates(src, _policy);
                }
                catch
                {
                    optionStates = new();
                }
            }

            foreach (var elem in _policy.RawPolicy.Elements ?? new List<PolicyElement>())
            {
                if (!_elementControls.TryGetValue(elem.ID, out var ctrl))
                    continue;

                if (state == PolicyState.Enabled && optionStates.TryGetValue(elem.ID, out var val))
                {
                    switch (ctrl)
                    {
                        case NumberBox nb when val is uint u:
                            nb.Value = u;
                            continue;
                        case TextBox tb when val is string s:
                            tb.Text = s;
                            continue;
                        case ComboBox cb when val is int index:
                            cb.SelectedIndex = index;
                            continue;
                        case CheckBox ch when val is bool b:
                            ch.IsChecked = b;
                            continue;
                        case TextBox mtb when val is string[] arr:
                            mtb.Text = string.Join("\r\n", arr);
                            continue;
                        case Button btn when val is Dictionary<string, string> dict:
                            btn.Tag = dict.ToList();
                            btn.Content = $"Edit... ({dict.Count})";
                            continue;
                        case Button btn2 when val is List<string> list:
                            btn2.Tag = list;
                            btn2.Content = $"Edit... ({list.Count})";
                            continue;
                    }
                }

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
                        if (defText != null)
                            tb2.Text = defText;
                    }
                }
                else if (elem is BooleanPolicyElement && ctrl is CheckBox ch2)
                {
                    _ = ch2;
                }
            }

            if (comments == null)
            {
                CommentBox.IsEnabled = false;
                CommentBox.Text = "Comments unavailable for this policy source";
            }
            else
            {
                CommentBox.IsEnabled = true;
                CommentBox.Text = comments.TryGetValue(_policy.UniqueID, out var c)
                    ? c
                    : string.Empty;
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
                        if (
                            pe.ElementType == "textBox"
                            && string.Equals(pe.ID, id, StringComparison.OrdinalIgnoreCase)
                            && pe is TextBoxPresentationElement tpe
                        )
                            return tpe.DefaultValue;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Accel_Apply failed: " + ex.Message);
            }
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
                        if (
                            pe.ElementType == "decimalTextBox"
                            && string.Equals(pe.ID, id, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            if (pe is NumericBoxPresentationElement nbpe)
                                return nbpe.DefaultValue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Accel_Ok failed: " + ex.Message);
            }
            return 0;
        }

        private uint ClampDecimal(PolicyElement elem, double value)
        {
            try
            {
                if (elem is DecimalPolicyElement de)
                {
                    if (value < de.Minimum)
                        value = de.Minimum;
                    if (value > de.Maximum)
                        value = de.Maximum;
                    return (uint)Math.Round(value);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Accel_Preview failed: " + ex.Message);
            }
            return (uint)Math.Max(0, Math.Round(value));
        }

        private Dictionary<string, object> CollectOptions()
        {
            var options = new Dictionary<string, object>();
            if (_policy.RawPolicy.Elements is null)
                return options;
            foreach (var elem in _policy.RawPolicy.Elements)
            {
                if (!_elementControls.TryGetValue(elem.ID, out var ctrl))
                    continue;
                switch (elem.ElementType)
                {
                    case "decimal":
                        if (ctrl is NumberBox nb)
                        {
                            options[elem.ID] = ClampDecimal(elem, nb.Value);
                        }
                        else if (ctrl is TextBox tbx && uint.TryParse(tbx.Text, out var u))
                            options[elem.ID] = ClampDecimal(elem, u);
                        else
                            options[elem.ID] = 0u;
                        break;
                    case "text":
                        if (ctrl is AutoSuggestBox asb)
                            options[elem.ID] = asb.Text;
                        else if (ctrl is TextBox tbx)
                            options[elem.ID] = tbx.Text;
                        break;
                    case "boolean":
                        options[elem.ID] = (ctrl as CheckBox)?.IsChecked == true;
                        break;
                    case "enum":
                        options[elem.ID] = (ctrl as ComboBox)?.SelectedIndex ?? -1;
                        break;
                    case "list":
                        var tag = (ctrl as Button)?.Tag;
                        if (tag is List<string> l)
                            options[elem.ID] = l;
                        else if (tag is List<KeyValuePair<string, string>> kvpList)
                            options[elem.ID] = kvpList;
                        break;
                    case "multiText":
                        var text = (ctrl as TextBox)?.Text ?? string.Empty;
                        options[elem.ID] = text.Split(
                            new[] { "\r\n", "\n" },
                            StringSplitOptions.None
                        );
                        break;
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
                if (string.IsNullOrEmpty(text))
                    comments.Remove(_policy.UniqueID);
                else
                    comments[_policy.UniqueID] = text;
            }

            try
            {
                var scope = (_currentSection == AdmxPolicySection.User) ? "User" : "Computer";
                PendingChangesService.Instance.Add(
                    new PendingChange
                    {
                        PolicyId = _policy.UniqueID,
                        PolicyName = _policy.DisplayName ?? _policy.UniqueID,
                        Scope = scope,
                        Action = action,
                        Details = details,
                        DetailsFull = detailsFull,
                        DesiredState = desired,
                        Options = options,
                    }
                );
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Accel_SectionComp failed: " + ex.Message);
            }

            try
            {
                Saved?.Invoke(this, EventArgs.Empty);
                try
                {
                    var scope = (_currentSection == AdmxPolicySection.User) ? "User" : "Computer";
                    SavedDetail?.Invoke(this, (scope, desired, options));
                    try
                    {
                        EventHub.PublishPolicyChangeQueued(
                            _policy.UniqueID,
                            scope,
                            desired,
                            options
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(
                            "EditSetting",
                            "SaveToSource SavedDetail publish failed: " + ex.Message
                        );
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "EditSetting",
                        "SaveToSource SavedDetail outer failed: " + ex.Message
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Accel_SectionUser failed: " + ex.Message);
            }
        }

        private (string shortText, string longText) BuildDetailsForPending(
            Dictionary<string, object> options
        )
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
                    if (options.Count > 4)
                        shortSb.Append($" (+{options.Count - 4} more)");
                }

                var longSb = new StringBuilder();
                longSb.AppendLine("Registry values:");
                foreach (var kv in PolicyProcessing.GetReferencedRegistryValues(_policy))
                {
                    longSb.AppendLine(
                        "  ? "
                            + kv.Key
                            + (string.IsNullOrEmpty(kv.Value) ? string.Empty : $" ({kv.Value})")
                    );
                }
                if (options != null && options.Count > 0)
                {
                    longSb.AppendLine("Options:");
                    foreach (var kv in options)
                        longSb.AppendLine("  - " + kv.Key + " = " + FormatOpt(kv.Value));
                }
                return (shortSb.ToString(), longSb.ToString());
            }
            catch
            {
                return (string.Empty, string.Empty);
            }
        }

        private static string FormatOpt(object v)
        {
            if (v == null)
                return string.Empty;
            if (v is string s)
                return s;
            if (v is bool b)
                return b ? "true" : "false";
            if (v is IEnumerable<string> strList)
                return string.Join(", ", strList);
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
            _currentSection =
                SectionSelector.SelectedIndex == 1
                    ? AdmxPolicySection.User
                    : AdmxPolicySection.Machine;
            _suppressLiveChange = true;
            try
            {
                LoadStateFromSource();
                // Overlay pending (unsaved) changes for the newly selected scope
                ApplyPendingOverlayFromQueue();
            }
            finally
            {
                _suppressLiveChange = false;
            }
            RaiseLiveChanged();
        }

        private void StateRadio_Checked(object sender, RoutedEventArgs e)
        {
            bool enableOptions = OptEnabled.IsChecked == true;
            ToggleChildrenEnabled(OptionsPanel, enableOptions);

            if (enableOptions && _lastLoadedState != PolicyState.Enabled)
            {
                try
                {
                    var (src, _, _) = GetCurrent();
                    var existing = PolicyProcessing.GetPolicyOptionStates(src, _policy);
                    foreach (var elem in _policy.RawPolicy.Elements ?? new List<PolicyElement>())
                    {
                        if (existing.ContainsKey(elem.ID))
                            continue;
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
                catch (Exception ex)
                {
                    Log.Debug(
                        "EditSetting",
                        "StateRadio_Checked init missing-element defaults failed: " + ex.Message
                    );
                }
            }
            RaiseLiveChanged();
        }

        private static void ToggleChildrenEnabled(Panel panel, bool enabled)
        {
            if (panel == null)
                return;
            foreach (var child in panel.Children)
            {
                if (child is Control ctrl)
                    ctrl.IsEnabled = enabled;
                else if (child is Panel p)
                    ToggleChildrenEnabled(p, enabled);
            }
        }

        private void ViewDetailApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var preview = new PolFile();
                var desired =
                    OptEnabled.IsChecked == true ? PolicyState.Enabled
                    : OptDisabled.IsChecked == true ? PolicyState.Disabled
                    : PolicyState.NotConfigured;
                Dictionary<string, object>? opts = null;
                if (desired == PolicyState.Enabled)
                    opts = CollectOptions();

                PolicyProcessing.SetPolicyState(
                    preview,
                    _policy,
                    desired,
                    opts ?? new Dictionary<string, object>()
                );

                var win = new DetailPolicyFormattedWindow();
                win.Initialize(_policy, _bundle, preview, preview, _currentSection);
                win.Activate();
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "Accel_Close failed: " + ex.Message);
            }
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (OptEnabled.IsChecked == true && !ValidateRequiredElements())
            {
                if (App.Window is MainWindow mw)
                {
                    // Show missing required value notice at error severity (no auto close).
                    mw.ShowInfo("Required value missing.", InfoBarSeverity.Error);
                }
                return;
            }
            try
            {
                SaveToSource();
            }
            catch (Exception ex)
            {
                Log.Error("EditSetting", "SaveToSource failed in Apply", ex);
            }
            if (App.Window is MainWindow mwQueuedApply)
            {
                mwQueuedApply.ShowInfo("Queued.", InfoBarSeverity.Informational);
            }
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (OptEnabled.IsChecked == true && !ValidateRequiredElements())
            {
                if (App.Window is MainWindow mw)
                {
                    mw.ShowInfo("Required value missing.", InfoBarSeverity.Error);
                }
                return;
            }
            try
            {
                SaveToSource();
            }
            catch (Exception ex)
            {
                Log.Error("EditSetting", "SaveToSource failed in OK", ex);
            }
            Close();
            if (App.Window is MainWindow mwQueuedOk)
            {
                mwQueuedOk.ShowInfo("Queued.", InfoBarSeverity.Informational);
            }
        }

        private bool ValidateRequiredElements()
        {
            try
            {
                if (_policy?.RawPolicy?.Elements == null)
                    return true;
                foreach (var elem in _policy.RawPolicy.Elements)
                {
                    if (elem is TextPolicyElement t && t.Required)
                    {
                        if (
                            _elementControls.TryGetValue(elem.ID, out var ctrl)
                            && ctrl is TextBox tb
                        )
                        {
                            if (string.IsNullOrWhiteSpace(tb.Text))
                                return false;
                        }
                        else if (
                            _elementControls.TryGetValue(elem.ID, out var ctrl2)
                            && ctrl2 is AutoSuggestBox asb
                        )
                        {
                            if (string.IsNullOrWhiteSpace(asb.Text))
                                return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("EditSetting", "ValidateRequiredElements failed", ex);
            }
            return true;
        }

        private void RaiseLiveChanged()
        {
            if (_suppressLiveChange)
                return;
            try
            {
                var scope = (_currentSection == AdmxPolicySection.User) ? "User" : "Computer";
                PolicyState state = PolicyState.NotConfigured;
                if (OptEnabled.IsChecked == true)
                    state = PolicyState.Enabled;
                else if (OptDisabled.IsChecked == true)
                    state = PolicyState.Disabled;
                Dictionary<string, object>? options = null;
                if (state == PolicyState.Enabled)
                    options = CollectOptions();
                LiveChanged?.Invoke(this, (scope, state, options));
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "ApplyWindowTheme failed: " + ex.Message);
            }
        }

        private void OnExternalPolicyChangeQueued(
            string policyId,
            string scope,
            PolicyState state,
            Dictionary<string, object>? options
        )
        {
            try
            {
                if (_policy == null)
                    return;
                if (!string.Equals(policyId, _policy.UniqueID, StringComparison.OrdinalIgnoreCase))
                    return;
                bool isUser = string.Equals(scope, "User", StringComparison.OrdinalIgnoreCase);
                bool isComputer = string.Equals(
                    scope,
                    "Computer",
                    StringComparison.OrdinalIgnoreCase
                );
                if (!isUser && !isComputer)
                    return;
                var targetSection = isUser ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                _suppressLiveChange = true;
                try
                {
                    if (_currentSection == targetSection)
                    {
                        if (state == PolicyState.Enabled)
                            OptEnabled.IsChecked = true;
                        else if (state == PolicyState.Disabled)
                            OptDisabled.IsChecked = true;
                        else
                            OptNotConfigured.IsChecked = true;
                        StateRadio_Checked(this, new RoutedEventArgs());
                        if (state == PolicyState.Enabled && options != null)
                            ApplyOptionsToControls(options);
                    }
                }
                finally
                {
                    _suppressLiveChange = false;
                    RaiseLiveChanged();
                }
            }
            catch (Exception ex)
            {
                Log.Debug("EditSetting", "RaiseLiveChanged external publish failed: " + ex.Message);
            }
        }
    }
}
