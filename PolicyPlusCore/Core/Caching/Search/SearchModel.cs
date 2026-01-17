using PolicyPlusCore.Utilities;

namespace PolicyPlusCore.Core.Caching.Search;

internal readonly record struct SearchRequest(
    SearchQuery Query,
    SearchCulturePreference Cultures,
    SearchFieldSet Fields,
    bool AndMode,
    int Limit
)
{
    public static bool TryCreate(
        string query,
        IReadOnlyList<string>? cultures,
        SearchFields fields,
        bool andMode,
        int limit,
        out SearchRequest request
    )
    {
        request = default;

        if (string.IsNullOrWhiteSpace(query) || fields == SearchFields.None)
            return false;

        var culturePref = SearchCulturePreference.Create(cultures);
        var searchQuery = SearchQuery.Create(query, andMode);
        var fieldSet = SearchFieldSet.Create(fields, searchQuery);
        if (!fieldSet.HasAny)
            return false;

        request = new SearchRequest(searchQuery, culturePref, fieldSet, andMode, limit);
        return true;
    }
}

internal sealed class SearchCulturePreference
{
    public IReadOnlyList<string> All { get; }
    public string Primary { get; }
    public string? Second { get; }

    private SearchCulturePreference(IReadOnlyList<string> all, string primary, string? second)
    {
        All = all;
        Primary = primary;
        Second = second;
    }

    public static SearchCulturePreference Create(IReadOnlyList<string>? cultures)
    {
        List<string> normalized;
        if (cultures == null || cultures.Count == 0)
        {
            normalized = new List<string>(1)
            {
                SearchHeuristics.NormalizeCultureName(string.Empty),
            };
        }
        else
        {
            normalized = new List<string>(cultures.Count);
            for (int i = 0; i < cultures.Count; i++)
                normalized.Add(SearchHeuristics.NormalizeCultureName(cultures[i]));
            if (normalized.Count == 0)
                normalized.Add(SearchHeuristics.NormalizeCultureName(string.Empty));
        }

        var primary = normalized[0];
        string? second = normalized.Count > 1 ? normalized[1] : null;
        if (second != null && string.Equals(second, primary, StringComparison.OrdinalIgnoreCase))
            second = null;

        return new SearchCulturePreference(normalized, primary, second);
    }
}

internal readonly record struct SearchQuery(
    string Raw,
    string Strict,
    string NGramStrict,
    string NGramLoose,
    bool AndMode
)
{
    public bool IsShort => Strict.Length <= 2;
    public bool IsSingleToken => Strict.IndexOf(' ') < 0;
    public bool IsPhraseMode => !AndMode && Strict.Contains(' ');

    public bool LooksLikeRegistry => SearchHeuristics.LooksLikeRegistryQuery(Raw);
    public bool LooksLikeId => SearchHeuristics.LooksLikeIdQuery(Raw);

    public bool IsSingleAsciiAlnumLenAtLeast3 =>
        IsSingleToken
        && Strict.Length >= 3
        && Strict.All(ch => ch < 128 && char.IsLetterOrDigit(ch));

    public bool IsSingleAsciiAlnumLenAtLeast8 =>
        IsSingleToken
        && Strict.Length >= 8
        && Strict.All(ch => ch < 128 && char.IsLetterOrDigit(ch));

    public bool IsSingleCjkLenAtLeast3 => IsSingleToken && Strict.Length >= 3 && Strict.Any(IsCjk);

    public static SearchQuery Create(string query, bool andMode)
    {
        var strict = TextNormalization.NormalizeStrict(query);
        var gramsStrict = TextNormalization.ToNGramTokens(strict);
        var gramsLoose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(strict)
        );
        return new SearchQuery(query, strict, gramsStrict, gramsLoose, andMode);
    }

    private static bool IsCjk(char ch) =>
        (ch >= '\u3040' && ch <= '\u30FF')
        || (ch >= '\u3400' && ch <= '\u9FFF')
        || (ch >= '\uF900' && ch <= '\uFAFF');
}

