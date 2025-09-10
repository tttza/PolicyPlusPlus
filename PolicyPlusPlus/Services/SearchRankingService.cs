using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PolicyPlusPlus.Services
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

        public static void Initialize(IDictionary<string, int> counts, IDictionary<string, DateTime> lastUsed)
        {
            try
            {
                _counts.Clear();
                _lastUsed.Clear();
                if (counts != null)
                {
                    foreach (var kv in counts)
                        _counts[kv.Key] = kv.Value;
                }
                if (lastUsed != null)
                {
                    foreach (var kv in lastUsed)
                        _lastUsed[kv.Key] = kv.Value;
                }
            }
            catch { }
        }

        public static (Dictionary<string, int> counts, Dictionary<string, DateTime> lastUsed) GetSnapshot()
        {
            var c = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var l = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _counts) c[kv.Key] = kv.Value;
            foreach (var kv in _lastUsed) l[kv.Key] = kv.Value;
            return (c, l);
        }
    }
}
