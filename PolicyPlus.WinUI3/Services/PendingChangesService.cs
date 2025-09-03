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

        // For application
        public PolicyState DesiredState { get; set; } = PolicyState.NotConfigured;
        public Dictionary<string, object>? Options { get; set; }
    }

    public class HistoryRecord
    {
        public string PolicyId { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // Enable/Disable/Clear/Reapply
        public string Result { get; set; } = string.Empty; // Applied/Discarded/Reapplied
        public string Details { get; set; } = string.Empty; // short summary
        public string DetailsFull { get; set; } = string.Empty; // expanded
        public DateTime AppliedAt { get; set; } = DateTime.Now;

        // Exact state snapshot to allow precise restore
        public PolicyState DesiredState { get; set; } = PolicyState.NotConfigured;
        public Dictionary<string, object>? Options { get; set; }
    }

    public sealed class PendingChangesService
    {
        public static PendingChangesService Instance { get; } = new PendingChangesService();

        public ObservableCollection<PendingChange> Pending { get; } = new();
        public ObservableCollection<HistoryRecord> History { get; } = new();

        private PendingChangesService() { }

        public void Add(PendingChange change)
        {
            if (change == null) return;
            var pid = change.PolicyId ?? string.Empty;
            var scope = change.Scope ?? string.Empty;
            var existing = Pending.FirstOrDefault(p => string.Equals(p?.PolicyId ?? string.Empty, pid, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(p?.Scope ?? string.Empty, scope, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Action = change.Action;
                existing.Details = change.Details;
                existing.DetailsFull = change.DetailsFull;
                existing.DesiredState = change.DesiredState;
                existing.Options = change.Options;
                existing.PolicyName = change.PolicyName;
                existing.CreatedAt = DateTime.Now;

                // Force a collection changed so bindings refresh (list uses a projection)
                var idx = Pending.IndexOf(existing);
                if (idx >= 0)
                {
                    Pending.RemoveAt(idx);
                    Pending.Insert(idx, existing);
                }
            }
            else
            {
                Pending.Add(change);
            }
        }

        public void Discard(params PendingChange[] changes)
        {
            foreach (var c in changes)
            {
                History.Add(new HistoryRecord
                {
                    PolicyId = c.PolicyId,
                    PolicyName = c.PolicyName,
                    Scope = c.Scope,
                    Action = c.Action,
                    Result = "Discarded",
                    Details = c.Details,
                    DetailsFull = c.DetailsFull,
                    AppliedAt = DateTime.Now,
                    DesiredState = c.DesiredState,
                    Options = c.Options
                });
                Pending.Remove(c);
            }
        }

        public void Applied(params PendingChange[] changes)
        {
            foreach (var c in changes)
            {
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
                    Options = c.Options
                });
                Pending.Remove(c);
            }
        }

        public void DiscardAll() => Discard(Pending.ToArray());
    }
}
