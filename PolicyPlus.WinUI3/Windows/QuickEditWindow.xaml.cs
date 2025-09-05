using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.IO;
using PolicyPlus.WinUI3.ViewModels;
using PolicyPlus.WinUI3.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Color = Windows.UI.Color;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed class QuickEditWindow : Window
    {
        private readonly QuickEditGridControl _grid = new();
        private TextBlock _headerCount = null!;
        private TextBlock _unsavedText = null!;
        private Button _saveButton = null!;
        private TextBlock _statusText = null!;
        private Grid _savingOverlay = null!;
        private Grid _root = null!; // keep reference for theme switching
        // Track child list editor windows so they can be closed when this window closes.
        private readonly List<ListEditorWindow> _childEditors = new();

        // For saving
        private AdmxBundle? _bundle;
        private IPolicySource? _compSource;
        private IPolicySource? _userSource;
        private readonly IElevationService _elevation = new ElevationServiceAdapter();
        private bool _isSaving;

        public QuickEditWindow()
        {
            Title = "Quick Edit";

            // Try to match other windows (Mica + dynamic theme)
            try { SystemBackdrop = new MicaBackdrop(); } catch { }

            var root = new Grid { Padding = new Thickness(12), MinWidth = 1280, MinHeight = 650 };
            _root = root;
            // Let Mica show through; if you want solid use WindowBackground like main window
            try { root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); } catch { }

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            headerPanel.Children.Add(new TextBlock { Text = "Quick Edit", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            _headerCount = new TextBlock { Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
            headerPanel.Children.Add(_headerCount);
            _unsavedText = new TextBlock { Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,0,0) };
            headerPanel.Children.Add(_unsavedText);
            root.Children.Add(headerPanel);

            _grid.ParentQuickEditWindow = this;
            Grid.SetRow(_grid, 1);
            root.Children.Add(_grid);

            var buttonsGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.75 };
            Grid.SetColumn(_statusText, 0);
            buttonsGrid.Children.Add(_statusText);

            _saveButton = new Button { Content = "Save", IsEnabled = false, Margin = new Thickness(0,0,8,0) };
            _saveButton.Click += async (_, __) => await SaveAsync();
            Grid.SetColumn(_saveButton, 1);
            buttonsGrid.Children.Add(_saveButton);

            var close = new Button { Content = "Close" }; close.Click += (_, __) => Close();
            Grid.SetColumn(close, 2);
            buttonsGrid.Children.Add(close);

            Grid.SetRow(buttonsGrid, 2);
            root.Children.Add(buttonsGrid);

            // Saving overlay
            _savingOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                Visibility = Visibility.Collapsed
            };
            var overlayStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8 };
            var ring = new ProgressRing { IsActive = true, Width = 48, Height = 48 };
            overlayStack.Children.Add(ring);
            overlayStack.Children.Add(new TextBlock { Text = "Saving...", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Center });
            _savingOverlay.Children.Add(overlayStack);
            Grid.SetRowSpan(_savingOverlay, 3);
            root.Children.Add(_savingOverlay);

            Content = root;

            // Register as secondary window so global theme & icon apply
            try { App.RegisterWindow(this); } catch { }
            ApplyCurrentTheme();
            App.ThemeChanged += App_ThemeChanged;

            this.Closed += (s, e) =>
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

            var saveAccel = new KeyboardAccelerator { Key = global::Windows.System.VirtualKey.S, Modifiers = global::Windows.System.VirtualKeyModifiers.Control };
            saveAccel.Invoked += async (a, b) => { if (_saveButton.IsEnabled && !_isSaving) { await SaveAsync(); b.Handled = true; } };
            root.KeyboardAccelerators.Add(saveAccel);
        }

        private void App_ThemeChanged(object? sender, System.EventArgs e) => ApplyCurrentTheme();
        private void ApplyCurrentTheme()
        {
            try
            {
                if (_root == null) return;
                var theme = App.CurrentTheme;
                _root.RequestedTheme = theme;
                // Force explicit light/dark background so OS high-contrast or system dark does not override app preference
                if (theme == ElementTheme.Light)
                {
                    _root.Background = new SolidColorBrush(Microsoft.UI.Colors.White);
                }
                else if (theme == ElementTheme.Dark)
                {
                    _root.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x20, 0x20));
                }
                else // System -> fallback to resource (may be dark depending on OS)
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
            _grid.Rows.Clear();
            foreach (var p in policies) _grid.Rows.Add(new QuickEditRow(p, bundle, comp, user));
            _headerCount.Text = $"{_grid.Rows.Count} policies";

            try { PendingChangesService.Instance.Pending.CollectionChanged += Pending_CollectionChanged; } catch { }
            UpdateUnsavedIndicator();
        }

        private void Pending_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        { try { DispatcherQueue.TryEnqueue(UpdateUnsavedIndicator); } catch { } }

        private void UpdateUnsavedIndicator()
        {
            try
            {
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
            if (_bundle == null || _isSaving) return;
            var ids = _grid.Rows.Select(r => r.Policy.UniqueID).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            var relevant = PendingChangesService.Instance.Pending.Where(p => ids.Contains(p.PolicyId)).ToList();
            if (relevant.Count == 0) return;
            _isSaving = true; SetStatus("Saving..."); SetSaving(true); _saveButton.IsEnabled = false;

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
