using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;

namespace PolicyPlus.WinUI3.Services
{
    public class PendingChange
    {
        public string PolicyId { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public string Scope { get; set; } = "Both"; // Computer/User/Both
        public string Action { get; set; } = "Set"; // UI label: Set/Delete/Enable/Disable/Clear
        public string Details { get; set; } = string.Empty;
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
        public string Action { get; set; } = string.Empty; // Enable/Disable/Clear/Revert
        public string Result { get; set; } = string.Empty; // Applied/Discarded/Reverted
        public string Details { get; set; } = string.Empty;
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
            var existing = Pending.FirstOrDefault(p => string.Equals(p.PolicyId, change.PolicyId, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(p.Scope, change.Scope, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Action = change.Action;
                existing.Details = change.Details;
                existing.DesiredState = change.DesiredState;
                existing.Options = change.Options;
                existing.PolicyName = change.PolicyName;
                existing.CreatedAt = DateTime.Now;
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
