using System;
using System.Collections.Generic;

namespace PolicyPlusCore.Utilities
{
    // Determines whether we should suppress in-memory fallback search when cache misses.
    public static class SearchFallbackPolicy
    {
        // New preferred overload using CultureSlot metadata (explicit placeholder detection).
        public static bool ShouldSkipMemoryFallback(
            int tokenCount,
            IReadOnlyList<CultureSlot> slots
        )
        {
            if (tokenCount > 1)
                return false; // Only suppress for single-token queries.
            if (slots == null || slots.Count < 2)
                return false;
            // Find second slot if present
            CultureSlot? second = null;
            for (int i = 1; i < slots.Count; i++)
            {
                if (slots[i].Role == CultureRole.Second)
                {
                    second = slots[i];
                    break;
                }
            }
            if (second is null)
                return false; // no second slot -> nothing to suppress (legacy path)
            if (second.Value.IsPlaceholder)
            {
                // Placeholder second with at least one further fallback culture => suppress to avoid resurrecting fallback only data.
                bool hasAdditional = slots.Count > 2; // beyond primary + placeholder
                return hasAdditional;
            }
            return false; // Real second language present -> allow memory fallback
        }

        // Backwards compatible overload retained temporarily until all callers migrated.
        public static bool ShouldSkipMemoryFallback(
            int tokenCount,
            IReadOnlyList<string> orderedCultures,
            string primary,
            bool secondEnabled
        )
        {
            if (orderedCultures == null)
                return false;
            // Reconstruct slots minimal: primary assumed at index 0; if index1 exists treat as second (placeholder if duplicate & second disabled)
            var slots = new List<CultureSlot>(orderedCultures.Count);
            if (orderedCultures.Count > 0)
                slots.Add(new CultureSlot(orderedCultures[0], CultureRole.Primary, false));
            if (orderedCultures.Count > 1)
            {
                bool placeholder = (
                    !secondEnabled
                    && string.Equals(
                        orderedCultures[0],
                        orderedCultures[1],
                        StringComparison.OrdinalIgnoreCase
                    )
                );
                slots.Add(new CultureSlot(orderedCultures[1], CultureRole.Second, placeholder));
                for (int i = 2; i < orderedCultures.Count; i++)
                    slots.Add(
                        new CultureSlot(orderedCultures[i], CultureRole.OtherFallback, false)
                    );
            }
            return ShouldSkipMemoryFallback(tokenCount, slots);
        }
    }
}
