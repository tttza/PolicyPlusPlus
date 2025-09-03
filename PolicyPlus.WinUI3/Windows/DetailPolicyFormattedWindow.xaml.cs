using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using PolicyPlus.WinUI3.Utils;
using Windows.ApplicationModel.DataTransfer;
using PolicyPlus.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Input;
using PolicyPlus.Core.IO;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.Admx;
using PolicyPlus.WinUI3.Services;

namespace PolicyPlus.WinUI3.Windows
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

        public DetailPolicyFormattedWindow()
        {
            this.Title = "Policy Details - Formatted";
            InitializeComponent();

            CopyPathBtn.Click += (s, e) => CopyToClipboard(PathBox.Text);
            CopyRegBtn.Click += (s, e) => CopyToClipboard(RegBox.Text);
            ToggleViewBtn.Click += ToggleViewBtn_Click;
            CloseBtn.Click += (s, e) => this.Close();
            if (OpenRegBtn != null) OpenRegBtn.Click += OpenRegBtn_Click;

            ApplyThemeResources();
            App.ThemeChanged += (s, e) => ApplyThemeResources();

            WindowHelpers.ResizeForDisplayScale(this, 600, 520);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);

            try { ScaleHelper.Attach(this, ScaleHost, RootShell); } catch { }
        }

        private void Accel_ToggleView(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { ToggleViewBtn_Click(this, new RoutedEventArgs()); } catch { } args.Handled = true; }
        private void Accel_CopyPath(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { CopyToClipboard(PathBox.Text); } catch { } args.Handled = true; }
        private void Accel_CopyReg(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { CopyToClipboard(RegBox.Text); } catch { } args.Handled = true; }
        private void Accel_Close(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { this.Close(); } catch { } args.Handled = true; }

        private void ApplyThemeResources()
        {
            if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme;
            var inputBg = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"]; var inputStroke = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"]; var inputFg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            NameBox.Background = inputBg; NameBox.BorderBrush = inputStroke; NameBox.Foreground = inputFg;
            IdBox.Background = inputBg; IdBox.BorderBrush = inputStroke; IdBox.Foreground = inputFg;
            DefinedInBox.Background = inputBg; DefinedInBox.BorderBrush = inputStroke; DefinedInBox.Foreground = inputFg;
            PathBox.Background = inputBg; PathBox.BorderBrush = inputStroke; PathBox.Foreground = inputFg;
            RegBox.Background = inputBg; RegBox.BorderBrush = inputStroke; RegBox.Foreground = inputFg;
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
                var hive = _currentSection == AdmxPolicySection.User ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
                await RegeditNavigationService.OpenAtKeyAsync(hive, kv.Key);
            }
            catch { }
        }

        private void CopyToClipboard(string text)
        {
            try
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(text ?? string.Empty);
                Clipboard.SetContent(dp);
            }
            catch { }
        }

        public void Initialize(PolicyPlusPolicy policy, AdmxBundle bundle, IPolicySource compSource, IPolicySource userSource, AdmxPolicySection section)
        {
            _policy = policy; _bundle = bundle; _compSource = compSource; _userSource = userSource; _currentSection = section;

            NameBox.Text = policy.DisplayName;
            IdBox.Text = policy.UniqueID;
            DefinedInBox.Text = System.IO.Path.GetFileName(policy.RawPolicy.DefinedIn?.SourceFile ?? string.Empty);

            // Build path panel via ViewModel helper for testability
            PathBox.Text = DetailPathFormatter.BuildPathText(_policy);

            // Build registry panel (formatted + .reg)
            var src = _currentSection == AdmxPolicySection.User ? _userSource : _compSource;
            _regFormattedCache = RegistryViewFormatter.BuildRegistryFormatted(_policy, src, _currentSection);
            _regFileCache = RegistryViewFormatter.BuildRegExport(_policy, src, _currentSection);
            _showRegFile = false;
            RegBox.Text = _regFormattedCache;
        }

        // BuildPathText now lives in ViewModels.DetailPathFormatter
    }
}
