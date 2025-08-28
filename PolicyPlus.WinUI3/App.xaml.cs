using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using PolicyPlus.WinUI3.Windows;
using PolicyPlus.WinUI3.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PolicyPlus.WinUI3
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window? Window { get; private set; }
        private static readonly HashSet<Window> _secondaryWindows = new();
        private static readonly Dictionary<string, EditSettingWindow> _openEditWindows = new();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Window = new MainWindow();
            Window.Closed += (s, e) => CloseAllSecondaryWindows();
            Window.Activate();
        }

        public static void RegisterWindow(Window w)
        {
            if (w == Window) return;
            _secondaryWindows.Add(w);
        }
        public static void UnregisterWindow(Window w)
        {
            _secondaryWindows.Remove(w);
        }
        public static void CloseAllSecondaryWindows()
        {
            foreach (var w in _secondaryWindows.ToArray())
            {
                try { w.Close(); } catch { }
            }
            _secondaryWindows.Clear();
            _openEditWindows.Clear();
        }

        public static bool TryActivateExistingEdit(string policyId)
        {
            if (_openEditWindows.TryGetValue(policyId, out var win))
            {
                try
                {
                    WindowHelpers.BringToFront(win);
                    win.Activate();
                    var timer = win.DispatcherQueue.CreateTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(180);
                    timer.IsRepeating = false;
                    timer.Tick += (s, e) => { try { WindowHelpers.BringToFront(win); win.Activate(); } catch { } };
                    timer.Start();
                }
                catch { }
                return true;
            }
            return false;
        }

        public static void RegisterEditWindow(string policyId, EditSettingWindow win)
        {
            _openEditWindows[policyId] = win;
            win.Closed += (s, e) => { _openEditWindows.Remove(policyId); };
        }
    }
}
