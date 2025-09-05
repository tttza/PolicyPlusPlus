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

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class QuickEditWindow : Window
    {
        // Track child list editor windows so they can be closed when this window closes.
        private readonly List<ListEditorWindow> _childEditors = new();

        // For saving
        private AdmxBundle? _bundle;
        private IPolicySource? _compSource;
        private IPolicySource? _userSource;
        private readonly IElevationService _elevation = new ElevationServiceAdapter();
        private bool _isSaving;
        private bool _initialResizeDone;

        public QuickEditWindow()
        {
            InitializeComponent();
            try { SystemBackdrop = new MicaBackdrop(); } catch { }

            // Wire up dynamic references
            if (_grid != null) _grid.ParentQuickEditWindow = this;

            // Events
            if (_saveButton != null) _saveButton.Click += async (_, __) => await SaveAsync();
            if (_closeButton != null) _closeButton.Click += (_, __) => Close();

            // Keyboard accelerator (Ctrl+S)
            var saveAccel = new KeyboardAccelerator { Key = global::Windows.System.VirtualKey.S, Modifiers = global::Windows.System.VirtualKeyModifiers.Control };
            saveAccel.Invoked += async (a, b) => { if (_saveButton?.IsEnabled == true && !_isSaving) { await SaveAsync(); b.Handled = true; } };
            _root?.KeyboardAccelerators.Add(saveAccel);

            // Theme
            ApplyCurrentTheme();
            App.ThemeChanged += App_ThemeChanged;

            Closed += (s, e) =>
            {
                try
                {
                    App.ThemeChanged -= App_ThemeChanged;
                    PendingChangesService.Instance.Pending.CollectionChanged -= Pending_CollectionChanged;
                    foreach (var w in _childEditors.ToList()) { try { w.Close(); } catch { } }
                    _childEditors.Clear();
                    App.UnregisterWindow(this);
                }
                catch { }
            };

            try { App.RegisterWindow(this); } catch { }
        }

        private void App_ThemeChanged(object? sender, System.EventArgs e) => ApplyCurrentTheme();
        private void ApplyCurrentTheme()
        {
            try
            {
                if (_root == null) return;
                var theme = App.CurrentTheme;
                _root.RequestedTheme = theme;
                if (theme == ElementTheme.Light)
                {
                    _root.Background = new SolidColorBrush(Microsoft.UI.Colors.White);
                }
                else if (theme == ElementTheme.Dark)
                {
                    _root.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x20, 0x20));
                }
                else
                {
                    if (Application.Current.Resources.TryGetValue("WindowBackground", out var bg) && bg is Brush b)
                        _root.Background = b;
                }
            }
            catch { }
        }

        internal void RegisterChild(ListEditorWindow w)
        {
            _childEditors.Add(w);
            w.Closed += (s, e) => _childEditors.Remove(w);
        }

        public static IEnumerable<PolicyPlusPolicy> BuildSourcePolicies(IEnumerable<PolicyPlusPolicy> allVisible, IEnumerable<PolicyPlusPolicy> selected, IEnumerable<string> bookmarkIds, bool bookmarksOnly, int cap = 500)
        {
            var result = selected.Any()
                ? selected
                : (bookmarksOnly || bookmarkIds.Any())
                    ? allVisible.Where(p => bookmarkIds.Contains(p.UniqueID, System.StringComparer.OrdinalIgnoreCase))
                    : allVisible;
            return result.Distinct().OrderBy(p => p.DisplayName).Take(cap);
        }

        public void Initialize(AdmxBundle bundle, IPolicySource? comp, IPolicySource? user, IEnumerable<PolicyPlusPolicy> policies)
        {
            _bundle = bundle; _compSource = comp; _userSource = user;
            if (_grid != null)
            {
                _grid.Rows.Clear();
                foreach (var p in policies) _grid.Rows.Add(new QuickEditRow(p, bundle, comp, user));
            }
            if (_headerCount != null) _headerCount.Text = $"{_grid?.Rows.Count ?? 0} policies";

            try { PendingChangesService.Instance.Pending.CollectionChanged += Pending_CollectionChanged; } catch { }
            UpdateUnsavedIndicator();
            TryScheduleInitialResize();
        }

        private void TryScheduleInitialResize()
        {
            if (_initialResizeDone || _root == null) return;
            _initialResizeDone = true;
            try
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    try { WindowHelpers.ResizeClientWidthToContent(this, 60); } catch { }
                });
                var timer = DispatcherQueue.CreateTimer();
                timer.Interval = System.TimeSpan.FromMilliseconds(220);
                timer.IsRepeating = false;
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        WindowHelpers.ResizeClientWidthToContent(this, 60);
                        AdjustInitialHeight();
                    }
                    catch { }
                };
                timer.Start();
            }
            catch { }
        }

        private void AdjustInitialHeight()
        {
            try
            {
                if (_grid == null) return;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
                if (appWindow == null) return;
                int currentH = appWindow.ClientSize.Height;
                int rows = _grid.Rows.Count;
                int showRows = rows == 0 ? 4 : System.Math.Min(rows, 10);
                int target = 42 + (46 * showRows) + 40 + 32;
                if (target < 400) target = 400;
                if (target < currentH)
                {
                    appWindow.ResizeClient(new global::Windows.Graphics.SizeInt32(appWindow.ClientSize.Width, target));
                }
            }
            catch { }
        }

        private void Pending_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        { try { DispatcherQueue.TryEnqueue(UpdateUnsavedIndicator); } catch { } }

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
                else
                {
                    SetStatus(string.IsNullOrEmpty(error) ? "Save failed." : $"Save failed: {error}");
                }
            }
            finally
            {
                _isSaving = false; SetSaving(false); UpdateUnsavedIndicator();
            }
        }

        private void SetSaving(bool saving)
        { try { if (_savingOverlay != null) _savingOverlay.Visibility = saving ? Visibility.Visible : Visibility.Collapsed; } catch { } }

        private void SetStatus(string text)
        { if (_statusText != null) _statusText.Text = text; }
    }
}
