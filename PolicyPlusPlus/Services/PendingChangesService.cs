using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PolicyPlusCore.Core;
using PolicyPlusPlus.Logging; // logging

namespace PolicyPlusPlus.Services
{
    public class PendingChange
    {
        public string PolicyId { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public string Scope { get; set; } = "Both"; // Computer/User/Both
        public string Action { get; set; } = "Set"; // UI label: Set/Delete/Enable/Disable/Clear
        public string Details { get; set; } = string.Empty; // short, human-friendly summary
        public string DetailsFull { get; set; } = string.Empty; // expanded, multiline tooltip/preview
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public PolicyState DesiredState { get; set; } = PolicyState.NotConfigured;
        public Dictionary<string, object>? Options { get; set; }
    }

    public class HistoryRecord
    {
        public string PolicyId { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty; // Applied / Reapplied
        public string Details { get; set; } = string.Empty;
        public string DetailsFull { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; } = DateTime.Now;
        public PolicyState DesiredState { get; set; } = PolicyState.NotConfigured;
        public Dictionary<string, object>? Options { get; set; }
    }

    public sealed class PendingChangesService
    {
        // Global shared instance used in normal application runtime.
        private static readonly PendingChangesService _sharedInstance = new();

        // Async-local instance for test isolation when enabled.
        private static readonly System.Threading.AsyncLocal<PendingChangesService?> _ambient =
            new();
        private static volatile bool _testIsolationEnabled;

        // Returns either the shared singleton (default) or an AsyncLocal-scoped instance when test isolation is enabled.
        public static PendingChangesService Instance
        {
            get
            {
                if (_testIsolationEnabled)
                {
                    return _ambient.Value ??= new PendingChangesService();
                }
                return _sharedInstance;
            }
        }

        // Enables per-test isolation; safe to call multiple times (idempotent).
        public static void EnableTestIsolation() => _testIsolationEnabled = true;

        // Clears the current ambient instance so the next access creates a fresh isolated instance.
        public static void ResetAmbientForTest() => _ambient.Value = null;

        public ObservableCollection<PendingChange> Pending { get; } = new();
        public ObservableCollection<HistoryRecord> History { get; } = new();

        private const int MaxHistoryEntries = 100; // cap

        public event EventHandler? DirtyChanged;
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (value == _isDirty)
                    return;
                _isDirty = value;
                DirtyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private PendingChangesService() { }

        private void UpdateDirtyFlag() => IsDirty = Pending.Count > 0;

        private static Dictionary<string, object>? CloneOptions(Dictionary<string, object>? src)
        {
            if (src == null)
                return null;
            var clone = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src)
            {
                var v = kv.Value;
                if (v is null)
                    clone[kv.Key] = string.Empty;
                else if (v is string s)
                    clone[kv.Key] = s;
                else if (v is bool || v is int || v is long || v is uint || v is double)
                    clone[kv.Key] = v;
                else if (v is string[] arr)
                    clone[kv.Key] = arr.ToArray();
                else if (v is IEnumerable<string> strEnum)
                    clone[kv.Key] = strEnum.ToList();
                else if (v is List<string> strList)
                    clone[kv.Key] = new List<string>(strList);
                else if (v is IEnumerable<KeyValuePair<string, string>> kvps)
                    clone[kv.Key] = kvps.Select(p => new KeyValuePair<string, string>(
                            p.Key,
                            p.Value
                        ))
                        .ToList();
                else
                    clone[kv.Key] = v.ToString() ?? string.Empty;
            }
            return clone;
        }

