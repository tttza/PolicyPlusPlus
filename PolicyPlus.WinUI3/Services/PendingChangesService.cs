using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using PolicyPlus.Core.Core;

namespace PolicyPlus.WinUI3.Services
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
        public string Result { get; set; } = string.Empty; // Applied / Reapplied (legacy Discarded entries may still exist)
        public string Details { get; set; } = string.Empty;
        public string DetailsFull { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; } = DateTime.Now;
        public PolicyState DesiredState { get; set; } = PolicyState.NotConfigured;
        public Dictionary<string, object>? Options { get; set; }
    }

    public sealed class PendingChangesService
    {
        public static PendingChangesService Instance { get; } = new PendingChangesService();

        public ObservableCollection<PendingChange> Pending { get; } = new();
        public ObservableCollection<HistoryRecord> History { get; } = new();

        private PendingChangesService() { }

        private static Dictionary<string, object>? CloneOptions(Dictionary<string, object>? src)
        {
            if (src == null) return null;
            var clone = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src)
            {
                var v = kv.Value;
                if (v is null) clone[kv.Key] = string.Empty;
                else if (v is string s) clone[kv.Key] = s;
                else if (v is bool || v is int || v is long || v is uint || v is double) clone[kv.Key] = v;
                else if (v is string[] arr) clone[kv.Key] = arr.ToArray();
                else if (v is IEnumerable<string> strEnum) clone[kv.Key] = strEnum.ToList();
                else if (v is List<string> strList) clone[kv.Key] = new List<string>(strList);
                else if (v is IEnumerable<KeyValuePair<string, string>> kvps) clone[kv.Key] = kvps.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)).ToList();
                else clone[kv.Key] = v.ToString() ?? string.Empty;
            }
            return clone;
        }

        public void Add(PendingChange change)
        {
            if (change == null) return;
            var pid = change.PolicyId ?? string.Empty;
            var scope = change.Scope ?? string.Empty;
            var existing = Pending.FirstOrDefault(p => string.Equals(p?.PolicyId ?? string.Empty, pid, StringComparison.OrdinalIgnoreCase) && string.Equals(p?.Scope ?? string.Empty, scope, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Action = change.Action;
                existing.Details = change.Details;
                existing.DetailsFull = change.DetailsFull;
                existing.DesiredState = change.DesiredState;
                existing.Options = CloneOptions(change.Options);
                existing.PolicyName = change.PolicyName;
                existing.CreatedAt = DateTime.Now;
                var idx = Pending.IndexOf(existing);
                if (idx >= 0)
                {
                    Pending.RemoveAt(idx);
                    Pending.Insert(idx, existing);
                }
            }
            else
            {
                change.Options = CloneOptions(change.Options);
                Pending.Add(change);
            }
        }

        // Discard: just remove pending entries (do NOT record in history anymore)
        public void Discard(params PendingChange[] changes)
        {
            if (changes == null || changes.Length == 0) return;
            foreach (var c in changes)
            {
                if (c == null) continue;
                Pending.Remove(c);
            }
        }

        public void Applied(params PendingChange[] changes)
        {
            if (changes == null || changes.Length == 0) return;
            bool any = false;
            foreach (var c in changes)
            {
                if (c == null) continue;
                History.Add(new HistoryRecord
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
                    Options = CloneOptions(c.Options)
                });
                Pending.Remove(c);
                any = true;
            }
            if (any)
            {
                try { SettingsService.Instance.SaveHistory(History.ToList()); } catch { }
            }
        }

        public void DiscardAll() => Discard(Pending.ToArray());
    }
}
