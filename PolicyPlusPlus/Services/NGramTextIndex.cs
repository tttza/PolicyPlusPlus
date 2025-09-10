using System;
using System.Collections.Generic;
using System.Linq;

namespace PolicyPlusPlus.Services
{
    // Simple N-gram inverted index for fast substring candidate filtering.
    internal sealed class NGramTextIndex
    {
        private readonly int _n;
        // gram -> policy ids
        private readonly Dictionary<string, HashSet<string>> _postings = new(StringComparer.Ordinal);

        public NGramTextIndex(int n = 2)
        {
            _n = Math.Max(1, n);
        }

        public int N => _n;

        public void Clear()
        {
            _postings.Clear();
        }

        public void Build(IEnumerable<(string id, string normalizedText)> items)
        {
            _postings.Clear();
            foreach (var (id, text) in items)
            {
                var s = text ?? string.Empty;
                if (s.Length < _n) continue;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i <= s.Length - _n; i++)
                {
                    var gram = s.Substring(i, _n);
                    if (!seen.Add(gram)) continue; // avoid adding same gram for this doc multiple times
                    if (!_postings.TryGetValue(gram, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _postings[gram] = set;
                    }
                    set.Add(id);
                }
            }
        }

        // Returns null if the index is not helpful for this query (e.g. too short or empty postings)
        public HashSet<string>? TryQuery(string normalizedQuery)
        {
            var q = normalizedQuery ?? string.Empty;
            if (q.Length < _n) return null;
            var grams = new List<HashSet<string>>();
            for (int i = 0; i <= q.Length - _n; i++)
            {
                var g = q.Substring(i, _n);
                if (_postings.TryGetValue(g, out var set)) grams.Add(set);
                else return new HashSet<string>(StringComparer.OrdinalIgnoreCase); // one gram missing => no candidates
            }
            if (grams.Count == 0) return null;
            // Intersect starting from the smallest set to reduce work
            grams.Sort((a, b) => a.Count.CompareTo(b.Count));
            HashSet<string> result = new HashSet<string>(grams[0], StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < grams.Count && result.Count > 0; i++)
            {
                result.IntersectWith(grams[i]);
            }
            return result;
        }

        // Serializable snapshot for caching
        public sealed class NGramSnapshot
        {
            public int N { get; set; }
            public Dictionary<string, string[]> Postings { get; set; } = new(StringComparer.Ordinal);
        }

        public NGramSnapshot GetSnapshot()
        {
            var dict = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var kv in _postings)
            {
                dict[kv.Key] = kv.Value.ToArray();
            }
            return new NGramSnapshot { N = _n, Postings = dict };
        }

        public void LoadSnapshot(NGramSnapshot snapshot)
        {
            _postings.Clear();
            if (snapshot == null || snapshot.Postings == null) return;
            foreach (var kv in snapshot.Postings)
            {
                _postings[kv.Key] = new HashSet<string>(kv.Value ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
