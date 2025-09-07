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
using System;
using PolicyPlus.WinUI3.Logging; // logging

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class QuickEditWindow : Window
    {
        private readonly List<ListEditorWindow> _childEditors = new(); // restored
        private AdmxBundle? _bundle; private IPolicySource? _compSource; private IPolicySource? _userSource;
        private readonly IElevationService _elevation = new ElevationServiceAdapter();
        private bool _isSaving; private bool _initialized; private bool _pendingSize;

        // Track opened EditSetting windows per policy (only one allowed)
        private readonly Dictionary<string, EditSettingWindow> _editWindows = new(System.StringComparer.OrdinalIgnoreCase);

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
            // Subscribe to global source refresh so we stay in sync when other windows save/apply changes
            try { MainWindow.PolicySourcesRefreshed += MainWindow_PolicySourcesRefreshed; } catch { }
            Closed += (s, e) =>
            {
                try { PendingChangesService.Instance.Pending.CollectionChanged -= Pending_CollectionChanged; } catch { }
                try { MainWindow.PolicySourcesRefreshed -= MainWindow_PolicySourcesRefreshed; } catch { }
                // Close any edit windows opened from here
                foreach (var win in _editWindows.Values.ToList())
                {
                    try { win.Close(); } catch { }
                }
                _editWindows.Clear();
            };
        }

        // When main window reloads policy sources, capture fresh references and rebuild row states
        private void MainWindow_PolicySourcesRefreshed(object? sender, EventArgs e)
        {
            try
            {
                var main = App.Window as MainWindow;
                if (main == null || _bundle == null || _grid == null) return;
                // Pull fresh sources via reflection (keeps existing access pattern isolated here)
                var compField = typeof(MainWindow).GetField("_compSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var userField = typeof(MainWindow).GetField("_userSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                _compSource = compField?.GetValue(main) as IPolicySource;
                _userSource = userField?.GetValue(main) as IPolicySource;
                // Refresh each row from the new sources (do not trigger pending queue)
                foreach (var row in _grid.Rows)
                {
                    try { RefreshRowFromSources(row); } catch { }
                }
            }
            catch { }
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

        // Open EditSetting window for given policy id; if already open, activate existing
        internal void OpenEditForPolicy(string policyId)
        {
            if (_bundle == null) return;
            if (_editWindows.TryGetValue(policyId, out var existing))
            {
                try { existing.Activate(); WindowHelpers.BringToFront(existing); } catch { }
                return;
            }
            var row = _grid?.Rows.FirstOrDefault(r => r.Policy.UniqueID.Equals(policyId, System.StringComparison.OrdinalIgnoreCase));
            var policy = row?.Policy;
            if (policy == null || _compSource == null || _userSource == null) return;

            // Decide initial section with priority:
            // 1. Pending change scope (Computer preferred when both pending)
            // 2. Configured state in Computer
            // 3. Configured state in User
            // 4. Default to Computer (Machine)
            AdmxPolicySection initialSection;
            if (policy.RawPolicy.Section == AdmxPolicySection.Both)
            {
                var pend = PendingChangesService.Instance.Pending.Where(p => string.Equals(p.PolicyId, policyId, StringComparison.OrdinalIgnoreCase)).ToList();
                bool hasUserPending = pend.Any(p => string.Equals(p.Scope, "User", StringComparison.OrdinalIgnoreCase));
                bool hasCompPending = pend.Any(p => string.Equals(p.Scope, "Computer", StringComparison.OrdinalIgnoreCase));
                if (hasCompPending) initialSection = AdmxPolicySection.Machine;
                else if (hasUserPending) initialSection = AdmxPolicySection.User;
                else
                {
                    try
                    {
                        var compState = PolicyProcessing.GetPolicyState(_compSource, policy);
                        bool compConfigured = compState == PolicyState.Enabled || compState == PolicyState.Disabled;
                        if (compConfigured) initialSection = AdmxPolicySection.Machine;
                        else
                        {
                            var userState = PolicyProcessing.GetPolicyState(_userSource, policy);
                            bool userConfigured = userState == PolicyState.Enabled || userState == PolicyState.Disabled;
                            initialSection = userConfigured ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                        }
                    }
                    catch { initialSection = AdmxPolicySection.Machine; }
                }
            }
            else
            {
                initialSection = policy.RawPolicy.Section == AdmxPolicySection.User ? AdmxPolicySection.User : AdmxPolicySection.Machine;
            }

            var compLoader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false);
            var userLoader = new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true);

            var win = new EditSettingWindow();
            win.Initialize(policy, initialSection, _bundle, _compSource, _userSource, compLoader, userLoader, null, null);
            // Overlay current (possibly unsaved) QuickEdit row values
            try { if (row != null) win.OverlayFromQuickEdit(row); } catch { }
            win.Saved += (_, __) => { try { RefreshRowFromSources(row); } catch { } };
            win.SavedDetail += (_, detail) =>
            {
                try
                {
                    if (row != null)
                    {
                        row.ApplyExternal(detail.Scope, detail.State, detail.Options);
                    }
                }
                catch { }
            };
            win.Closed += (_, __) => { _editWindows.Remove(policyId); };
            _editWindows[policyId] = win;
            win.Activate();
            try { WindowHelpers.BringToFront(win); } catch { }
        }

        private void Columns_PropertyChanged(object? s, PropertyChangedEventArgs e) => ScheduleSize();
        private void Pending_CollectionChanged(object? s, NotifyCollectionChangedEventArgs e) { try { DispatcherQueue.TryEnqueue(UpdateUnsavedIndicator); } catch (Exception ex) { Log.Warn("QuickEdit", "Dispatcher enqueue failed (Pending_CollectionChanged)", ex); } }

        private void ScheduleSize()
        {
            if (!_initialized || _pendingSize) return; _pendingSize = true;
            try
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                { _pendingSize = false; try { ApplyAutoSize(); } catch (Exception ex) { Log.Warn("QuickEdit", "ApplyAutoSize failed (enqueued)", ex); } });
            }
            catch (Exception ex) { _pendingSize = false; Log.Warn("QuickEdit", "ScheduleSize enqueue failed", ex); }
        }

        private void RefreshRowFromSources(QuickEditRow? row)
        {
            if (row == null || _compSource == null || _userSource == null) return;
            try
            {
                // Recreate a temp row to read fresh state then adopt silently
                var fresh = new QuickEditRow(row.Policy, _bundle!, _compSource, _userSource);
                row.AdoptState(fresh);
            }
            catch (Exception ex) { Log.Warn("QuickEdit", "RefreshRowFromSources failed", ex); }
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
                double baseSlack = 8 + (6 * 8);
                if (customScale < 0.9) baseSlack += 12;
                if (customScale < 0.8) baseSlack += 24;
                if (model > 0 && (model + baseSlack) < logicalW)
                    logicalW = model + baseSlack;
            }

            double targetClientW = logicalW * customScale * dpiScale + 2;
            double targetClientH = logicalH * customScale * dpiScale + 2;

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
                // Always allow Save (even if 0) as requested; it will no-op when there are no changes.
                _saveButton.IsEnabled = _bundle != null && !_isSaving;
            }
            catch { }
        }

        private async Task SaveAsync()
        {
            if (_bundle == null || _isSaving || _grid == null) return;
            var ids = _grid.Rows.Select(r => r.Policy.UniqueID).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            var relevant = PendingChangesService.Instance.Pending.Where(p => ids.Contains(p.PolicyId)).ToList();
            if (relevant.Count == 0) { SetStatus("No changes to save."); return; }
            _isSaving = true; SetStatus("Saving..."); SetSaving(true); if (_saveButton != null) _saveButton.IsEnabled = false;
            try
            {
                var (ok, error, _, _) = await SaveChangesCoordinator.SaveAsync(_bundle, relevant, _elevation, System.TimeSpan.FromSeconds(8), triggerRefresh: true);
                if (ok)
                {
                    PendingChangesService.Instance.Applied(relevant.ToArray());
                    try { SettingsService.Instance.SaveHistory(PendingChangesService.Instance.History.ToList()); } catch { }
                    SetStatus(relevant.Count == 1 ? "Saved 1 change." : $"Saved {relevant.Count} changes.");
                    // Force main window to reload sources which will raise PolicySourcesRefreshed and update our rows
                    try { (App.Window as MainWindow)?.RefreshLocalSources(); } catch { }
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
