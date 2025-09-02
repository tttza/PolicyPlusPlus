using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PolicyPlusModTests.TestHelpers
{
    // Utilities for testing per-monitor DPI changes and detecting layout issues.
    internal static class DpiTestHelper
    {
        private const int WM_DPICHANGED = 0x02E0;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static void InitializeForm(Form form, float initialDpiScale = 1.25f)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(10, 10);
            form.Size = new Size(800, 600);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            using var _ = new Form(); // ensure WinForms is initialized
            form.CreateControl();
            form.Show();
            Application.DoEvents();

            if (Math.Abs(initialDpiScale - 1.0f) > 0.001f)
            {
                SimulateDpiChange(form, (uint)(96 * initialDpiScale));
            }
        }

        public static void SimulateDpiChange(Form form, uint targetDpi)
        {
            var dpiX = (ushort)targetDpi;
            var dpiY = (ushort)targetDpi;
            int wParam = (dpiY << 16) | dpiX;

            var suggested = new RECT
            {
                left = form.Left,
                top = form.Top,
                right = form.Right,
                bottom = form.Bottom
            };
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<RECT>());
            try
            {
                Marshal.StructureToPtr(suggested, ptr, false);
                SendMessage(form.Handle, WM_DPICHANGED, new IntPtr(wParam), ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            Application.DoEvents();
        }

        public static DpiLayoutReport AnalyzeLayout(Control root)
        {
            var report = new DpiLayoutReport();
            AnalyzeControl(root, report);
            return report;
        }

        private static void AnalyzeControl(Control c, DpiLayoutReport report)
        {
            if (c is null) return;
            if (!c.Visible) return;

            if (c.Parent != null)
            {
                if (!c.Parent.DisplayRectangle.Contains(c.Bounds))
                {
                    report.AddIssue(c, "OutOfParentBounds");
                }
            }

            if (c.Width <= 0 || c.Height <= 0)
            {
                report.AddIssue(c, "ZeroOrNegativeSize");
            }

            if (c is Label lbl && lbl.AutoSize)
            {
                var proposed = TextRenderer.MeasureText(lbl.Text ?? string.Empty, lbl.Font);
                if (proposed.Width > c.Width + 4)
                {
                    report.AddIssue(c, "LabelClipped");
                }
            }

            // Simple overlap detection among siblings
            if (c.Parent != null)
            {
                foreach (Control sibling in c.Parent.Controls)
                {
                    if (sibling == c || !sibling.Visible) continue;
                    if (c.Bounds.IntersectsWith(sibling.Bounds))
                    {
                        // Allow containment (e.g., panels), flag only tight intersections
                        var inter = Rectangle.Intersect(c.Bounds, sibling.Bounds);
                        if (inter.Width > 2 && inter.Height > 2)
                        {
                            report.AddIssue(c, $"Overlaps:{sibling.Name}");
                            break;
                        }
                    }
                }
            }

            foreach (Control child in c.Controls)
            {
                AnalyzeControl(child, report);
            }
        }

        public static T RunInSta<T>(Func<T> func)
        {
            T result = default!;
            Exception? ex = null;
            var t = new System.Threading.Thread(() =>
            {
                try { result = func(); }
                catch (Exception e) { ex = e; }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            if (ex != null) throw ex;
            return result;
        }
    }

    internal sealed class DpiLayoutReport
    {
        public class Issue
        {
            public Control Control { get; init; } = null!;
            public string Kind { get; init; } = string.Empty;
            public override string ToString() => $"{Control?.FindForm()?.Name}/{Control?.Name}: {Kind}";
        }

        private readonly System.Collections.Generic.List<Issue> _issues = new();
        public Issue[] Issues => _issues.ToArray();
        public bool HasIssues => _issues.Count > 0;
        public void AddIssue(Control control, string kind) => _issues.Add(new Issue { Control = control, Kind = kind });
        public override string ToString() => string.Join("; ", _issues.ConvertAll(i => i.ToString()));
    }
}
