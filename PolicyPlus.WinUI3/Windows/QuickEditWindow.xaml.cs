using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.IO;
using PolicyPlus.WinUI3.ViewModels;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Color = Windows.UI.Color;
using System.Collections.Specialized;
using Windows.Foundation;
using Windows.Graphics;
using System.ComponentModel;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class QuickEditWindow : Window
    {
        private readonly List<ListEditorWindow> _childEditors = new(); // restored
        private AdmxBundle? _bundle; private IPolicySource? _compSource; private IPolicySource? _userSource;
        private readonly IElevationService _elevation = new ElevationServiceAdapter();
        private bool _isSaving; private bool _initialized; private bool _pendingSize;

        public QuickEditWindow()
        {
            InitializeComponent();
            try { SystemBackdrop = new MicaBackdrop(); } catch { }
            ChildWindowCommon.Initialize(this, 680, 520, ApplyCurrentTheme);
            if (_grid != null) _grid.ParentQuickEditWindow = this;
            if (_saveButton != null) _saveButton.Click += async (_, __) => await SaveAsync();
            if (_closeButton != null) _closeButton.Click += (_, __) => Close();
            var saveAccel = new KeyboardAccelerator { Key = global::Windows.System.VirtualKey.S, Modifiers = global::Windows.System.VirtualKeyModifiers.Control };
            saveAccel.Invoked += async (a, b) => { if (_saveButton?.IsEnabled == true && !_isSaving) { await SaveAsync(); b.Handled = true; } };
            RootShell?.KeyboardAccelerators.Add(saveAccel);
            try { App.ScaleChanged += (_, __) => ScheduleSize(); } catch { }
            Closed += (s, e) => { try { PendingChangesService.Instance.Pending.CollectionChanged -= Pending_CollectionChanged; } catch { } };
        }

        public void Initialize(AdmxBundle bundle, IPolicySource? comp, IPolicySource? user, IEnumerable<PolicyPlusPolicy> policies)
        {
            _bundle = bundle; _compSource = comp; _userSource = user;
            if (_grid != null)
            {
                _grid.Rows.Clear(); foreach (var p in policies) _grid.Rows.Add(new QuickEditRow(p, bundle, comp, user));
                try { if (_grid.Columns != null) _grid.Columns.PropertyChanged += Columns_PropertyChanged; } catch { }
            }
            if (_headerCount != null) _headerCount.Text = $"{_grid?.Rows.Count ?? 0} policies";
            try { PendingChangesService.Instance.Pending.CollectionChanged += Pending_CollectionChanged; } catch { }
            UpdateUnsavedIndicator();
            _initialized = true; ScheduleSize();
        }

        private void Columns_PropertyChanged(object? s, PropertyChangedEventArgs e) => ScheduleSize();
        private void Pending_CollectionChanged(object? s, NotifyCollectionChangedEventArgs e) { try { DispatcherQueue.TryEnqueue(UpdateUnsavedIndicator); } catch { } }

        private void ScheduleSize()
        {
            if (!_initialized || _pendingSize) return; _pendingSize = true;
            try
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                { _pendingSize = false; try { ApplyAutoSize(); } catch { } });
            }
            catch { _pendingSize = false; }
        }

        private void ApplyAutoSize()
        {
            if (RootShell == null) return;
            double customScale = 1.0; try { customScale = System.Math.Max(0.1, App.CurrentScale); } catch { }
            double dpiScale = 1.0; try { dpiScale = WindowHelpers.GetDisplayScale(this); } catch { }

            // Let content measure itself unconstrained (unscaled logical size)
            RootShell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double logicalW = RootShell.DesiredSize.Width;
            double logicalH = RootShell.DesiredSize.Height;
            if (logicalW <= 0 || logicalH <= 0) { RootShell.UpdateLayout(); logicalW = RootShell.ActualWidth; logicalH = RootShell.ActualHeight; }
            if (logicalW <= 0 || logicalH <= 0) return;

            // Column-model based tightening with generous slack to avoid clipping at low scales
            if (_grid?.Columns != null)
            {
                var c = _grid.Columns;
                double model = c.Name.Value + c.Id.Value + c.UserState.Value + c.UserOptions.Value + c.ComputerState.Value + c.ComputerOptions.Value + (6 * 5);
                try { model += RootShell.Padding.Left + RootShell.Padding.Right; } catch { }
                // Base slack: per-column inner margins + cell content spacing
                double baseSlack = 8 /*base*/ + (6 * 8); // 8px * 6 columns ~= 56
                if (customScale < 0.9) baseSlack += 12;   // small extra for rounding
                if (customScale < 0.8) baseSlack += 24;   // more extra for very small scales (e.g. 67%)
                if (model > 0 && (model + baseSlack) < logicalW)
                    logicalW = model + baseSlack; // only shrink, keep slack
            }

            // Convert to client pixel size: logical * customScale * dpiScale
            double targetClientW = logicalW * customScale * dpiScale + 2; // +2 slack
            double targetClientH = logicalH * customScale * dpiScale + 2;

            // Clamp to work area
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                if (area != null)
                {
                    double maxW = area.WorkArea.Width * 0.95; if (targetClientW > maxW) targetClientW = maxW;
                    double maxH = area.WorkArea.Height * 0.95; if (targetClientH > maxH) targetClientH = maxH;
                }
            }
            catch { }

            var hwnd2 = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id2 = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd2);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id2); if (appWindow == null) return;
            int newW = (int)System.Math.Ceiling(targetClientW);
            int newH = (int)System.Math.Ceiling(targetClientH);
            if (System.Math.Abs(newW - appWindow.ClientSize.Width) >= 2 || System.Math.Abs(newH - appWindow.ClientSize.Height) >= 2)
                appWindow.ResizeClient(new SizeInt32(newW, newH));
        }

        private void UpdateUnsavedIndicator()
        {
            try
            {
                if (_grid == null || _unsavedText == null || _saveButton == null) return;
                var ids = _grid.Rows.Select(r => r.Policy.UniqueID).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
                var relevant = PendingChangesService.Instance.Pending.Where(p => ids.Contains(p.PolicyId)).ToList();
                int count = relevant.Count;
                _unsavedText.Text = count > 0 ? $"Unsaved changes ({count})" : "";
                _saveButton.IsEnabled = count > 0 && _bundle != null && !_isSaving;
            }
            catch { }
        }

        private async Task SaveAsync()
        {
            if (_bundle == null || _isSaving || _grid == null) return;
            var ids = _grid.Rows.Select(r => r.Policy.UniqueID).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            var relevant = PendingChangesService.Instance.Pending.Where(p => ids.Contains(p.PolicyId)).ToList();
            if (relevant.Count == 0) return;
            _isSaving = true; SetStatus("Saving..."); SetSaving(true); if (_saveButton != null) _saveButton.IsEnabled = false;
            try
            {
                var (ok, error, _, _) = await SaveChangesCoordinator.SaveAsync(_bundle, relevant, _elevation, System.TimeSpan.FromSeconds(8), triggerRefresh: true);
                if (ok)
                {
                    PendingChangesService.Instance.Applied(relevant.ToArray());
                    try { SettingsService.Instance.SaveHistory(PendingChangesService.Instance.History.ToList()); } catch { }
                    SetStatus(relevant.Count == 1 ? "Saved 1 change." : $"Saved {relevant.Count} changes.");
                }
                else SetStatus(string.IsNullOrEmpty(error) ? "Save failed." : $"Save failed: {error}");
            }
            finally { _isSaving = false; SetSaving(false); UpdateUnsavedIndicator(); }
        }

        private void SetSaving(bool saving) { try { if (_savingOverlay != null) _savingOverlay.Visibility = saving ? Visibility.Visible : Visibility.Collapsed; } catch { } }
        private void SetStatus(string text) { if (_statusText != null) _statusText.Text = text; }

        private void ApplyCurrentTheme()
        {
            try
            {
                if (RootShell == null) return;
                var theme = App.CurrentTheme;
                RootShell.RequestedTheme = theme;
                Brush? bg = null;
                if (theme == ElementTheme.Light) bg = new SolidColorBrush(Microsoft.UI.Colors.White);
                else if (theme == ElementTheme.Dark) bg = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x20, 0x20));
                else if (Application.Current.Resources.TryGetValue("WindowBackground", out var res) && res is Brush b) bg = b;
                if (bg != null)
                {
                    RootShell.Background = bg;
                    try { (ScaleHost as Grid)!.Background = bg; } catch { }
                }
            }
            catch { }
        }

        // Restored helper to register child windows so they close with parent
        internal void RegisterChild(ListEditorWindow w)
        {
            if (w == null) return;
            _childEditors.Add(w);
            w.Closed += (s, e) => _childEditors.Remove(w);
        }

        // Restored static helper used by MainWindow to choose policies for Quick Edit
        public static IEnumerable<PolicyPlusPolicy> BuildSourcePolicies(IEnumerable<PolicyPlusPolicy> allVisible, IEnumerable<PolicyPlusPolicy> selected, IEnumerable<string> bookmarkIds, bool bookmarksOnly, int cap = 500)
        {
            var result = selected.Any()
                ? selected
                : (bookmarksOnly || bookmarkIds.Any())
                    ? allVisible.Where(p => bookmarkIds.Contains(p.UniqueID, System.StringComparer.OrdinalIgnoreCase))
                    : allVisible;
            return result.Distinct().OrderBy(p => p.DisplayName).Take(cap);
        }
    }
}
