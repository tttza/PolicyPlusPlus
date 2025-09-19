using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using PolicyPlusCore.Core;

namespace PolicyPlusPlus.Services
{
    // Central UI event hub for cross-window refresh and queue state notifications.
    internal static class EventHub
    {
        public static event Action<IReadOnlyCollection<string>?>? PolicySourcesRefreshed; // null => full refresh
        public static event Action<
            IReadOnlyCollection<string>,
            IReadOnlyCollection<string>
        >? PendingQueueChanged; // added, removed
        public static event Action<IReadOnlyCollection<string>>? PendingAppliedOrDiscarded; // after sources refresh
        public static event Action<
            string,
            string,
            PolicyState,
            Dictionary<string, object>?
        >? PolicyChangeQueued; // policyId, scope, desired, options
        public static event Action? HistoryChanged;

        private static readonly HashSet<string> _affectedForRefresh = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly HashSet<string> _pendingAdded = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly HashSet<string> _pendingRemoved = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static DispatcherQueueTimer? _timer;
        private static readonly object _lock = new();

        public static void PublishPolicySourcesRefreshed(IEnumerable<string>? policyIds = null)
        {
            if (policyIds == null)
            {
                TryInvoke(() => PolicySourcesRefreshed?.Invoke(null));
                return;
            }
            lock (_lock)
            {
                foreach (var id in policyIds)
                    _affectedForRefresh.Add(id);
            }
            if (!EnsureTimer())
                FlushAggregated();
        }

        public static void PublishPendingQueueDelta(
            IEnumerable<string> added,
            IEnumerable<string> removed
        )
        {
            lock (_lock)
            {
                foreach (var id in added)
                    _pendingAdded.Add(id);
                foreach (var id in removed)
                    _pendingRemoved.Add(id);
            }
            if (!EnsureTimer())
                FlushAggregated();
        }

        public static void PublishPendingAppliedOrDiscarded(IEnumerable<string> affectedIds)
        {
            TryInvoke(() => PendingAppliedOrDiscarded?.Invoke(new List<string>(affectedIds)));
        }

        public static void PublishPolicyChangeQueued(
            string policyId,
            string scope,
            PolicyState state,
            Dictionary<string, object>? options
        )
        {
            TryInvoke(() => PolicyChangeQueued?.Invoke(policyId, scope, state, options));
        }

        public static void PublishHistoryChanged()
        {
            TryInvoke(() => HistoryChanged?.Invoke());
        }

        private static bool EnsureTimer()
        {
            if (_timer != null)
                return true;
            try
            {
                _timer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
                if (_timer == null)
                    return false; // no UI dispatcher context
                _timer.Interval = TimeSpan.FromMilliseconds(160);
                _timer.IsRepeating = false;
                _timer.Tick += (s, e) => FlushAggregated();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void FlushAggregated()
        {
            List<string> refresh;
            List<string> add;
            List<string> rem;
            lock (_lock)
            {
                refresh = new List<string>(_affectedForRefresh);
                add = new List<string>(_pendingAdded);
                rem = new List<string>(_pendingRemoved);
                _affectedForRefresh.Clear();
                _pendingAdded.Clear();
                _pendingRemoved.Clear();
            }
            if (refresh.Count > 0)
                TryInvoke(() => PolicySourcesRefreshed?.Invoke(refresh));
            if (add.Count > 0 || rem.Count > 0)
                TryInvoke(() => PendingQueueChanged?.Invoke(add, rem));
        }

        private static void TryInvoke(Action action)
        {
            try
            {
                action();
            }
            catch { }
        }
    }
}