        public void Add(PendingChange change)
        {
            if (change == null)
                return;
            var pid = change.PolicyId ?? string.Empty;
            var scope = change.Scope ?? string.Empty;
            var existing = Pending.FirstOrDefault(p =>
                string.Equals(p?.PolicyId ?? string.Empty, pid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    p?.Scope ?? string.Empty,
                    scope,
                    StringComparison.OrdinalIgnoreCase
                )
            );
            bool isNew = existing == null;
            Dictionary<string, object>? effectiveOptionsClone;
            PolicyState effectiveState;
            if (existing != null)
            {
                existing.Action = change.Action;
                existing.Details = change.Details;
                existing.DetailsFull = change.DetailsFull;
                existing.DesiredState = change.DesiredState;
                existing.Options = CloneOptions(change.Options);
                existing.PolicyName = change.PolicyName;
                existing.CreatedAt = DateTime.Now;
                effectiveOptionsClone = CloneOptions(existing.Options);
                effectiveState = existing.DesiredState;
                var idx = Pending.IndexOf(existing);
                if (idx >= 0)
                {
                    Pending.RemoveAt(idx);
                    Pending.Insert(idx, existing);
                }
                Log.Info(
                    "PendingChanges",
                    $"Updated pending policy id={pid} scope={scope} action={existing.Action}"
                );
            }
            else
            {
                change.Options = CloneOptions(change.Options);
                effectiveOptionsClone = CloneOptions(change.Options);
                effectiveState = change.DesiredState;
                Pending.Add(change);
                Log.Info(
                    "PendingChanges",
                    $"Added pending policy id={pid} scope={scope} action={change.Action}"
                );
            }
            UpdateDirtyFlag();
            try
            {
                EventHub.PublishPendingQueueDelta(new[] { pid }, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                Log.Debug("PendingChanges", "PublishPendingQueueDelta failed: " + ex.Message);
            }
            // Broadcast fine-grained change for live UI sync (QuickEdit <-> EditSetting)
            try
            {
                EventHub.PublishPolicyChangeQueued(
                    pid,
                    scope,
                    effectiveState,
                    effectiveOptionsClone
                );
            }
            catch (Exception ex)
            {
                Log.Debug("PendingChanges", "PublishPolicyChangeQueued failed: " + ex.Message);
            }
        }

        // Discard: just remove pending entries
        public void Discard(params PendingChange[] changes)
        {
            if (changes == null || changes.Length == 0)
                return;
            var removedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in changes)
            {
                if (c == null)
                    continue;
                Pending.Remove(c);
                removedIds.Add(c.PolicyId);
                Log.Info("PendingChanges", $"Discarded policy id={c.PolicyId} scope={c.Scope}");
            }
            UpdateDirtyFlag();
            if (removedIds.Count > 0)
            {
                try
                {
                    EventHub.PublishPendingQueueDelta(Array.Empty<string>(), removedIds);
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "PendingChanges",
                        "PublishPendingQueueDelta (discard) failed: " + ex.Message
                    );
                }
                try
                {
                    EventHub.PublishPendingAppliedOrDiscarded(removedIds);
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "PendingChanges",
                        "PublishPendingAppliedOrDiscarded (discard) failed: " + ex.Message
                    );
                }
            }
        }

        private void TrimHistoryIfNeeded()
        {
            // Remove oldest (by AppliedAt) until within cap
            while (History.Count > MaxHistoryEntries)
            {
                var oldest = History.OrderBy(h => h.AppliedAt).FirstOrDefault();
                if (oldest == null)
                    break;
                History.Remove(oldest);
                Log.Debug("PendingChanges", $"Trimmed oldest history id={oldest.PolicyId}");
            }
        }

        public void AddHistory(HistoryRecord record)
        {
            if (record == null)
                return;
            History.Add(record);
            Log.Info(
                "PendingChanges",
                $"History add policy id={record.PolicyId} scope={record.Scope} action={record.Action} result={record.Result}"
            );
            TrimHistoryIfNeeded();
            try
            {
                SettingsService.Instance.SaveHistory(History.ToList());
            }
            catch (Exception ex)
            {
                Log.Warn("PendingChanges", "SaveHistory failed", ex);
            }
            try
            {
                EventHub.PublishHistoryChanged();
            }
            catch (Exception ex)
            {
                Log.Debug("PendingChanges", "PublishHistoryChanged failed: " + ex.Message);
            }
        }

        public void Applied(params PendingChange[] changes)
        {
            if (changes == null || changes.Length == 0)
                return;
            var appliedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in changes)
            {
                if (c == null)
                    continue;
                var rec = new HistoryRecord
                {
                    PolicyId = c.PolicyId,
                    PolicyName = c.PolicyName,
                    Scope = c.Scope,
                    Action = c.Action,
                    Result = "Applied",
                    Details = c.Details,
                    DetailsFull = c.DetailsFull,
                    AppliedAt = DateTime.Now,
                    DesiredState = c.DesiredState,
                    Options = CloneOptions(c.Options),
                };
                AddHistory(rec);
                Pending.Remove(c);
                appliedIds.Add(c.PolicyId);
                Log.Info(
                    "PendingChanges",
                    $"Applied policy id={c.PolicyId} scope={c.Scope} action={c.Action} state={c.DesiredState}"
                );
            }
            UpdateDirtyFlag();
            if (appliedIds.Count > 0)
            {
                try
                {
                    EventHub.PublishPendingQueueDelta(Array.Empty<string>(), appliedIds);
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "PendingChanges",
                        "PublishPendingQueueDelta (applied) failed: " + ex.Message
                    );
                }
                try
                {
                    EventHub.PublishPendingAppliedOrDiscarded(appliedIds);
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "PendingChanges",
                        "PublishPendingAppliedOrDiscarded (applied) failed: " + ex.Message
                    );
                }
            }
        }

        public void DiscardAll()
        {
            var count = Pending.Count;
            if (count == 0)
                return;
            var all = Pending.ToArray();
            var removedIds = new HashSet<string>(
                all.Select(p => p.PolicyId),
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var c in all)
                Pending.Remove(c);
            Log.Info("PendingChanges", $"DiscardAll removed={count}");
            UpdateDirtyFlag();
            if (removedIds.Count > 0)
            {
                try
                {
                    EventHub.PublishPendingQueueDelta(Array.Empty<string>(), removedIds);
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "PendingChanges",
                        "PublishPendingQueueDelta (discardAll) failed: " + ex.Message
                    );
                }
                try
                {
                    EventHub.PublishPendingAppliedOrDiscarded(removedIds);
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        "PendingChanges",
                        "PublishPendingAppliedOrDiscarded (discardAll) failed: " + ex.Message
                    );
                }
            }
        }
    }
}
