using System;
using System.Collections.Concurrent;

namespace PolicyPlus.WinUI3.Services
{
    internal static class SearchRankingService
    {
        private static readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, DateTime> _lastUsed = new(StringComparer.OrdinalIgnoreCase);

        public static void RecordUsage(string policyId)
        {
            if (string.IsNullOrEmpty(policyId)) return;
            _counts.AddOrUpdate(policyId, 1, (_, c) => c + 1);
            _lastUsed[policyId] = DateTime.UtcNow;
        }

        public static int GetBoost(string policyId)
        {
            if (string.IsNullOrEmpty(policyId)) return 0;
            _counts.TryGetValue(policyId, out var cnt);
            _lastUsed.TryGetValue(policyId, out var last);
            int countBoost = Math.Min(50, cnt * 5); // up to +50 for frequent use
            int recencyBoost = 0;
            if (last != default)
            {
                var age = DateTime.UtcNow - last;
                if (age <= TimeSpan.FromHours(1)) recencyBoost = 30;
                else if (age <= TimeSpan.FromDays(1)) recencyBoost = 20;
                else if (age <= TimeSpan.FromDays(7)) recencyBoost = 10;
            }
            return countBoost + recencyBoost;
        }
    }
}