internal readonly record struct SearchFieldSet(
    SearchFields Raw,
    bool UseName,
    bool UseId,
    bool UseRegistry,
    bool UseDescription
)
{
    public bool HasAny => UseName || UseId || UseRegistry || UseDescription;

    public static SearchFieldSet Create(SearchFields fields, SearchQuery query)
    {
        bool useName = (fields & SearchFields.Name) != 0;
        bool useId = (fields & SearchFields.Id) != 0;
        bool useDesc = (fields & SearchFields.Description) != 0;

        bool wantRegistry = (fields & SearchFields.Registry) != 0;
        bool useRegistry = wantRegistry && query.LooksLikeRegistry;

        return new SearchFieldSet(fields, useName, useId, useRegistry, useDesc);
    }
}

internal static class SearchOrdering
{
    public static void AppendPriorityOrdered(
        IEnumerable<PolicyHit> source,
        List<PolicyHit> dest,
        int limit,
        string qLowerOrder
    )
    {
        int Priority(PolicyHit h)
        {
            bool idHit =
                !string.IsNullOrEmpty(h.UniqueId)
                && h.UniqueId.IndexOf(qLowerOrder, StringComparison.OrdinalIgnoreCase) >= 0;
            bool nameHit =
                !string.IsNullOrEmpty(h.DisplayName)
                && h.DisplayName.IndexOf(qLowerOrder, StringComparison.OrdinalIgnoreCase) >= 0;
            bool regHit =
                !string.IsNullOrEmpty(h.RegistryPath)
                && h.RegistryPath.IndexOf(qLowerOrder, StringComparison.OrdinalIgnoreCase) >= 0;
            bool regValueHit = false;
            if (regHit)
            {
                try
                {
                    var parts = h.RegistryPath.Split('\\');
                    if (parts.Length > 0)
                    {
                        var last = parts[^1];
                        if (
                            !string.IsNullOrEmpty(last)
                            && last.IndexOf(qLowerOrder, StringComparison.OrdinalIgnoreCase) >= 0
                        )
                            regValueHit = true;
                    }
                }
                catch { }
            }
            if (idHit || nameHit || regValueHit)
                return 3;
            if (regHit)
                return 2;
            return 1;
        }

        foreach (
            var h in source
                .Select(h => (h, pri: Priority(h)))
                .OrderByDescending(t => t.pri)
                .ThenByDescending(t => t.h.Score)
                .ThenBy(t => t.h.UniqueId, StringComparer.OrdinalIgnoreCase)
                .Select(t => t.h)
        )
        {
            if (dest.Count >= limit)
                break;
            dest.Add(h);
        }
    }
}

internal static class SearchHeuristics
{
    public static string NormalizeCultureName(string culture)
    {
        return Culture.CultureNameNormalization.NormalizeCultureName(culture);
    }

    public static bool LooksLikeRegistryQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;
        var q = query.Trim();
        if (q.Contains('\\'))
            return true;
        var ql = q.Replace("_", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (
            ql.StartsWith("HKCU")
            || ql.StartsWith("HKLM")
            || ql.StartsWith("HKEYCURRENTUSER")
            || ql.StartsWith("HKEYLOCALMACHINE")
        )
            return true;
        var qll = q.ToLowerInvariant();
        if (
            qll.Contains("policies\\")
            || qll.Contains("software\\")
            || qll.Contains("system\\")
            || qll.Contains("microsoft\\")
        )
            return true;
        return false;
    }

    public static bool LooksLikeIdQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;
        if (query.Contains(':'))
            return true;
        if (query.IndexOf(' ') < 0 && query.Length >= 12)
        {
            int transitions = 0;
            for (int i = 1; i < query.Length; i++)
            {
                if (char.IsUpper(query[i]) && char.IsLower(query[i - 1]))
                    transitions++;
                if (transitions >= 2)
                    return true;
            }
        }
        return false;
    }
}
