using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusPlus.Services;

namespace PolicyPlusPlus.ViewModels
{
    public static class PendingChangesFilter
    {
        public static List<PendingChange> FilterPending(IEnumerable<PendingChange> src, string query, string scope, string operation)
        {
            string q = (query ?? string.Empty).Trim();
            string sc = string.IsNullOrWhiteSpace(scope) ? "Both" : scope;
            string op = string.IsNullOrWhiteSpace(operation) ? "All" : operation;

            var list = new List<PendingChange>();
            foreach (var c in src ?? Enumerable.Empty<PendingChange>())
            {
                if (c == null) continue;
                bool scopeOk = (sc == "Both") || string.Equals(c.Scope ?? string.Empty, sc, StringComparison.OrdinalIgnoreCase);
                bool opOk = (op == "All") || string.Equals(c.Action ?? string.Empty, op, StringComparison.OrdinalIgnoreCase);
                bool textOk = string.IsNullOrEmpty(q) ||
                              (c.PolicyName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                              (c.Details?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
                if (scopeOk && opOk && textOk)
                    list.Add(c);
            }
            return list.OrderBy(c => c.PolicyName ?? string.Empty).ToList();
        }

        public static List<HistoryRecord> FilterHistory(IEnumerable<HistoryRecord> src, string query, string type, string range, DateTime? now = null)
        {
            string q = (query ?? string.Empty).Trim();
            string ty = string.IsNullOrWhiteSpace(type) ? "All" : type;
            string rg = string.IsNullOrWhiteSpace(range) ? "All" : range;

            DateTime baseNow = now ?? DateTime.Now;
            DateTime today = baseNow.Date;
            DateTime? since = null;
            if (string.Equals(rg, "Today", StringComparison.OrdinalIgnoreCase)) since = today;
            else if (string.Equals(rg, "Last 7 days", StringComparison.OrdinalIgnoreCase)) since = today.AddDays(-7);
            else if (string.Equals(rg, "Last 30 days", StringComparison.OrdinalIgnoreCase)) since = today.AddDays(-30);

            var list = new List<HistoryRecord>();
            foreach (var h in src ?? Enumerable.Empty<HistoryRecord>())
            {
                if (h == null) continue;
                bool typeOk = (ty == "All") || string.Equals(h.Result ?? string.Empty, ty, StringComparison.OrdinalIgnoreCase);
                bool sinceOk = (!since.HasValue) || h.AppliedAt >= since.Value;
                bool textOk = string.IsNullOrEmpty(q) ||
                              (h.PolicyName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                              (h.Details?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
                if (typeOk && sinceOk && textOk)
                    list.Add(h);
            }
            return list.OrderByDescending(h => h.AppliedAt).ToList();
        }

        public static string BuildSummary(int pendingCount)
        {
            return pendingCount == 0 ? "No pending changes" : $"{pendingCount} pending change(s)";
        }
    }
}
