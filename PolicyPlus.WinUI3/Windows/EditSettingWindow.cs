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
using System.Text.RegularExpressions;
using WinRT.Interop;
using PolicyPlus.WinUI3.Utils;

namespace PolicyPlus.WinUI3.Windows
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

        private static readonly Regex UrlRegex = new Regex(@"(https?://[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public EditSettingWindow()
        {
            InitializeComponent();
            Title = "Edit Policy Setting";

            ApplyWindowTheme();
            App.ThemeChanged += (s, e) => ApplyWindowTheme();

            WindowHelpers.Resize(this, 800, 600);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);

            SectionSelector.SelectionChanged += SectionSelector_SelectionChanged;
            OptNotConfigured.Checked += StateRadio_Checked;
            OptEnabled.Checked += StateRadio_Checked;
            OptDisabled.Checked += StateRadio_Checked;
            ViewDetailApplyBtn.Click += ViewDetailApplyBtn_Click;
            ApplyBtn.Click += ApplyBtn_Click;
            OkBtn.Click += OkBtn_Click;
            CancelBtn.Click += (s, e) => Close();
        }

        private void ApplyWindowTheme()
        {
            if (Content is FrameworkElement fe)
                fe.RequestedTheme = App.CurrentTheme;
            // Force text boxes to theme-aware colors
            var textBg = Application.Current.Resources["TextControlBackground"] as Brush;
            var textFg = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
            CommentBox.Background = textBg; CommentBox.Foreground = textFg;
            SupportedBox.Background = textBg; SupportedBox.Foreground = textFg;
            // RichTextBlock uses theme brushes automatically; no explicit background needed
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

            SectionSelector.IsEnabled = policy.RawPolicy.Section == AdmxPolicySection.Both;
            SectionSelector.SelectedIndex = (section == AdmxPolicySection.Machine) ? 0 : 1;

            BuildElements();
            LoadStateFromSource();
            StateRadio_Checked(this, null!);

            WindowHelpers.BringToFront(this);
            var tmr = this.DispatcherQueue.CreateTimer();
            tmr.Interval = System.TimeSpan.FromMilliseconds(180);
            tmr.IsRepeating = false;
            tmr.Tick += (s, e) => { try { WindowHelpers.BringToFront(this); this.Activate(); } catch { } }; tmr.Start();
            App.RegisterEditWindow(_policy.UniqueID, this);
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
            if (_policy.RawPolicy.Elements is null || _policy.Presentation is null) return;
            var elemDict = _policy.RawPolicy.Elements.ToDictionary(e => e.ID);
            foreach (var pres in _policy.Presentation.Elements)
            {
                FrameworkElement control = null!;
                string label = string.Empty;
                switch (pres.ElementType)
                {
                    case "text":
                        label = ((LabelPresentationElement)pres).Text;
                        control = new TextBlock { Text = label };
                        label = string.Empty;
                        break;
                    case "decimalTextBox":
                        {
                            var p = (NumericBoxPresentationElement)pres;
                            var e = (DecimalPolicyElement)elemDict[pres.ID];
                            if (p.HasSpinner)
                            {
                                var sp = new NumberBox { Minimum = e.Minimum, Maximum = e.Maximum, Value = p.DefaultValue, SmallChange = p.SpinnerIncrement };
                                control = sp;
                            }
                            else
                            {
                                var tb = new TextBox { Text = p.DefaultValue.ToString() };
                                control = tb;
                            }
                            label = p.Label; break;
                        }
                    case "textBox":
                        {
                            var p = (TextBoxPresentationElement)pres;
                            var e = (TextPolicyElement)elemDict[pres.ID];
                            var tb = new TextBox { Text = p.DefaultValue ?? string.Empty, MaxLength = e.MaxLength };
                            control = tb; label = p.Label; break;
                        }
                    case "checkBox":
                        {
                            var p = (CheckBoxPresentationElement)pres;
                            var cb = new CheckBox { Content = p.Text, IsChecked = p.DefaultState };
                            control = cb; label = string.Empty; break;
                        }
                    case "comboBox":
                        {
                            var p = (ComboBoxPresentationElement)pres;
                            var e = (TextPolicyElement)elemDict[pres.ID];
                            var acb = new AutoSuggestBox { Text = p.DefaultText ?? string.Empty };
                            acb.ItemsSource = p.Suggestions;
                            control = acb; label = p.Label; break;
                        }
                    case "dropdownList":
                        {
                            var p = (DropDownPresentationElement)pres;
                            var e = (EnumPolicyElement)elemDict[pres.ID];
                            var cb = new ComboBox { MinWidth = 160 };
                            int selectedIdx = 0;
                            int curIdx = 0;
                            foreach (var enumItem in e.Items)
                            {
                                var text = _bundle.ResolveString(enumItem.DisplayCode, _policy.RawPolicy.DefinedIn);
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
                            control = cb; label = p.Label; break;
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
                                win.Initialize(p.Label, e.UserProvidesNames, btn.Tag);
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
                            control = btn; label = p.Label; break;
                        }
                    case "multiTextBox":
                        {
                            var p = (MultiTextPresentationElement)pres;
                            var tb = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 120, Text = string.Empty };
                            control = tb; label = p.Label; break;
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
            OptEnabled.IsChecked = state == PolicyState.Enabled;
            OptDisabled.IsChecked = state == PolicyState.Disabled;
            OptNotConfigured.IsChecked = state == PolicyState.NotConfigured || state == PolicyState.Unknown;

            var optionStates = PolicyProcessing.GetPolicyOptionStates(src, _policy);
            foreach (var kv in optionStates)
            {
                if (!_elementControls.TryGetValue(kv.Key, out var ctrl)) continue;
                switch (ctrl)
                {
                    case NumberBox nb when kv.Value is uint u: nb.Value = u; break;
                    case TextBox tb when kv.Value is string s: tb.Text = s; break;
                    case ComboBox cb when kv.Value is int index: cb.SelectedIndex = index; break;
                    case CheckBox ch when kv.Value is bool b: ch.IsChecked = b; break;
                    case TextBox mtb when kv.Value is string[] arr: mtb.Text = string.Join("\r\n", arr); break;
                    case Button btn when kv.Value is Dictionary<string, string> dict: btn.Tag = dict.ToList(); btn.Content = $"Edit... ({dict.Count})"; break;
                    case Button btn2 when kv.Value is List<string> list: btn2.Tag = list; btn2.Content = $"Edit... ({list.Count})"; break;
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
                        if (ctrl is NumberBox nb) options[elem.ID] = (uint)Math.Round(nb.Value);
                        else if (ctrl is TextBox tb && uint.TryParse(tb.Text, out var u)) options[elem.ID] = u; else options[elem.ID] = 0u; break;
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
            PolicyProcessing.ForgetPolicy(src, _policy);
            if (OptEnabled.IsChecked == true)
            {
                var options = CollectOptions();
                PolicyProcessing.SetPolicyState(src, _policy, PolicyState.Enabled, options);
            }
            else if (OptDisabled.IsChecked == true)
            {
                PolicyProcessing.SetPolicyState(src, _policy, PolicyState.Disabled, null);
            }
            if (comments != null)
            {
                var text = CommentBox.Text ?? string.Empty;
                if (string.IsNullOrEmpty(text)) comments.Remove(_policy.UniqueID); else comments[_policy.UniqueID] = text;
            }
            Saved?.Invoke(this, EventArgs.Empty);
        }

        private void SectionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (SectionSelector.SelectedItem as ComboBoxItem)?.Content?.ToString();
            _currentSection = selected == "User" ? AdmxPolicySection.User : AdmxPolicySection.Machine;
            LoadStateFromSource();
            StateRadio_Checked(this, null!);
        }

        private void StateRadio_Checked(object sender, RoutedEventArgs e)
        {
            bool enableOptions = OptEnabled.IsChecked == true;
            foreach (var ctrl in _elementControls.Values.OfType<Control>()) ctrl.IsEnabled = enableOptions;
        }

        private void ViewDetailApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            // Build an in-memory preview without persisting changes
            var previewComp = new PolicyPlus.PolFile();
            var previewUser = new PolicyPlus.PolFile();
            var targetSection = _policy.RawPolicy.Section == AdmxPolicySection.Both ? _currentSection : _policy.RawPolicy.Section;
            var previewTarget = targetSection == AdmxPolicySection.User ? (IPolicySource)previewUser : (IPolicySource)previewComp;

            if (OptEnabled.IsChecked == true)
            {
                var options = CollectOptions();
                PolicyProcessing.SetPolicyState(previewTarget, _policy, PolicyState.Enabled, options);
            }
            else if (OptDisabled.IsChecked == true)
            {
                PolicyProcessing.SetPolicyState(previewTarget, _policy, PolicyState.Disabled, null);
            }
            else
            {
                // Not Configured: leave preview empty so values appear as not set
            }

            var win = new DetailPolicyFormattedWindow();
            _childWindows.Add(win);
            win.Closed += (s, ee) => _childWindows.Remove(win);
            var section = _policy.RawPolicy.Section == AdmxPolicySection.Both ? _currentSection : _policy.RawPolicy.Section;
            // Pass the preview sources so the detail window reads from the in-memory state
            win.Initialize(_policy, _bundle, previewComp, previewUser, section);
            win.Activate();
            WindowHelpers.BringToFront(win);
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e) => SaveToSource();
        private void OkBtn_Click(object sender, RoutedEventArgs e) { SaveToSource(); Close(); }

        public void BringToFront()
        {
            WindowHelpers.BringToFront(this);
        }

        private class ComboItem { public int Id { get; set; } public string Text { get; set; } = string.Empty; public override string ToString() => Text; }
    }
}
