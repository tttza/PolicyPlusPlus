using PolicyPlusCore.Core; // For AdmxPolicySection
using System;
using System.Collections.Generic;

namespace PolicyPlusPlus.Services
{
    public sealed class ViewNavigationService
    {
        public static ViewNavigationService Instance { get; } = new ViewNavigationService();

        private readonly List<ViewState> _items = new List<ViewState>();
        private int _index = -1;

        public event EventHandler? HistoryChanged;

        private ViewNavigationService() { }

        public bool CanGoBack => _index > 0;
        public bool CanGoForward => _index >= 0 && _index < _items.Count - 1;
        public ViewState? Current => (_index >= 0 && _index < _items.Count) ? _items[_index] : null;

        public void Clear()
        {
            _items.Clear();
            _index = -1;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Push(ViewState state)
        {
            if (state == null) return;

            // Trim any forward history
            if (_index >= 0 && _index < _items.Count - 1)
            {
                _items.RemoveRange(_index + 1, _items.Count - (_index + 1));
            }

            // Avoid pushing duplicates
            if (Current != null && Current.Equals(state))
            {
                return;
            }

            _items.Add(state);
            _index = _items.Count - 1;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public ViewState? GoBack()
        {
            if (!CanGoBack) return Current;
            _index--;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return Current;
        }

        public ViewState? GoForward()
        {
            if (!CanGoForward) return Current;
            _index++;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return Current;
        }
    }

    public sealed class ViewState : IEquatable<ViewState>
    {
        public string? CategoryId { get; init; }
        public string? Query { get; init; }
        public AdmxPolicySection AppliesTo { get; init; }
        public bool ConfiguredOnly { get; init; }

        public override bool Equals(object? obj) => Equals(obj as ViewState);

        public bool Equals(ViewState? other)
        {
            if (other == null) return false;
            return string.Equals(CategoryId ?? string.Empty, other.CategoryId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Query ?? string.Empty, other.Query ?? string.Empty, StringComparison.Ordinal)
                && AppliesTo == other.AppliesTo
                && ConfiguredOnly == other.ConfiguredOnly;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (CategoryId?.ToLowerInvariant().GetHashCode() ?? 0);
                h = h * 31 + (Query?.GetHashCode() ?? 0);
                h = h * 31 + (int)AppliesTo;
                h = h * 31 + (ConfiguredOnly ? 1 : 0);
                return h;
            }
        }

        public static ViewState Create(string? categoryId, string? query, AdmxPolicySection applies, bool configuredOnly)
            => new ViewState { CategoryId = categoryId, Query = query ?? string.Empty, AppliesTo = applies, ConfiguredOnly = configuredOnly };
    }
}
