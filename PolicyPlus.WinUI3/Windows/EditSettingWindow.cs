using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics;
using WinRT.Interop;
using PolicyPlus.WinUI3.Utils;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed class EditSettingWindow : Window
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

        // UI controls
        private TextBlock SettingTitle = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold };
        private ComboBox SectionSelector = new ComboBox { Width = 220 };
        private RadioButton OptNotConfigured = new RadioButton { Content = "Not Configured", GroupName = "State" };
        private RadioButton OptEnabled = new RadioButton { Content = "Enable", GroupName = "State" };
        private RadioButton OptDisabled = new RadioButton { Content = "Disable", GroupName = "State" };
        private TextBox CommentBox = new TextBox();
        private TextBox SupportedBox = new TextBox { IsReadOnly = true };
        private StackPanel OptionsPanel = new StackPanel { Spacing = 8 };
        private TextBox ExplainBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, IsReadOnly = true };
        private Button ViewDetailApplyBtn = new Button { Content = "View Detail (Apply)" };
        private Button ApplyBtn = new Button { Content = "Apply" };
        private Button OkBtn = new Button { Content = "OK" };
        private Button CancelBtn = new Button { Content = "Cancel" };

        private readonly Dictionary<string, FrameworkElement> _elementControls = new();
        private readonly List<ListEditorWindow> _childEditors = new();
        private readonly List<Window> _childWindows = new();

        public event EventHandler? Saved;

        public EditSettingWindow()
        {
            Title = "Edit Policy Setting";
            Content = BuildLayout();
            WindowHelpers.Resize(this, 1400, 900);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);

            SectionSelector.SelectionChanged += SectionSelector_SelectionChanged;
            OptNotConfigured.Checked += StateRadio_Checked;
            OptEnabled.Checked += StateRadio_Checked;
            OptDisabled.Checked += StateRadio_Checked;
            ViewDetailApplyBtn.Click += ViewDetailApplyBtn_Click;
            ApplyBtn.Click += ApplyBtn_Click;
            OkBtn.Click += OkBtn_Click;
            CancelBtn.Click += (s, e) => Close();

            this.Closed += (s, e) =>
            {
                foreach (var w in _childEditors.ToArray()) { try { w.Close(); } catch { } }
                _childEditors.Clear();
                foreach (var w in _childWindows.ToArray()) { try { w.Close(); } catch { } }
                _childWindows.Clear();
            };
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { RowSpacing = 8, ColumnSpacing = 20, Padding = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            Grid.SetRow(SettingTitle, 0); Grid.SetColumnSpan(SettingTitle, 2); root.Children.Add(SettingTitle);

            var header = new Grid { ColumnSpacing = 20 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            Grid.SetRow(header, 1); Grid.SetColumnSpan(header, 2); root.Children.Add(header);

            var leftHead = new StackPanel { Spacing = 8 };
            var editingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            editingRow.Children.Add(new TextBlock { Text = "Editing for", VerticalAlignment = VerticalAlignment.Center });
            SectionSelector.Items.Add(new ComboBoxItem { Content = "Computer" });
            SectionSelector.Items.Add(new ComboBoxItem { Content = "User" });
            editingRow.Children.Add(SectionSelector);
            leftHead.Children.Add(editingRow);
            leftHead.Children.Add(new StackPanel { Spacing = 4, Children = { OptNotConfigured, OptEnabled, OptDisabled } });
            Grid.SetColumn(leftHead, 0); header.Children.Add(leftHead);

            var rightHead = new Grid { ColumnSpacing = 8 };
            rightHead.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightHead.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rightHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rightHead.Children.Add(new TextBlock { Text = "Comment", VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(CommentBox, 1); rightHead.Children.Add(CommentBox);
            var sup = new TextBlock { Text = "Supported on", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(sup, 1); rightHead.Children.Add(sup);
            Grid.SetRow(SupportedBox, 1); Grid.SetColumn(SupportedBox, 1); rightHead.Children.Add(SupportedBox);
            Grid.SetColumn(rightHead, 1); header.Children.Add(rightHead);

            var middle = new Grid { ColumnSpacing = 20 };
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            Grid.SetRow(middle, 2); Grid.SetColumnSpan(middle, 2); root.Children.Add(middle);

            var optScroll = new ScrollViewer { MaxHeight = 600, Content = OptionsPanel };
            Grid.SetColumn(optScroll, 0); middle.Children.Add(optScroll);
            var expScroll = new ScrollViewer { MaxHeight = 600, Content = ExplainBox };
            Grid.SetColumn(expScroll, 1); middle.Children.Add(expScroll);

            var bottom = new Grid(); bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bottom.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ViewDetailApplyBtn } });
            Grid.SetColumn(ApplyBtn, 1); bottom.Children.Add(ApplyBtn);
            Grid.SetColumn(OkBtn, 2); bottom.Children.Add(OkBtn);
            Grid.SetColumn(CancelBtn, 3); bottom.Children.Add(CancelBtn);
            Grid.SetRow(bottom, 3); Grid.SetColumnSpan(bottom, 2); root.Children.Add(bottom);

            return root;
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
            ExplainBox.Text = policy.DisplayExplanation ?? string.Empty;

            SectionSelector.IsEnabled = policy.RawPolicy.Section == AdmxPolicySection.Both;
            SectionSelector.SelectedIndex = (section == AdmxPolicySection.Machine) ? 0 : 1;

            BuildElements();
            LoadStateFromSource();
            StateRadio_Checked(this, null!);

            WindowHelpers.BringToFront(this);
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
                            var cb = new ComboBox();
                            foreach (var (item, idx) in e.Items.Select((it, ix) => (it, ix)))
                            {
                                cb.Items.Add(new ComboItem { Id = idx, Text = _bundle.ResolveString(item.DisplayCode, _policy.RawPolicy.DefinedIn) });
                            }
                            if (p.DefaultItemID.HasValue) cb.SelectedIndex = p.DefaultItemID.Value;
                            control = cb; label = p.Label; break;
                        }
                    case "listBox":
                        {
                            var p = (ListPresentationElement)pres;
                            var e = (ListPolicyElement)elemDict[pres.ID];
                            var btn = new Button { Content = "Edit..." };
                            btn.Click += (s, e2) =>
                            {
                                var win = new ListEditorWindow();
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
                    if (!string.IsNullOrEmpty(label)) OptionsPanel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
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
            SaveToSource();
            var win = new DetailPolicyFormattedWindow();
            _childWindows.Add(win);
            win.Closed += (s, ee) => _childWindows.Remove(win);
            var section = _policy.RawPolicy.Section == AdmxPolicySection.Both ? _currentSection : _policy.RawPolicy.Section;
            win.Initialize(_policy, _bundle, _compSource, _userSource, section);
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
