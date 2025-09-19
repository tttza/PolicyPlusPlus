namespace PolicyPlusPlus.Filtering
{
    /// <summary>
    /// Encapsulates the decision logic for filter presentation (flattening, headers, limits) based on current flags.
    /// Matches the provided truth table specification.
    /// </summary>
    public static class FilterDecisionEngine
    {
        public static FilterDecisionResult Evaluate(
            bool hasCategory,
            bool hasSearch,
            bool configuredOnly,
            bool bookmarkOnly,
            bool limitSettingEnabled
        )
        {
            // Row classification is mutually exclusive by the combination of 4 booleans.
            // Implement rules derived from table:
            // 1) FlattenHierarchy = false only for rows (1) and (2); otherwise true.
            //    Row1: no category, no search, no configured, no bookmark.
            //    Row2: category only (no search/config/bookmark).
            bool flatten = true;
            if (!hasSearch && !configuredOnly && !bookmarkOnly)
            {
                if (!hasCategory) // row1 scenario
                    flatten = false;
                else // hasCategory && only category filter -> row2
                    flatten = false;
            }

            // 2) ShowSubcategoryHeaders = true only in row2 (category only).
            bool showSubcatHeaders = hasCategory && !hasSearch && !configuredOnly && !bookmarkOnly;

            // 3) IncludeSubcategoryPolicies = false only in row2; otherwise true.
            bool includeSubcatPolicies = !(
                hasCategory && !hasSearch && !configuredOnly && !bookmarkOnly
            );

            // 4) Limit: 1000 only in rows 1 and 3 (global base or global search) AND only when limitSettingEnabled=true.
            // Rows 1: no category, no search, no other filters -> limit 1000.
            // Row 3: no category, search only (no configured, no bookmark) -> limit 1000.
            int? limit = null;
            if (limitSettingEnabled && !hasCategory && !configuredOnly && !bookmarkOnly)
            {
                // Now only search flag differentiates row1 (false) vs row3 (true); both limited.
                limit = 1000;
            }

            return new FilterDecisionResult
            {
                FlattenHierarchy = flatten,
                ShowSubcategoryHeaders = showSubcatHeaders,
                IncludeSubcategoryPolicies = includeSubcatPolicies,
                Limit = limit,
            };
        }
    }

    public sealed class FilterDecisionResult
    {
        public bool FlattenHierarchy { get; init; }
        public bool ShowSubcategoryHeaders { get; init; }
        public bool IncludeSubcategoryPolicies { get; init; }
        public int? Limit { get; init; }
    }
}
