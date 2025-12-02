using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace PolicyPlusPlus.Utils
{
    internal static class WindowHelpers
    {
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags
        );

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private enum DWMWINDOWATTRIBUTE : uint
        {
            SYSTEMBACKDROP_TYPE = 38,
            USE_IMMERSIVE_DARK_MODE = 20,
            USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19,
        }

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            DWMWINDOWATTRIBUTE dwAttribute,
            ref int pvAttribute,
            int cbAttribute
        );

        public static void BringToFront(Window window)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(id);
                appWindow?.MoveInZOrderAtTop();

                // Topmost toggle trick to force Z-order raise
                SetWindowPos(
                    hwnd,
                    HWND_TOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW
                );
                SetWindowPos(
                    hwnd,
                    HWND_NOTOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW
                );

                // Foreground requests
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            catch { }
        }

        public static void Resize(Window window, int width, int height)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(id);
                appWindow?.Resize(new SizeInt32(width, height));
            }
            catch { }
        }

        public static void ResizeClient(Window window, int clientWidth, int clientHeight)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(id);
                appWindow?.ResizeClient(new SizeInt32(clientWidth, clientHeight));
            }
            catch { }
        }

        public static double GetDisplayScale(Window window)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                uint dpi = GetDpiForWindow(hwnd);
                return dpi / 96.0;
            }
            catch
            {
                return 1.0;
            }
        }

        public static void ResizeForDisplayScale(
            Window window,
            int baseWidth,
            int baseHeight,
            double workAreaMargin = 0.9
        )
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(id);
                if (appWindow == null)
                {
                    Resize(window, baseWidth, baseHeight);
                    return;
                }

                double scale = GetDisplayScale(window);
                int targetW = (int)Math.Round(baseWidth * scale);
                int targetH = (int)Math.Round(baseHeight * scale);

                var area = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Primary);
                if (area != null)
                {
                    int maxW = (int)Math.Round(area.WorkArea.Width * workAreaMargin);
                    int maxH = (int)Math.Round(area.WorkArea.Height * workAreaMargin);
                    targetW = Math.Min(targetW, maxW);
                    targetH = Math.Min(targetH, maxH);
                }

                appWindow.Resize(new SizeInt32(Math.Max(200, targetW), Math.Max(200, targetH)));
            }
            catch { }
        }

        /// <summary>
        /// Resize only the client area width of a window to fit its current content DesiredSize, capped by work area.
        /// Keeps current client height. Uses AppWindow.ResizeClient for precise client sizing.
        /// </summary>
        public static void ResizeClientWidthToContent(
            Window window,
            double horizontalPadding = 0,
            double maxWorkAreaFraction = 0.95
        )
        {
            try
            {
                if (window?.Content is not FrameworkElement root)
                    return;
                root.UpdateLayout();
                if (root.DesiredSize.Width <= 0.1)
                    root.Measure(
                        new global::Windows.Foundation.Size(
                            double.PositiveInfinity,
                            double.PositiveInfinity
                        )
                    );

                double desiredDipWidth = root.DesiredSize.Width + horizontalPadding;
                if (desiredDipWidth < 50)
                    desiredDipWidth = 50;

                var hwnd = WindowNative.GetWindowHandle(window);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(id);
                if (appWindow == null)
                    return;
                double scale = GetDisplayScale(window);
                int desiredClientPixelWidth = (int)Math.Ceiling(desiredDipWidth * scale);
                int currentClientHeight = appWindow.ClientSize.Height;
                if (currentClientHeight < 200)
                    currentClientHeight = 200;
                var area = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Primary);
                if (area != null)
                {
                    int maxWidth = (int)Math.Round(area.WorkArea.Width * maxWorkAreaFraction);
                    if (desiredClientPixelWidth > maxWidth)
                        desiredClientPixelWidth = maxWidth;
                }
                if (desiredClientPixelWidth < 200)
                    desiredClientPixelWidth = 200;
                appWindow.ResizeClient(new SizeInt32(desiredClientPixelWidth, currentClientHeight));
            }
            catch { }
        }

        public static void ApplyImmersiveDarkMode(Window window, bool enable)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                if (hwnd == IntPtr.Zero)
                    return;

                int useDark = enable ? 1 : 0;
                const int attributeSize = sizeof(int);
                var attr = DWMWINDOWATTRIBUTE.USE_IMMERSIVE_DARK_MODE;
                int hr = DwmSetWindowAttribute(hwnd, attr, ref useDark, attributeSize);
                if (hr != 0)
                {
                    var legacy = DWMWINDOWATTRIBUTE.USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                    _ = DwmSetWindowAttribute(hwnd, legacy, ref useDark, attributeSize);
                }
            }
            catch { }
        }

        public static void ActivateAndBringToFront(Window window)
        {
            if (window == null)
                return;

            void ActivateNow()
            {
                try
                {
                    window.Activate();
                }
                catch { }

                try
                {
                    BringToFront(window);
                }
                catch { }

                try
                {
                    var timer = window.DispatcherQueue?.CreateTimer();
                    if (timer == null)
                        return;
                    timer.Interval = TimeSpan.FromMilliseconds(180);
                    timer.IsRepeating = false;
                    void OnTick(DispatcherQueueTimer sender, object args)
                    {
                        sender.Tick -= OnTick;
                        try
                        {
                            BringToFront(window);
                            window.Activate();
                        }
                        catch { }
                    }
                    timer.Tick += OnTick;
                    timer.Start();
                }
                catch { }
            }

            try
            {
                if (window.Content is FrameworkElement root && !root.IsLoaded)
                {
                    RoutedEventHandler? handler = null;
                    handler = (s, e) =>
                    {
                        root.Loaded -= handler;
                        ActivateNow();
                    };
                    root.Loaded += handler;
                }
                else
                {
                    ActivateNow();
                }
            }
            catch
            {
                ActivateNow();
            }
        }
    }

    public static class DataPackageExtensions
    {
        public static T Also<T>(this T obj, System.Action<T> act)
        {
            act(obj);
            return obj;
        }
    }
}
