using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
using PolicyPlus.WinUI3.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

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

            ApplyThemeResources();
            App.ThemeChanged += (s, e) => ApplyThemeResources();

            // adapt initial size to display scale
            WindowHelpers.ResizeForDisplayScale(this, 600, 520);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);

            TryAttachScale();
            this.Activated += (s, e) => TryAttachScale();
        }

        private void TryAttachScale()
        {
            try
            {
                if (Content is FrameworkElement fe)
                {
                    var host = fe.FindName("ScaleHost") as FrameworkElement;
                    var root = fe.FindName("RootShell") as FrameworkElement;
                    if (host != null && root != null)
                    {
                        ScaleHelper.Attach(this, host, root);
                      }
                }
            }
            catch { }
        }

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
        { _showRegFile = !_showRegFile; RegBox.Text = _showRegFile ? _regFileCache : _regFormattedCache; }

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

            _regFormattedCache = string.Join("\r\n", PolicyProcessing.GetReferencedRegistryValues(policy).Select(kv => kv.Key + (string.IsNullOrEmpty(kv.Value) ? string.Empty : $" ({kv.Value})")));
            _regFileCache = _regFormattedCache; // simplified
            RegBox.Text = _showRegFile ? _regFileCache : _regFormattedCache;

            try
            {
                var sb = new StringBuilder();
                var c = policy.Category; var stack = new Stack<string>();
                while (c != null) { stack.Push(c.DisplayName); c = c.Parent; }
                sb.AppendLine("Administrative Templates");
                foreach (var name in stack) sb.AppendLine("+ " + name);
                sb.AppendLine("+ " + policy.DisplayName);
                PathBox.Text = sb.ToString();
            }
            catch { }
        }
    }
}
