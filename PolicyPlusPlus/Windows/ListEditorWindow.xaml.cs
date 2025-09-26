using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq; // for duplicate detection
using Microsoft.UI.Input; // added for InputKeyboardSource
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using PolicyPlusPlus.Utils;
using Windows.System; // VirtualKey
using Windows.UI.Core; // CoreVirtualKeyStates

namespace PolicyPlusPlus.Windows
{
    public sealed partial class ListEditorWindow : Window
    {
        private static readonly Dictionary<string, ListEditorWindow> _openEditors = new();

        public static bool TryActivateExisting(string key)
        {
            if (_openEditors.TryGetValue(key, out var w))
            {
                WindowHelpers.BringToFront(w);
                w.Activate();
                var timer = w.DispatcherQueue.CreateTimer();
                timer.Interval = TimeSpan.FromMilliseconds(180);
                timer.IsRepeating = false;
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        WindowHelpers.BringToFront(w);
                        w.Activate();
                    }
                    catch (Exception ex)
                    {
                        Logging.Log.Debug(
                            "ListEditor",
                            "activate focus retry failed: " + ex.Message
                        );
                    }
                };
                timer.Start();
                return true;
            }
            return false;
        }

        public static void Register(string key, ListEditorWindow w)
        {
            _openEditors[key] = w;
            w.Closed += (s, e) =>
            {
                _openEditors.Remove(key);
            };
        }

        private bool _userProvidesNames;
        private ObservableCollection<Models.ListEditorNamedRow>? _namedRows;
        private ObservableCollection<Models.ListEditorValueRow>? _valueRows;
        public object? Result { get; private set; }
        public string? CountText { get; private set; }
        public event EventHandler<bool>? Finished; // true=OK

        public ListEditorWindow()
        {
            InitializeComponent();
            this.Title = "Edit list";

            // Centralized common window init
            ChildWindowCommon.Initialize(this, 560, 480, ApplyThemeResources);

            AddBtn.Click += (s, e) =>
            {
                AddNewRow(focus: true);
            };
            OkBtn.Click += Ok_Click;
            CancelBtn.Click += Cancel_Click;
        }

        private void Accel_Add(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            try
            {
                AddNewRow(focus: true);
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "Accel_Add failed: " + ex.Message);
            }
            args.Handled = true;
        }

        private void Accel_Ok(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            try
            {
                Ok_Click(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "Accel_Ok failed: " + ex.Message);
            }
            args.Handled = true;
        }

        private void Accel_Close(
            KeyboardAccelerator sender,
            KeyboardAcceleratorInvokedEventArgs args
        )
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "Accel_Close failed: " + ex.Message);
            }
            args.Handled = true;
        }

        private void ApplyThemeResources()
        {
            try
            {
                if (Content is FrameworkElement fe)
                    fe.RequestedTheme = App.CurrentTheme;
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "ApplyThemeResources failed: " + ex.Message);
            }
        }

        public void BringToFront() => WindowHelpers.BringToFront(this);

        public void Initialize(string label, bool userProvidesNames, object? initial)
        {
            HeaderText.Text = label;
            _userProvidesNames = userProvidesNames;
            ListItems.ItemsSource = null;
            bool any = false;
            if (userProvidesNames)
            {
                _namedRows = new ObservableCollection<Models.ListEditorNamedRow>();
                if (initial is List<KeyValuePair<string, string>> kvp)
                {
                    foreach (var p in kvp)
                    {
                        AppendRow(
                            new Models.ListEditorNamedRow
                            {
                                Key = p.Key,
                                Value = p.Value,
                                IsPlaceholder = false,
                            }
                        );
                        any = true;
                    }
                }
                else if (initial is Dictionary<string, string> dict)
                {
                    foreach (var kv in dict)
                    {
                        AppendRow(
                            new Models.ListEditorNamedRow
                            {
                                Key = kv.Key,
                                Value = kv.Value,
                                IsPlaceholder = false,
                            }
                        );
                        any = true;
                    }
                }
                else if (initial is IEnumerable<KeyValuePair<string, string>> en)
                {
                    foreach (var kv in en)
                    {
                        AppendRow(
                            new Models.ListEditorNamedRow
                            {
                                Key = kv.Key,
                                Value = kv.Value,
                                IsPlaceholder = false,
                            }
                        );
                        any = true;
                    }
                }
            }
            else if (initial is List<string> list)
            {
                _valueRows = new ObservableCollection<Models.ListEditorValueRow>();
                foreach (var s in list)
                {
                    AppendRow(new Models.ListEditorValueRow { Value = s, IsPlaceholder = false });
                    any = true;
                }
            }
            if (!any)
            {
                if (_userProvidesNames)
                    _namedRows = new ObservableCollection<Models.ListEditorNamedRow>();
                else
                    _valueRows = new ObservableCollection<Models.ListEditorValueRow>();
            }

            // Bind ItemsSource and template
            if (_userProvidesNames)
            {
                ListItems.ItemTemplate = (DataTemplate)RootShell.Resources["KeyValueTemplate"];
                ListItems.ItemsSource = _namedRows;
            }
            else
            {
                ListItems.ItemTemplate = (DataTemplate)RootShell.Resources["ValueOnlyTemplate"];
                ListItems.ItemsSource = _valueRows;
            }

            EnsureTrailingPlaceholder();
        }

        private void EnsureTrailingPlaceholder()
        {
            try
            {
                if (_userProvidesNames)
                {
                    if (_namedRows == null)
                        return;
                    // Remove extra placeholders except last
                    for (int i = _namedRows.Count - 2; i >= 0; i--)
                    {
                        if (_namedRows[i].IsPlaceholder)
                            _namedRows.RemoveAt(i);
                    }
                    if (_namedRows.Count == 0 || !_namedRows[^1].IsPlaceholder)
                    {
                        AppendRow(new Models.ListEditorNamedRow { IsPlaceholder = true });
                    }
                }
                else
                {
                    if (_valueRows == null)
                        return;
                    for (int i = _valueRows.Count - 2; i >= 0; i--)
                    {
                        if (_valueRows[i].IsPlaceholder)
                            _valueRows.RemoveAt(i);
                    }
                    if (_valueRows.Count == 0 || !_valueRows[^1].IsPlaceholder)
                    {
                        AppendRow(new Models.ListEditorValueRow { IsPlaceholder = true });
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "EnsureTrailingPlaceholder failed: " + ex.Message);
            }
        }

        private void AddNewRow(bool focus)
        {
            if (_userProvidesNames)
            {
                var row = new Models.ListEditorNamedRow
                {
                    Key = string.Empty,
                    Value = string.Empty,
                    IsPlaceholder = false,
                };
                AppendRow(row, insertBeforePlaceholder: true);
                if (focus)
                    TryFocusRow(row, preferKey: true);
            }
            else
            {
                var row = new Models.ListEditorValueRow
                {
                    Value = string.Empty,
                    IsPlaceholder = false,
                };
                AppendRow(row, insertBeforePlaceholder: true);
                if (focus)
                    TryFocusRow(row, preferKey: false);
            }
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                var shiftDown =
                    (
                        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                        & CoreVirtualKeyStates.Down
                    ) == CoreVirtualKeyStates.Down;
                if (!shiftDown && sender is TextBox tb)
                {
                    if (_userProvidesNames && (tb.Name == "ValTb"))
                    {
                        MoveToNextRowAndFocusKey(ref e);
                    }
                    else if (!_userProvidesNames)
                    {
                        MoveToNextRowAndFocusValue(ref e);
                    }
                }
            }
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is object ctx)
            {
                if (_userProvidesNames)
                {
                    if (ctx is Models.ListEditorNamedRow nr)
                    {
                        if (nr.IsPlaceholder)
                            return;
                        _namedRows?.Remove(nr);
                    }
                }
                else
                {
                    if (ctx is Models.ListEditorValueRow vr)
                    {
                        if (vr.IsPlaceholder)
                            return;
                        _valueRows?.Remove(vr);
                    }
                }
                EnsureTrailingPlaceholder();
            }
        }

        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (_userProvidesNames)
            {
                var list = new List<KeyValuePair<string, string>>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_namedRows != null)
                {
                    foreach (var row in _namedRows)
                    {
                        if (row.IsPlaceholder)
                            continue;
                        var key = row.Key?.Trim() ?? string.Empty;
                        var val = row.Value?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(val))
                            continue;
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            await ShowValidationDialog("A key is required for all non-empty rows.");
                            return;
                        }
                        if (!seen.Add(key))
                        {
                            await ShowValidationDialog(
                                $"Multiple entries are named \"{key}\". Remove or rename duplicates."
                            );
                            return;
                        }
                        list.Add(new KeyValuePair<string, string>(key, val));
                    }
                }
                Result = list;
                CountText = $"Edit... ({list.Count})";
            }
            else
            {
                var list = new List<string>();
                if (_valueRows != null)
                {
                    foreach (var row in _valueRows)
                    {
                        if (row.IsPlaceholder)
                            continue;
                        var s = row.Value ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(s))
                            list.Add(s);
                    }
                }
                Result = list;
                CountText = $"Edit... ({list.Count})";
            }
            Finished?.Invoke(this, true);
            this.Close();
        }

        private async System.Threading.Tasks.Task ShowValidationDialog(string message)
        {
            try
            {
                var dlg = new ContentDialog
                {
                    Title = "Validation",
                    Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = "OK",
                    XamlRoot = (Content as FrameworkElement)?.XamlRoot,
                };
                await dlg.ShowAsync();
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "ShowValidationDialog failed: " + ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AppendRow(Models.ListEditorNamedRow row, bool insertBeforePlaceholder = false)
        {
            if (_namedRows == null)
                _namedRows = new ObservableCollection<Models.ListEditorNamedRow>();
            if (insertBeforePlaceholder && _namedRows.Count > 0 && _namedRows[^1].IsPlaceholder)
                _namedRows.Insert(_namedRows.Count - 1, row);
            else
                _namedRows.Add(row);
            row.PropertyChanged += Row_PropertyChanged;
        }

        private void AppendRow(Models.ListEditorValueRow row, bool insertBeforePlaceholder = false)
        {
            if (_valueRows == null)
                _valueRows = new ObservableCollection<Models.ListEditorValueRow>();
            if (insertBeforePlaceholder && _valueRows.Count > 0 && _valueRows[^1].IsPlaceholder)
                _valueRows.Insert(_valueRows.Count - 1, row);
            else
                _valueRows.Add(row);
            row.PropertyChanged += Row_PropertyChanged;
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When last row gets content, ensure trailing placeholder exists
            try
            {
                if (sender is Models.ListEditorNamedRow nr)
                {
                    if (
                        nr.IsPlaceholder
                        && (
                            !string.IsNullOrWhiteSpace(nr.Key)
                            || !string.IsNullOrWhiteSpace(nr.Value)
                        )
                    )
                    {
                        nr.IsPlaceholder = false;
                    }
                }
                else if (sender is Models.ListEditorValueRow vr)
                {
                    if (vr.IsPlaceholder && !string.IsNullOrWhiteSpace(vr.Value))
                    {
                        vr.IsPlaceholder = false;
                    }
                }
                EnsureTrailingPlaceholder();
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "Row_PropertyChanged failed: " + ex.Message);
            }
        }

        private void TryFocusRow(object row, bool preferKey)
        {
            try
            {
                ListItems.ScrollIntoView(row);
                var timer = DispatcherQueue.CreateTimer();
                timer.Interval = TimeSpan.FromMilliseconds(1);
                timer.IsRepeating = false;
                timer.Tick += (s, e) =>
                {
                    var container = ListItems.ContainerFromItem(row) as ListViewItem;
                    if (container == null)
                        return;
                    var root = container.ContentTemplateRoot as FrameworkElement;
                    if (root == null)
                        return;
                    var tb = FindDescendantByName<TextBox>(root, preferKey ? "KeyTb" : "ValueTb");
                    tb?.Focus(FocusState.Programmatic);
                    tb?.SelectAll();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Logging.Log.Debug("ListEditor", "TryFocusRow failed: " + ex.Message);
            }
        }

        private void MoveToNextRowAndFocusKey(ref KeyRoutedEventArgs e)
        {
            if (_namedRows == null)
                return;
            // If at last non-placeholder row, ensure placeholder exists
            EnsureTrailingPlaceholder();
            var focused = FocusManager.GetFocusedElement() as FrameworkElement;
            var item = (focused as FrameworkElement)?.DataContext;
            if (item is Models.ListEditorNamedRow nr)
            {
                int index = _namedRows.IndexOf(nr);
                int nextIndex = Math.Min(index + 1, _namedRows.Count - 1);
                if (nextIndex > index)
                {
                    var next = _namedRows[nextIndex];
                    e.Handled = true;
                    TryFocusRow(next, preferKey: true);
                }
            }
        }

        private void MoveToNextRowAndFocusValue(ref KeyRoutedEventArgs e)
        {
            if (_valueRows == null)
                return;
            EnsureTrailingPlaceholder();
            var focused = FocusManager.GetFocusedElement() as FrameworkElement;
            var item = (focused as FrameworkElement)?.DataContext;
            if (item is Models.ListEditorValueRow vr)
            {
                int index = _valueRows.IndexOf(vr);
                int nextIndex = Math.Min(index + 1, _valueRows.Count - 1);
                if (nextIndex > index)
                {
                    var next = _valueRows[nextIndex];
                    e.Handled = true;
                    TryFocusRow(next, preferKey: false);
                }
            }
        }

        private static TElement? FindDescendantByName<TElement>(DependencyObject root, string name)
            where TElement : FrameworkElement
        {
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
                if (child is TElement fe && string.Equals(fe.Name, name, StringComparison.Ordinal))
                    return fe;
                var found = FindDescendantByName<TElement>(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}

namespace PolicyPlusPlus.Windows.Models
{
    using System.Runtime.CompilerServices;

    internal abstract class ListEditorRowBase : INotifyPropertyChanged
    {
        private bool _isPlaceholder;
        public bool IsPlaceholder
        {
            get => _isPlaceholder;
            set
            {
                if (_isPlaceholder != value)
                {
                    _isPlaceholder = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanRemove));
                }
            }
        }

        public bool CanRemove => !IsPlaceholder;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal sealed class ListEditorNamedRow : ListEditorRowBase
    {
        private string _key = string.Empty;
        public string Key
        {
            get => _key;
            set
            {
                if (!string.Equals(_key, value, StringComparison.Ordinal))
                {
                    _key = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                if (!string.Equals(_value, value, StringComparison.Ordinal))
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    internal sealed class ListEditorValueRow : ListEditorRowBase
    {
        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                if (!string.Equals(_value, value, StringComparison.Ordinal))
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
