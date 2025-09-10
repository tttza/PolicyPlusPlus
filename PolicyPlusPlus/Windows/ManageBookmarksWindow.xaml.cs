using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Utils;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class ManageBookmarksWindow : Window
    {
        private bool _suppressSelect;

        public ManageBookmarksWindow()
        {
            InitializeComponent();
            Title = "Manage Bookmark Lists";
            try { SystemBackdrop = new MicaBackdrop(); } catch { }
            ChildWindowCommon.Initialize(this, 460, 420, ApplyTheme);
            try { if (RootShell != null) RootShell.Loaded += OnLoaded; else DispatcherQueue.TryEnqueue(() => OnLoaded(this, new RoutedEventArgs())); } catch { }
            Closed += (_, __) =>
            {
                try { BookmarkService.Instance.ActiveListChanged -= Instance_ActiveListChanged; } catch { }
                try { BookmarkService.Instance.Changed -= Instance_ActiveListChanged; } catch { }
            };

            AddBtn.Click += (_, __) => AddList();
            RenameBtn.Click += (_, __) => RenameSelected();
            CloseBtn.Click += (_, __) => Close();
            ListNames.SelectionChanged += (_, __) => { if (_suppressSelect) return; UpdateButtons(); };
            ExportBtn.Click += (_, __) => ExportJson();
            ImportBtn.Click += async (_, __) => await ImportJsonAsync();
            ListNames.ContainerContentChanging += ListNames_ContainerContentChanging;
            ListNames.DoubleTapped += ListNames_DoubleTapped; // double-click prepare rename
            NameBox.TextChanged += (_, __) => UpdateButtons();
        }

        private void ListNames_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                if (ListNames.SelectedItem is string name)
                {
                    NameBox.Text = name;
                    NameBox.Focus(FocusState.Programmatic);
                    NameBox.SelectAll();
                    SetStatus("Edit name then press Rename.");
                }
            }
            catch { }
        }

        private void ListNames_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            try
            {
                if (args.Item is string name)
                {
                    var root = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
                    var icon = root?.FindName("ActiveIcon") as IconElement;
                    if (icon != null)
                    {
                        bool isActive = string.Equals(name, BookmarkService.Instance.ActiveList, StringComparison.OrdinalIgnoreCase);
                        icon.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            catch { }
        }

        private void ApplyTheme()
        {
            try
            {
                if (RootShell == null) return;
                var theme = App.CurrentTheme;
                RootShell.RequestedTheme = theme;
                if (Application.Current.Resources.TryGetValue("WindowBackground", out var bg) && bg is Brush b)
                { RootShell.Background = b; try { (ScaleHost as Grid)!.Background = b; } catch { } }
            }
            catch { }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try { BookmarkService.Instance.ActiveListChanged += Instance_ActiveListChanged; } catch { }
            try { BookmarkService.Instance.Changed += Instance_ActiveListChanged; } catch { }
            Refresh();
        }

        private void Instance_ActiveListChanged(object? sender, EventArgs e)
        { try { Refresh(); } catch { } }

        private void UpdateRowButtons()
        {
            try
            {
                var active = BookmarkService.Instance.ActiveList;
                var total = BookmarkService.Instance.ListNames.Count;
                foreach (var item in ListNames.Items)
                {
                    var container = ListNames.ContainerFromItem(item) as ListViewItem; if (container == null) continue;
                    var root = container.ContentTemplateRoot as FrameworkElement; if (root == null) continue;
                    var actBtn = root.FindName("RowActivateBtn") as Button;
                    var delBtn = root.FindName("RowDeleteBtn") as Button;
                    // Ensure glyph content if not set
                    if (actBtn != null && actBtn.Content == null) actBtn.Content = new FontIcon { Glyph = "\uE8FB" };
                    if (delBtn != null && delBtn.Content == null) delBtn.Content = new FontIcon { Glyph = "\uE74D" };
                    if (actBtn != null)
                    {
                        bool isActive = string.Equals(item as string, active, StringComparison.OrdinalIgnoreCase);
                        actBtn.IsEnabled = !isActive;
                        actBtn.Opacity = isActive ? 0.35 : 1.0;
                    }
                    if (delBtn != null)
                    {
                        // Disable delete only when this would remove the last remaining list
                        bool disable = total <= 1;
                        delBtn.IsEnabled = !disable;
                        delBtn.Opacity = disable ? 0.35 : 1.0;
                    }
                }
            }
            catch { }
        }

        private void UpdateAllActiveIcons()
        {
            try
            {
                var active = BookmarkService.Instance.ActiveList;
                foreach (var item in ListNames.Items)
                {
                    var container = ListNames.ContainerFromItem(item) as ListViewItem;
                    if (container == null) continue;
                    var root = container.ContentTemplateRoot as FrameworkElement;
                    var icon = root?.FindName("ActiveIcon") as IconElement;
                    if (icon != null)
                    {
                        bool isActive = string.Equals(item as string, active, StringComparison.OrdinalIgnoreCase);
                        icon.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                UpdateRowButtons();
                if (ListNames.Items.Count > 0 && ListNames.Items.OfType<object>().Any(i => ListNames.ContainerFromItem(i) == null))
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => { try { UpdateAllActiveIcons(); } catch { } });
                }
            }
            catch { }
        }

        private void Refresh()
        {
            try
            {
                var active = BookmarkService.Instance.ActiveList;
                var names = BookmarkService.Instance.ListNames
                    .OrderBy(n => string.Equals(n, "default", StringComparison.OrdinalIgnoreCase) ? "" : n)
                    .ThenBy(n => n)
                    .ToList();
                _suppressSelect = true;
                ListNames.ItemsSource = names;
                ListNames.SelectedItem = names.FirstOrDefault(n => string.Equals(n, active, StringComparison.OrdinalIgnoreCase));
                _suppressSelect = false;
                ActiveLabel.Text = $"Active: {active}";
                UpdateButtons();
                DispatcherQueue.TryEnqueue(UpdateAllActiveIcons);
            }
            catch { }
        }

        private string? SelectedName => ListNames.SelectedItem as string;

        private void UpdateButtons()
        {
            try
            {
                var sel = SelectedName;
                bool isDefault = string.Equals(sel, "default", StringComparison.OrdinalIgnoreCase);
                bool hasText = !string.IsNullOrWhiteSpace(NameBox.Text);
                AddBtn.IsEnabled = hasText; // Add needs text; always allowed
                RenameBtn.IsEnabled = sel != null && !isDefault && hasText;
            }
            catch { }
        }

        private void AddList()
        {
            try
            {
                var name = (NameBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name)) { SetStatus("Enter a name then press Add."); return; }
                BookmarkService.Instance.AddList(name);
                NameBox.Text = string.Empty;
                Refresh();
                SetStatus("List added and activated.");
            }
            catch { SetStatus("Add failed."); }
        }

        private void RenameSelected()
        {
            try
            {
                var old = SelectedName; if (old == null) { SetStatus("Select a list to rename."); return; }
                var name = (NameBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name)) { SetStatus("Type new name."); return; }
                if (BookmarkService.Instance.RenameList(old, name)) { NameBox.Text = string.Empty; Refresh(); SetStatus("Renamed."); }
                else SetStatus("Rename failed (default or duplicate).");
            }
            catch { SetStatus("Rename failed."); }
        }

        private void ExportJson()
        {
            try
            {
                var json = BookmarkService.Instance.ExportJson();
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(json); Clipboard.SetContent(dp);
                SetStatus("Copied JSON to clipboard.");
            }
            catch { SetStatus("Export failed."); }
        }

        private async System.Threading.Tasks.Task ImportJsonAsync()
        {
            try
            {
                var dlg = new ContentDialog
                {
                    XamlRoot = (Content as FrameworkElement)?.XamlRoot,
                    Title = "Import Bookmark JSON",
                    PrimaryButtonText = "Import",
                    CloseButtonText = "Cancel"
                };
                var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinWidth = 320, MinHeight = 180, PlaceholderText = "Paste JSON here" };
                dlg.Content = box;
                var res = await dlg.ShowAsync();
                if (res == ContentDialogResult.Primary)
                {
                    var json = box.Text;
                    if (BookmarkService.Instance.TryImportJson(json, out var err)) { SetStatus("Imported."); Refresh(); }
                    else SetStatus(err ?? "Import failed.");
                }
            }
            catch { SetStatus("Import failed."); }
        }

        private void SetStatus(string text) { try { StatusText.Text = text; } catch { } }

        private async void RowDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string name)
                {
                    // If would become zero lists after delete disallow
                    if (BookmarkService.Instance.ListNames.Count <= 1) { SetStatus("Cannot delete the last list."); return; }
                    if (Content is FrameworkElement root)
                    {
                        var cd = new ContentDialog
                        {
                            XamlRoot = root.XamlRoot,
                            Title = "Delete list?",
                            Content = new TextBlock { Text = $"Delete bookmark list '{name}'? This cannot be undone.", TextWrapping = TextWrapping.Wrap },
                            PrimaryButtonText = "Delete",
                            CloseButtonText = "Cancel",
                            DefaultButton = ContentDialogButton.Close
                        };
                        var res = await cd.ShowAsync();
                        if (res != ContentDialogResult.Primary) return;
                    }
                    BookmarkService.Instance.RemoveList(name);
                    Refresh();
                    SetStatus("Deleted.");
                }
            }
            catch { SetStatus("Delete failed."); }
        }

        private void RowActivate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string name)
                { BookmarkService.Instance.SetActive(name); SetStatus("Activated."); }
            }
            catch { }
        }
    }
}
