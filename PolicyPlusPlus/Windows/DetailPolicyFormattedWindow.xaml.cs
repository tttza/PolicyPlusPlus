using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusCore.Utilities; // culture preference
using PolicyPlusPlus.Logging; // logging
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace PolicyPlusPlus.Windows
{
    public sealed partial class DetailPolicyFormattedWindow : Window
    {
        private PolicyPlusPolicy _policy = null!;
        private AdmxBundle _bundle = null!;
        private IPolicySource _compSource = null!;
        private IPolicySource _userSource = null!;
        private AdmxPolicySection _currentSection;

        private string _regFormattedCache = string.Empty;
        private string _regFileCache = string.Empty;
        private bool _showRegFile = false;
        private string _joinSymbol = "+";
        private ToggleButton? _langToggle;
        private bool _useSecondLanguage = false;

        public DetailPolicyFormattedWindow()
        {
            this.Title = "Policy Details - Formatted";
            InitializeComponent();

            CopyPathBtn.Click += (s, e) => CopyToClipboard(PathBox.Text);
            CopyRegBtn.Click += (s, e) => CopyToClipboard(RegBox.Text);
            CopyNameBtn.Click += (s, e) => CopyToClipboard(NameBox.Text);
            CopyIdBtn.Click += (s, e) => CopyToClipboard(IdBox.Text);
            CopyDefinedInBtn.Click += (s, e) => CopyToClipboard(DefinedInBox.Text);
            ToggleViewBtn.Click += ToggleViewBtn_Click;
            CloseBtn.Click += (s, e) => this.Close();
            OpenRegBtn.Click += OpenRegBtn_Click;

            // Centralized child window boilerplate
            ChildWindowCommon.Initialize(this, 600, 520, ApplyThemeResources);

            try
            {
                _langToggle = RootShell.FindName("LangToggle") as ToggleButton;
                if (_langToggle != null)
                {
                    _langToggle.Checked += LangToggle_Checked;
                    _langToggle.Unchecked += LangToggle_Checked;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"lang toggle init failed: {ex.Message}");
            }

            // Load persisted join symbol
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                if (!string.IsNullOrEmpty(s.PathJoinSymbol))
                    _joinSymbol = s.PathJoinSymbol!.Substring(
                        0,
                        Math.Min(1, s.PathJoinSymbol!.Length)
                    );
                PathSymbolBtn.Content = _joinSymbol;
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"load join symbol failed: {ex.Message}");
            }
        }

        private void Accel_ToggleView(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                ToggleViewBtn_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"accel toggle failed: {ex.Message}");
            }
            args.Handled = true;
        }

        private void Accel_CopyPath(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                CopyToClipboard(PathBox.Text);
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"accel copy path failed: {ex.Message}");
            }
            args.Handled = true;
        }

        private void Accel_CopyReg(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                CopyToClipboard(RegBox.Text);
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"accel copy reg failed: {ex.Message}");
            }
            args.Handled = true;
        }

        private void Accel_Close(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"accel close failed: {ex.Message}");
            }
            args.Handled = true;
        }

        private void ApplyThemeResources()
        {
            try
            {
                var theme = App.GetEffectiveTheme(this);
                if (Content is FrameworkElement fe)
                    fe.RequestedTheme = theme;
                WindowHelpers.ApplyImmersiveDarkMode(this, theme == ElementTheme.Dark);
                var inputBg = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
                var inputStroke = (Brush)
                    Application.Current.Resources["ControlStrokeColorDefaultBrush"];
                var inputFg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                NameBox.Background = inputBg;
                NameBox.BorderBrush = inputStroke;
                NameBox.Foreground = inputFg;
                IdBox.Background = inputBg;
                IdBox.BorderBrush = inputStroke;
                IdBox.Foreground = inputFg;
                DefinedInBox.Background = inputBg;
                DefinedInBox.BorderBrush = inputStroke;
                DefinedInBox.Foreground = inputFg;
                PathBox.Background = inputBg;
                PathBox.BorderBrush = inputStroke;
                PathBox.Foreground = inputFg;
                RegBox.Background = inputBg;
                RegBox.BorderBrush = inputStroke;
                RegBox.Foreground = inputFg;
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"apply theme failed: {ex.Message}");
            }
        }

        private void ToggleViewBtn_Click(object sender, RoutedEventArgs e)
        {
            _showRegFile = !_showRegFile;
            RegBox.Text = _showRegFile ? _regFileCache : _regFormattedCache;
        }

        private async void OpenRegBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var values = PolicyProcessing.GetReferencedRegistryValues(_policy);
                if (values.Count == 0)
                    return;
                var kv = values[0];
                var hive =
                    _currentSection == AdmxPolicySection.User
                        ? "HKEY_CURRENT_USER"
                        : "HKEY_LOCAL_MACHINE";
                await RegeditNavigationService.OpenAtKeyAsync(hive, kv.Key);
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"open regedit exception: {ex.Message}");
            }
        }

        private void CopyToClipboard(string text)
        {
            try
            {
                var normalized = NormalizeLineEndings(text ?? string.Empty);
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(normalized);
                Clipboard.SetContent(dp);
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"clipboard copy failed: {ex.Message}");
            }
        }

        private static string NormalizeLineEndings(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");
            return s.Replace("\n", "\r\n");
        }

        public void Initialize(
            PolicyPlusPolicy policy,
            AdmxBundle bundle,
            IPolicySource compSource,
            IPolicySource userSource,
            AdmxPolicySection section
        )
        {
            _policy = policy;
            _bundle = bundle;
            _compSource = compSource;
            _userSource = userSource;
            _currentSection = section;

            var s = SettingsService.Instance.LoadSettings();
            if (_langToggle != null)
            {
                _langToggle.Visibility =
                    (s.SecondLanguageEnabled ?? false) ? Visibility.Visible : Visibility.Collapsed;
                ToolTipService.SetToolTip(
                    _langToggle,
                    (s.SecondLanguageEnabled ?? false)
                        ? $"Toggle 2nd language ({s.SecondLanguage ?? "en-US"})"
                        : "2nd language disabled in preferences"
                );
            }

            // Basic fields
            IdBox.Text = policy.UniqueID;
            DefinedInBox.Text = System.IO.Path.GetFileName(
                policy.RawPolicy.DefinedIn?.SourceFile ?? string.Empty
            );

            RefreshTexts();
        }

        private void PathSymbolItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem m)
                {
                    var sym = (m.Text ?? "+").Trim();
                    if (string.IsNullOrEmpty(sym))
                        sym = "+";
                    _joinSymbol = sym.Substring(0, Math.Min(1, sym.Length));
                    PathSymbolBtn.Content = _joinSymbol;
                    // Rebuild all localized texts to respect the current second-language toggle and ADML availability
                    RefreshTexts();
                    try
                    {
                        SettingsService.Instance.UpdatePathJoinSymbol(_joinSymbol);
                    }
                    catch (Exception ex2)
                    {
                        Log.Debug("DetailPolicyFmt", $"persist join symbol failed: {ex2.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", $"join symbol change failed: {ex.Message}");
            }
        }

        private void LangToggle_Click(object sender, RoutedEventArgs e)
        {
            _useSecondLanguage = !_useSecondLanguage;
            RefreshTexts();
        }

        private void LangToggle_Checked(object sender, RoutedEventArgs e)
        {
            _useSecondLanguage = _langToggle?.IsChecked == true;
            RefreshTexts();
        }

        private void RefreshTexts()
        {
            if (_policy == null)
                return;
            var s = SettingsService.Instance.LoadSettings();
            bool prefEnabled = s.SecondLanguageEnabled ?? false;
            string lang = s.SecondLanguage ?? "en-US";
            // useFallback:false -> only count as present if that culture has its own ADML
            bool hasAdml =
                prefEnabled && LocalizedTextService.HasAdml(_policy, lang, useFallback: false);

            if (_langToggle != null)
            {
                _langToggle.Visibility = prefEnabled ? Visibility.Visible : Visibility.Collapsed;
                _langToggle.IsEnabled = hasAdml;
                ToolTipService.SetToolTip(
                    _langToggle,
                    prefEnabled
                        ? (
                            hasAdml
                                ? $"Toggle 2nd language ({lang})"
                                : $"{lang} language resources not found"
                        )
                        : "2nd language disabled in preferences"
                );
            }

            bool useSecond = hasAdml && _useSecondLanguage;
            NameBox.Text = useSecond
                ? LocalizedTextService.GetPolicyNameIn(_policy, lang)
                : _policy.DisplayName;
            PathBox.Text = DetailPathFormatter.BuildPathText(_policy, _joinSymbol, useSecond, lang);

            var src = _currentSection == AdmxPolicySection.User ? _userSource : _compSource;
            _regFormattedCache = RegistryViewFormatter.BuildRegistryFormatted(
                _policy,
                src,
                _currentSection,
                useSecond,
                lang
            );
            _regFileCache = RegistryViewFormatter.BuildRegExport(_policy, src, _currentSection);
            RegBox.Text = _showRegFile ? _regFileCache : _regFormattedCache;

            // Attempt asynchronous fallback fill if primary explanation empty and fallback setting enabled
            if (!useSecond && string.IsNullOrWhiteSpace(_policy.DisplayExplanation))
            {
                try
                {
                    var cultures = BuildOrderedCulturesForDetailFallback();
                    if (cultures.Count > 1)
                        _ = TryFillFromCacheAsync(cultures); // fire and forget
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "DetailPolicyFmt",
                        "detail cache fallback start failed: " + ex.Message
                    );
                }
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
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(_policy.DisplayExplanation))
                        {
                            if (string.IsNullOrWhiteSpace(NameBox.Text))
                                NameBox.Text = detail.DisplayName;
                        }
                    }
                    catch (Exception exUi)
                    {
                        Log.Debug(
                            "DetailPolicyFmt",
                            "cache detail UI apply failed: " + exUi.Message
                        );
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Debug("DetailPolicyFmt", "cache detail retrieval failed: " + ex.Message);
            }
        }
    }
}
