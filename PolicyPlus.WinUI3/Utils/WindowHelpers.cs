using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;
using Windows.Graphics;
using Windows.ApplicationModel.DataTransfer;

namespace PolicyPlus.WinUI3.Utils
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
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static void BringToFront(Window window)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(id);
                appWindow?.MoveInZOrderAtTop();

                // Topmost toggle trick to force Z-order raise
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

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
    }

    public static class DataPackageExtensions
    {
        public static T Also<T>(this T obj, System.Action<T> act) { act(obj); return obj; }
    }
}
