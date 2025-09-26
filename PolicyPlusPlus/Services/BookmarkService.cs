using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PolicyPlusPlus.Logging; // logging

namespace PolicyPlusPlus.Services
{
    public sealed class BookmarkService
    {
        public static BookmarkService Instance { get; } = new BookmarkService();

        private readonly object _gate = new();
        private Dictionary<string, List<string>> _lists = new(StringComparer.OrdinalIgnoreCase);
        private string _active = "default";
        public event EventHandler? Changed;
        public event EventHandler? ActiveListChanged;

        private BookmarkService()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                var (lists, active) = SettingsService.Instance.LoadBookmarkListsWithActive();
                _lists = new Dictionary<string, List<string>>(
                    lists,
                    StringComparer.OrdinalIgnoreCase
                );
                _active = active;
            }
            catch
            {
                _lists = new(StringComparer.OrdinalIgnoreCase) { ["default"] = new List<string>() };
                _active = "default";
                Log.Warn("Bookmarks", "Load failed - initialized empty lists");
            }
        }

        private void Persist()
        {
            try
            {
                SettingsService.Instance.SaveBookmarkLists(_lists, _active);
            }
            catch (Exception ex)
            {
                Log.Warn("Bookmarks", "Persist failed", ex);
            }
        }

        public string ActiveList => _active;
        public IReadOnlyCollection<string> ListNames
        {
            get
            {
                lock (_gate)
                {
                    return _lists.Keys.ToList(); // snapshot to avoid enumeration during mutation
                }
            }
        }
        public IReadOnlyCollection<string> ActiveIds
        {
            get
            {
                lock (_gate)
                {
                    return _lists.TryGetValue(_active, out var l)
                        ? l.ToArray() // snapshot; underlying list may mutate
                        : Array.Empty<string>();
                }
            }
        }

        public bool IsBookmarked(string policyId)
        {
            if (string.IsNullOrEmpty(policyId))
                return false;
            lock (_gate)
            {
                if (!_lists.TryGetValue(_active, out var l))
                    return false;
                // Manual loop under lock to avoid "Collection was modified" during LINQ enumeration.
                for (int i = 0; i < l.Count; i++)
                {
                    if (string.Equals(l[i], policyId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }

        public void Toggle(string policyId)
        {
            if (string.IsNullOrEmpty(policyId))
                return;
            lock (_gate)
            {
                if (!_lists.TryGetValue(_active, out var l))
                {
                    l = new List<string>();
                    _lists[_active] = l;
                }
                int idx = l.FindIndex(x =>
                    string.Equals(x, policyId, StringComparison.OrdinalIgnoreCase)
                );
                if (idx >= 0)
                    l.RemoveAt(idx);
                else
                    l.Add(policyId);
                Persist();
            }
            RaiseChanged();
        }

        public void AddList(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            lock (_gate)
            {
                if (!_lists.ContainsKey(name))
                    _lists[name] = new List<string>();
                _active = name;
                Persist();
            }
            ActiveListChanged?.Invoke(this, EventArgs.Empty);
            RaiseChanged();
        }

        public bool RenameList(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return false;
            lock (_gate)
            {
                if (!_lists.TryGetValue(oldName, out var items))
                    return false;
                if (_lists.ContainsKey(newName))
                    return false;
                _lists.Remove(oldName);
                _lists[newName] = items;
                if (string.Equals(_active, oldName, StringComparison.OrdinalIgnoreCase))
                    _active = newName;
                Persist();
            }
            ActiveListChanged?.Invoke(this, EventArgs.Empty);
            RaiseChanged();
            return true;
        }

        public void RemoveList(string name)
        {
            lock (_gate)
            {
                if (_lists.Count <= 1)
                    return; // keep at least one list in existence
                if (
                    _lists.Remove(name)
                    && string.Equals(_active, name, StringComparison.OrdinalIgnoreCase)
                )
                {
                    _active = _lists.Keys.FirstOrDefault() ?? "default";
                    if (!_lists.ContainsKey(_active))
                        _lists[_active] = new List<string>();
                }
                Persist();
            }
            ActiveListChanged?.Invoke(this, EventArgs.Empty);
            RaiseChanged();
        }

        public void SetActive(string name)
        {
            lock (_gate)
            {
                if (!_lists.ContainsKey(name))
                    return;
                if (string.Equals(_active, name, StringComparison.OrdinalIgnoreCase))
                    return;
                _active = name;
                Persist();
            }
            ActiveListChanged?.Invoke(this, EventArgs.Empty);
            RaiseChanged();
        }

        public (Dictionary<string, List<string>> lists, string active) GetAll()
        {
            lock (_gate)
            {
                var copy = _lists.ToDictionary(
                    k => k.Key,
                    v => v.Value.ToList(),
                    StringComparer.OrdinalIgnoreCase
                );
                return (copy, _active);
            }
        }

        public void ReplaceAll(Dictionary<string, List<string>> lists, string active)
        {
            lock (_gate)
            {
                if (lists.Count == 0)
                    lists["default"] = new List<string>();
                _lists = new Dictionary<string, List<string>>(
                    lists,
                    StringComparer.OrdinalIgnoreCase
                );
                _active = _lists.ContainsKey(active) ? active : _lists.Keys.First();
                Persist();
            }
            ActiveListChanged?.Invoke(this, EventArgs.Empty);
            RaiseChanged();
        }

        public string ExportJson()
        {
            lock (_gate)
            {
                var obj = new { active = _active, lists = _lists };
                return JsonSerializer.Serialize(
                    obj,
                    new JsonSerializerOptions { WriteIndented = true }
                );
            }
        }

        public bool TryImportJson(string json, out string? error)
        {
            try
            {
                var doc = JsonSerializer.Deserialize<JsonElement>(json);
                if (!doc.TryGetProperty("lists", out var listsElem))
                {
                    error = "Missing 'lists'.";
                    return false;
                }
                var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in listsElem.EnumerateObject())
                {
                    var arr =
                        prop.Value.ValueKind == JsonValueKind.Array
                            ? prop
                                .Value.EnumerateArray()
                                .Select(e => e.GetString() ?? string.Empty)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList()
                            : new List<string>();
                    dict[prop.Name] = arr;
                }
                string active = doc.TryGetProperty("active", out var activeElem)
                    ? (activeElem.GetString() ?? "default")
                    : "default";
                ReplaceAll(dict, active);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Warn("Bookmarks", "TryImportJson failed: " + ex.Message, ex);
                return false;
            }
        }

        private void RaiseChanged()
        {
            try
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Debug("Bookmarks", "Changed handlers failed: " + ex.Message);
            }
        }
    }
}
