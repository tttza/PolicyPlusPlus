using System.Globalization;
using System.Text;
using PolicyPlusCore.Core.Caching.Search;
using PolicyPlusCore.Utilities;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheSearchService
{
    public static async Task<IReadOnlyList<PolicyHit>> SearchAsync(
        AdmxCache cache,
        string query,
        IReadOnlyList<string> cultures,
        SearchFields fields,
        bool andMode,
        int limit,
        CancellationToken ct
    )
    {
        if (cultures == null || cultures.Count == 0)
            cultures = new[] { CultureInfo.CurrentUICulture.Name };
        if (string.IsNullOrWhiteSpace(query) || fields == SearchFields.None)
            return Array.Empty<PolicyHit>();

        var qExact = query;
        var qPrefix = query + "%";
        var qStrict = TextNormalization.NormalizeStrict(query);
        var norm = TextNormalization.ToNGramTokens(qStrict);
        var loose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(qStrict)
        );

        bool useId = (fields & SearchFields.Id) != 0;
        bool useReg =
            (fields & SearchFields.Registry) != 0 && SearchHeuristics.LooksLikeRegistryQuery(query);
        bool useName = (fields & SearchFields.Name) != 0;
        bool useDesc = (fields & SearchFields.Description) != 0;
        bool enableFts = useName || useDesc || useReg || useId;
        bool shortQuery = qStrict.Length <= 2;
        if (shortQuery)
        {
            enableFts = false;
        }

        var ftsNormCols = new List<string>();
        if (useName)
            ftsNormCols.Add("title_norm");
        if (useDesc)
            ftsNormCols.Add("desc_norm");
        if (useId && SearchHeuristics.LooksLikeIdQuery(query))
            ftsNormCols.Add("tags");
        if (useReg)
            ftsNormCols.Add("registry_path");
        var ftsLooseCols = new List<string>();
        if (useName)
            ftsLooseCols.Add("title_loose");
        if (useDesc)
            ftsLooseCols.Add("desc_loose");
        if (useId && SearchHeuristics.LooksLikeIdQuery(query))
            ftsLooseCols.Add("tags");
        if (useReg)
            ftsLooseCols.Add("registry_path");

        static string EscapeSqlSingle(string s) => s.Replace("'", "''");

        static IEnumerable<string> SplitTokens(string grams) =>
            grams.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        static string SanitizeTok(string t)
        {
            if (string.IsNullOrEmpty(t))
                return string.Empty;
            var sb = new StringBuilder(t.Length);
            foreach (var ch in t)
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
            return sb.ToString();
        }

        static string BuildFtsOr(List<string> cols, IEnumerable<string> rawTokens)
        {
            var toks = rawTokens
                .Select(SanitizeTok)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (cols.Count == 0 || toks.Count == 0)
                return string.Empty;

            var perTok = new List<string>(toks.Count);
            foreach (var t in toks)
                perTok.Add("(" + string.Join(" OR ", cols.Select(c => c + ":(" + t + ")")) + ")");
            return string.Join(" OR ", perTok);
        }

        static string BuildFtsAnd(List<string> cols, IEnumerable<string> rawTokens)
        {
            var toks = rawTokens
                .Select(SanitizeTok)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (cols.Count == 0 || toks.Count == 0)
                return string.Empty;

            var per = new List<string>(toks.Count);
            foreach (var t in toks)
                per.Add("(" + string.Join(" OR ", cols.Select(c => c + ":(" + t + ")")) + ")");
            return string.Join(" AND ", per);
        }

        var normTokens = SplitTokens(norm);
        var looseTokens = SplitTokens(loose);

        static string BuildPerSegmentAllGrams(
            List<string> cols,
            IEnumerable<string> rawTokens,
            int maxGrams = 8
        )
        {
            if (cols.Count == 0)
                return string.Empty;

            var toks = rawTokens
                .Select(SanitizeTok)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(maxGrams)
                .ToList();

            if (toks.Count == 0)
                return string.Empty;
            if (toks.Count == 1)
                return "(" + string.Join(" OR ", cols.Select(c => c + ":(" + toks[0] + ")")) + ")";

            var per = new List<string>(toks.Count);
            foreach (var t in toks)
                per.Add("(" + string.Join(" OR ", cols.Select(c => c + ":(" + t + ")")) + ")");
            return "(" + string.Join(" AND ", per) + ")";
        }

        static string BuildFtsSegmentedAnd(List<string> cols, IEnumerable<string> segments)
        {
            if (cols.Count == 0)
                return string.Empty;

            var segExprs = new List<string>();
            foreach (var seg in segments)
            {
                if (string.IsNullOrWhiteSpace(seg))
                    continue;

                var grams = TextNormalization.ToNGramTokens(seg);
                if (string.IsNullOrWhiteSpace(grams))
                {
                    if (seg.Length == 1)
                    {
                        var tok = SanitizeTok(seg);
                        if (!string.IsNullOrWhiteSpace(tok))
                            grams = tok;
                    }
                }

                var toks = string.IsNullOrEmpty(grams)
                    ? Array.Empty<string>()
                    : grams.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    );

                if (toks.Length > 0)
                {
                    bool hasTri = toks.Any(t => t.Length == 3);
                    if (hasTri)
                    {
                        var filtered = toks.Where(t => t.Length == 3).Take(10).ToArray();
                        if (filtered.Length > 0)
                            toks = filtered;
                    }
                }

                if (toks.Length == 0)
                    continue;

                var expr = BuildPerSegmentAllGrams(cols, toks);
                if (!string.IsNullOrWhiteSpace(expr))
                    segExprs.Add(expr);
            }

            if (segExprs.Count == 0)
                return string.Empty;
            return string.Join(" AND ", segExprs);
        }

        string matchNorm;
        string matchLoose;
        if (andMode && qStrict.IndexOf(' ') >= 0)
        {
            var segmentsStrict = qStrict.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            matchNorm = BuildFtsSegmentedAnd(ftsNormCols, segmentsStrict);

            var segmentsLoose = segmentsStrict.Select(s =>
                TextNormalization.NormalizeLooseFromStrict(s)
            );
            matchLoose = BuildFtsSegmentedAnd(ftsLooseCols, segmentsLoose);

            if (string.IsNullOrWhiteSpace(matchNorm))
                matchNorm = BuildFtsAnd(ftsNormCols, normTokens);
            if (string.IsNullOrWhiteSpace(matchLoose))
                matchLoose = BuildFtsAnd(ftsLooseCols, looseTokens);
        }
        else
        {
            bool nameOnly = useName && !useDesc && !useReg && !useId;

            static bool IsCjk(char ch) =>
                (ch >= '\u3040' && ch <= '\u30FF')
                || (ch >= '\u3400' && ch <= '\u9FFF')
                || (ch >= '\uF900' && ch <= '\uFAFF');

            bool isSingle = qStrict.IndexOf(' ') < 0;
            bool isAsciiWord =
                isSingle
                && qStrict.Length >= 8
                && qStrict.All(ch => ch < 128 && (char.IsLetterOrDigit(ch)));

            bool isCjkWord = isSingle && qStrict.Length >= 3 && qStrict.Any(IsCjk);
            bool forcePreciseSingleToken = !andMode && (isAsciiWord || isCjkWord);

            if (shortQuery)
            {
                matchNorm = string.Empty;
                matchLoose = string.Empty;
            }
            else if (!andMode && nameOnly)
            {
                matchNorm = BuildFtsAnd(ftsNormCols, normTokens);
                matchLoose = BuildFtsAnd(ftsLooseCols, looseTokens);
            }
            else if (forcePreciseSingleToken)
            {
                matchNorm = BuildFtsAnd(ftsNormCols, normTokens);
                matchLoose = BuildFtsAnd(ftsLooseCols, looseTokens);
            }
            else
            {
                matchNorm = andMode
                    ? BuildFtsAnd(ftsNormCols, normTokens)
                    : BuildFtsOr(ftsNormCols, normTokens);
                matchLoose = andMode
                    ? BuildFtsAnd(ftsLooseCols, looseTokens)
                    : BuildFtsOr(ftsLooseCols, looseTokens);
            }
        }

        var matchNormEsc = EscapeSqlSingle(matchNorm);
        var matchLooseEsc = EscapeSqlSingle(matchLoose);

        var queryGramSet = new HashSet<string>(normTokens, StringComparer.Ordinal);

        using var conn = cache.OpenStoreConnection();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();

        bool phraseMode = !andMode && qStrict.Contains(' ');
        if (phraseMode)
        {
            enableFts = false;
        }

        string? phraseLike = null;
        if (phraseMode)
        {
            var esc = qStrict.Replace("%", "\\%").Replace("_", "\\_");
            phraseLike = "%" + esc + "%";
        }

        sb.AppendLine("WITH CulturePref AS (");
        for (int i = 0; i < cultures.Count; i++)
        {
            if (i > 0)
                sb.AppendLine("UNION ALL");
            sb.Append("SELECT ")
                .Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(" AS prio, @c")
                .Append(i)
                .Append(" AS culture")
                .AppendLine();
        }

        sb.AppendLine(") , ExactPrefix AS (");
        sb.AppendLine(
            "  SELECT NULL AS id, NULL AS culture, '' AS display_name, -1 AS score WHERE 1=0"
        );

        if (useId)
        {
            sb.AppendLine("  UNION ALL");
            sb.AppendLine(
                "  SELECT p.id, s.culture, s.display_name, 1200 FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id WHERE p.policy_name=@q_exact AND s.culture IN (SELECT culture FROM CulturePref)"
            );
            sb.AppendLine("  UNION ALL");
            sb.AppendLine(
                "  SELECT p.id, s.culture, s.display_name, 300 FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id WHERE p.policy_name LIKE @q_prefix AND s.culture IN (SELECT culture FROM CulturePref)"
            );
        }

        if (useReg)
        {
            sb.AppendLine("  UNION ALL");
            sb.AppendLine(
                "  SELECT p.id, s.culture, s.display_name, 1000 FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id WHERE (p.hive||'\\\\'||p.reg_key||'\\\\'||p.reg_value)=@q_exact AND s.culture IN (SELECT culture FROM CulturePref)"
            );
        }

        sb.AppendLine(") , Phrase AS (");
        if (phraseMode)
        {
            sb.AppendLine(
                "  SELECT p.id, s.culture, s.display_name, 160 AS score FROM Policies p JOIN PolicyI18n s ON s.policy_id=p.id WHERE s.culture IN (SELECT culture FROM CulturePref) AND ("
            );
            sb.AppendLine(
                "      LOWER(s.display_name) LIKE @phraseLike ESCAPE '\\' OR LOWER(s.explain_text) LIKE @phraseLike ESCAPE '\\'"
            );
            sb.AppendLine("  )");
        }
        else
        {
            sb.AppendLine(
                "  SELECT NULL AS id, NULL AS culture, '' AS display_name, -1 AS score WHERE 1=0"
            );
        }

        sb.AppendLine(") , F1 AS (");
        sb.AppendLine(
            "  SELECT m.policy_id AS id, m.culture, s.display_name, 100 AS score FROM PolicyIndex JOIN PolicyIndexMap m ON m.rowid=PolicyIndex.rowid JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=m.culture WHERE @enableFts=1 AND m.culture IN (SELECT culture FROM CulturePref)"
        );
        if (enableFts && ftsNormCols.Count > 0 && !string.IsNullOrWhiteSpace(matchNorm))
            sb.Append("    AND PolicyIndex MATCH '").Append(matchNormEsc).AppendLine("'");
        else
            sb.AppendLine("    AND (0)");

        sb.AppendLine(") , F2 AS (");
        sb.AppendLine(
            "  SELECT m.policy_id AS id, m.culture, s.display_name, 60 AS score FROM PolicyIndex JOIN PolicyIndexMap m ON m.rowid=PolicyIndex.rowid JOIN PolicyI18n s ON s.policy_id=m.policy_id AND s.culture=m.culture WHERE @enableFts=1 AND m.culture IN (SELECT culture FROM CulturePref)"
        );
        if (enableFts && ftsLooseCols.Count > 0 && !string.IsNullOrWhiteSpace(matchLoose))
            sb.Append("    AND PolicyIndex MATCH '").Append(matchLooseEsc).AppendLine("'");
        else
            sb.AppendLine("    AND (0)");

        sb.AppendLine(") , Candidates AS (");
        sb.AppendLine("  SELECT id,culture,display_name,score FROM ExactPrefix");
        sb.AppendLine("  UNION ALL SELECT id,culture,display_name,score FROM Phrase");
        sb.AppendLine("  UNION ALL SELECT id,culture,display_name,score FROM F1");
        sb.AppendLine("  UNION ALL SELECT id,culture,display_name,score FROM F2");

        sb.AppendLine(") , Filtered AS (");
        sb.AppendLine("  SELECT c.* FROM Candidates c");
        sb.AppendLine("  WHERE c.culture = @primaryCulture");
        sb.AppendLine("     OR (@secondCulture IS NOT NULL AND c.culture = @secondCulture)");
        sb.AppendLine("     OR ( c.culture <> @primaryCulture");
        sb.AppendLine("          AND (@secondCulture IS NULL OR c.culture <> @secondCulture)");
        sb.AppendLine(
            "          AND NOT EXISTS (SELECT 1 FROM PolicyI18n px WHERE px.policy_id = c.id AND px.culture = @primaryCulture)"
        );
        sb.AppendLine(
            "          AND ( @secondCulture IS NULL OR NOT EXISTS (SELECT 1 FROM PolicyI18n sx WHERE sx.policy_id = c.id AND sx.culture = @secondCulture) )"
        );
        sb.AppendLine("        )");

        sb.AppendLine(") , Ranked AS (");
        sb.AppendLine(
            "  SELECT c.id,c.culture,c.display_name,c.score,cp.prio, ROW_NUMBER() OVER (PARTITION BY c.id ORDER BY cp.prio ASC) AS rnk FROM Filtered c JOIN CulturePref cp ON cp.culture=c.culture"
        );

        sb.AppendLine(
            ") SELECT R.id,R.culture, (SELECT ns||':'||policy_name FROM Policies P WHERE P.id=R.id) AS unique_id,"
        );
        sb.AppendLine("       R.score, R.display_name,");
        sb.AppendLine(
            "       (SELECT hive||'\\\\'||reg_key||'\\\\'||reg_value FROM Policies P2 WHERE P2.id=R.id) AS registry_path,"
        );
        sb.AppendLine(
            "       (SELECT product_hint FROM Policies P3 WHERE P3.id=R.id) AS product_hint,"
        );
        sb.AppendLine(
            "       (SELECT value_type FROM Policies P4 WHERE P4.id=R.id) AS value_type,"
        );
        sb.AppendLine(
            "       (SELECT explain_text FROM PolicyI18n PX WHERE PX.policy_id=R.id AND PX.culture=R.culture) AS explain_text"
        );
        sb.AppendLine("  FROM Ranked R WHERE R.rnk = 1 ORDER BY R.score DESC LIMIT @limit; ");

        int internalLimit = limit;
        if (useDesc && (useName || useId || useReg))
        {
            try
            {
                checked
                {
                    internalLimit = Math.Min(limit * 3, 300);
                }
            }
            catch
            {
                internalLimit = Math.Min(limit * 2, 300);
            }
        }

        var hits = new List<(PolicyHit Hit, bool Phrase)>(Math.Min(internalLimit, 512));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@q_exact", qExact);
        cmd.Parameters.AddWithValue("@q_prefix", qPrefix);
        cmd.Parameters.AddWithValue("@limit", internalLimit);
        cmd.Parameters.AddWithValue("@enableFts", enableFts ? 1 : 0);

        if (phraseMode && phraseLike != null)
        {
            var pPhrase = cmd.CreateParameter();
            pPhrase.ParameterName = "@phraseLike";
            pPhrase.Value = phraseLike;
            cmd.Parameters.Add(pPhrase);
        }

        for (int i = 0; i < cultures.Count; i++)
            cmd.Parameters.AddWithValue(
                "@c" + i,
                AdmxCacheCulture.NormalizeCultureName(cultures[i])
            );

        var primaryCulture =
            cultures.Count > 0
                ? AdmxCacheCulture.NormalizeCultureName(cultures[0])
                : CultureInfo.CurrentUICulture.Name;
        string? secondCulture =
            cultures.Count > 1 ? AdmxCacheCulture.NormalizeCultureName(cultures[1]) : null;
        if (
            secondCulture != null
            && string.Equals(secondCulture, primaryCulture, StringComparison.OrdinalIgnoreCase)
        )
        {
            secondCulture = null;
        }

        cmd.Parameters.AddWithValue("@primaryCulture", primaryCulture);
        var pSecond = cmd.CreateParameter();
        pSecond.ParameterName = "@secondCulture";
        pSecond.Value = secondCulture == null ? DBNull.Value : secondCulture;
        cmd.Parameters.Add(pSecond);

        using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await rdr.ReadAsync(ct).ConfigureAwait(false))
        {
            var pid = rdr.GetInt64(0);
            var cul = rdr.GetString(1);
            var uid = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
            var score = rdr.GetDouble(3);
            var dname = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4);
            var reg = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5);
            var prod = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6);
            var vtype = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7);
            var explainText = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8);

            bool singleToken = qStrict.IndexOf(' ') < 0;
            bool singleAsciiLong =
                singleToken
                && qStrict.Length >= 3
                && qStrict.All(ch => ch < 128 && (char.IsLetterOrDigit(ch)));
            bool singleCjkLong =
                singleToken
                && qStrict.Length >= 3
                && qStrict.Any(ch =>
                    (ch >= '\u3040' && ch <= '\u30FF')
                    || (ch >= '\u3400' && ch <= '\u9FFF')
                    || (ch >= '\uF900' && ch <= '\uFAFF')
                );

            if ((singleAsciiLong || singleCjkLong))
            {
                var qLowerSub = qStrict;
                bool hitId = false,
                    hitName = false,
                    hitReg = false,
                    hitDesc = false;

                if (
                    useId
                    && !string.IsNullOrEmpty(uid)
                    && uid.IndexOf(qLowerSub, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    hitId = true;
                if (
                    useName
                    && !string.IsNullOrEmpty(dname)
                    && dname.IndexOf(qLowerSub, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    hitName = true;
                if (
                    useReg
                    && !string.IsNullOrEmpty(reg)
                    && reg.IndexOf(qLowerSub, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    hitReg = true;
                if (
                    useDesc
                    && !string.IsNullOrEmpty(explainText)
                    && explainText.IndexOf(qLowerSub, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    hitDesc = true;

                if (!(hitId || hitName || hitReg || hitDesc))
                {
                    continue;
                }

                if (hitDesc && !(hitId || hitName || hitReg))
                {
                    bool ascii = qStrict.All(ch => ch < 128 && (char.IsLetterOrDigit(ch)));
                    if ((ascii && qStrict.Length >= 4) || (!ascii && qStrict.Length >= 3))
                        continue;
                }
            }

            bool boostAscii =
                qStrict.IndexOf(' ') < 0
                && qStrict.Length >= 8
                && qStrict.All(ch => ch < 128 && (char.IsLetterOrDigit(ch)));
            bool boostCjk =
                qStrict.IndexOf(' ') < 0
                && qStrict.Length >= 3
                && qStrict.Any(ch =>
                    (ch >= '\u3040' && ch <= '\u30FF')
                    || (ch >= '\u3400' && ch <= '\u9FFF')
                    || (ch >= '\uF900' && ch <= '\uFAFF')
                );

            if (boostAscii || boostCjk)
            {
                var qLower = qStrict.ToLowerInvariant();
                if (!string.IsNullOrEmpty(uid) && uid.ToLowerInvariant().Contains(qLower))
                    score += 240;
                else if (!string.IsNullOrEmpty(dname) && dname.ToLowerInvariant().Contains(qLower))
                    score += 140;
            }

            if (score >= 20 && score <= 400)
            {
                var qLower2 = qStrict;
                bool idHit =
                    !string.IsNullOrEmpty(uid)
                    && uid.IndexOf(qLower2, StringComparison.OrdinalIgnoreCase) >= 0;
                bool nameHit =
                    !string.IsNullOrEmpty(dname)
                    && dname.IndexOf(qLower2, StringComparison.OrdinalIgnoreCase) >= 0;
                bool regHit =
                    !string.IsNullOrEmpty(reg)
                    && reg.IndexOf(qLower2, StringComparison.OrdinalIgnoreCase) >= 0;
                bool descHit =
                    !string.IsNullOrEmpty(explainText)
                    && explainText.IndexOf(qLower2, StringComparison.OrdinalIgnoreCase) >= 0;

                bool regValueHit = false;
                if (regHit)
                {
                    try
                    {
                        var parts = reg.Split('\\');
                        if (parts.Length > 0)
                        {
                            var last = parts[^1];
                            if (
                                !string.IsNullOrEmpty(last)
                                && last.IndexOf(qLower2, StringComparison.OrdinalIgnoreCase) >= 0
                            )
                                regValueHit = true;
                        }
                    }
                    catch { }
                }

                int add = 0;
                if (idHit || nameHit)
                    add = 55;
                else if (regValueHit)
                    add = 40;
                else if (regHit)
                    add = 28;
                else if (descHit)
                    add = 12;

                score += add;

                try
                {
                    static bool IsAsciiLetterOrDigit(char c) =>
                        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

                    static bool WordBoundaryContains(string src, string needle)
                    {
                        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(needle))
                            return false;
                        var lowerSrc = src.ToLowerInvariant();
                        var lowerNeedle = needle.ToLowerInvariant();
                        int idx = 0;
                        while (
                            (idx = lowerSrc.IndexOf(lowerNeedle, idx, StringComparison.Ordinal))
                            >= 0
                        )
                        {
                            bool startOk = idx == 0 || !IsAsciiLetterOrDigit(lowerSrc[idx - 1]);
                            int endPos = idx + lowerNeedle.Length;
                            bool endOk =
                                endPos >= lowerSrc.Length
                                || !IsAsciiLetterOrDigit(lowerSrc[endPos]);
                            if (startOk && endOk)
                                return true;
                            idx = idx + 1;
                        }
                        return false;
                    }

                    int nameOverlap = 0;
                    int descOverlap = 0;
                    if (!string.IsNullOrEmpty(dname))
                    {
                        var dnStrict = TextNormalization.NormalizeStrict(dname);
                        var dnTok = TextNormalization.ToNGramTokens(dnStrict);
                        if (!string.IsNullOrWhiteSpace(dnTok))
                        {
                            foreach (
                                var t in dnTok.Split(
                                    ' ',
                                    StringSplitOptions.RemoveEmptyEntries
                                        | StringSplitOptions.TrimEntries
                                )
                            )
                                if (t.Length > 0 && queryGramSet.Contains(t))
                                    nameOverlap++;
                        }
                    }

                    if (!string.IsNullOrEmpty(explainText))
                    {
                        var exStrict = TextNormalization.NormalizeStrict(explainText);
                        var exTok = TextNormalization.ToNGramTokens(exStrict);
                        if (!string.IsNullOrWhiteSpace(exTok))
                        {
                            foreach (
                                var t in exTok.Split(
                                    ' ',
                                    StringSplitOptions.RemoveEmptyEntries
                                        | StringSplitOptions.TrimEntries
                                )
                            )
                                if (t.Length > 0 && queryGramSet.Contains(t))
                                    descOverlap++;
                        }
                    }

                    if (nameOverlap > 0 || descOverlap > 0)
                    {
                        if (nameOverlap > 0)
                            score += Math.Min(60, nameOverlap * 6);
                        if (descOverlap > 0)
                            score += Math.Min(20, descOverlap * 2);

                        if (
                            WordBoundaryContains(dname ?? string.Empty, qLower2)
                            || WordBoundaryContains(uid ?? string.Empty, qLower2)
                        )
                            score += 35;

                        if (!idHit && !nameHit && nameOverlap == 0 && descOverlap > 0 && !regHit)
                            score -= 35;
                    }
                }
                catch { }
            }

            var hit = new PolicyHit(
                pid,
                cul,
                uid ?? string.Empty,
                dname ?? string.Empty,
                reg ?? string.Empty,
                prod ?? string.Empty,
                vtype ?? string.Empty,
                score
            );

            bool phraseHit = false;
            if (phraseMode)
            {
                if (
                    useName
                    && !string.IsNullOrEmpty(dname)
                    && dname.IndexOf(qStrict, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    phraseHit = true;
                if (
                    !phraseHit
                    && useDesc
                    && !string.IsNullOrEmpty(explainText)
                    && explainText.IndexOf(qStrict, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    phraseHit = true;
                if (
                    !phraseHit
                    && useId
                    && !string.IsNullOrEmpty(uid)
                    && uid.IndexOf(qStrict, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    phraseHit = true;
                if (
                    !phraseHit
                    && useReg
                    && !string.IsNullOrEmpty(reg)
                    && reg.IndexOf(qStrict, StringComparison.OrdinalIgnoreCase) >= 0
                )
                    phraseHit = true;
            }

            hits.Add((hit, phraseHit));
        }

        var finalList = new List<PolicyHit>(limit);
        if (phraseMode)
        {
            var phraseHits = hits.Where(h => h.Phrase)
                .Select(h => h.Hit)
                .OrderByDescending(h => h.Score)
                .ThenBy(h => h.UniqueId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (phraseHits.Count > 0)
            {
                foreach (var h in phraseHits.Take(limit))
                    finalList.Add(h);

                if (finalList.Count < limit)
                {
                    var remaining = hits.Where(h => !h.Phrase).Select(h => h.Hit).ToList();
                    SearchOrdering.AppendPriorityOrdered(remaining, finalList, limit, qStrict);
                }

                try
                {
                    bool nameOnlySelected = useName && !useDesc && !useReg && !useId;
                    if (nameOnlySelected && !string.IsNullOrWhiteSpace(qExact))
                    {
                        var qNormAll = SearchText.Normalize(qExact);
                        var tokens = qNormAll
                            .Split(
                                new[] { ' ' },
                                StringSplitOptions.RemoveEmptyEntries
                                    | StringSplitOptions.TrimEntries
                            )
                            .Distinct(StringComparer.Ordinal)
                            .ToArray();
                        if (tokens.Length > 0)
                        {
                            finalList = finalList
                                .Where(h =>
                                {
                                    var dn = h.DisplayName ?? string.Empty;
                                    if (string.IsNullOrWhiteSpace(dn))
                                        return false;
                                    var dnNorm = SearchText.Normalize(dn);
                                    foreach (var t in tokens)
                                        if (!dnNorm.Contains(t, StringComparison.Ordinal))
                                            return false;
                                    return true;
                                })
                                .ToList();
                        }
                    }
                }
                catch { }

                return finalList;
            }
        }

        SearchOrdering.AppendPriorityOrdered(hits.Select(h => h.Hit), finalList, limit, qStrict);

        try
        {
            bool nameOnlySelected2 = useName && !useDesc && !useReg && !useId;
            if (nameOnlySelected2 && !string.IsNullOrWhiteSpace(qExact))
            {
                var qNormAll = SearchText.Normalize(qExact);
                var tokens = qNormAll
                    .Split(
                        new[] { ' ' },
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (tokens.Length > 0)
                {
                    finalList = finalList
                        .Where(h =>
                        {
                            var dn = h.DisplayName ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(dn))
                                return false;
                            var dnNorm = SearchText.Normalize(dn);
                            foreach (var t in tokens)
                                if (!dnNorm.Contains(t, StringComparison.Ordinal))
                                    return false;
                            return true;
                        })
                        .ToList();
                }
            }
        }
        catch { }

        if (shortQuery)
        {
            var qLower = qStrict;
            finalList = finalList
                .Where(h =>
                {
                    string dn = h.DisplayName ?? string.Empty;
                    string id = h.UniqueId ?? string.Empty;
                    string reg = h.RegistryPath ?? string.Empty;

                    bool Accept(string s)
                    {
                        if (string.IsNullOrEmpty(s))
                            return false;
                        int idx = s.IndexOf(qLower, StringComparison.OrdinalIgnoreCase);
                        if (idx < 0)
                            return false;
                        if (idx == 0)
                            return true;
                        char prev = s[idx - 1];
                        return !char.IsLetterOrDigit(prev);
                    }

                    if (Accept(dn) || Accept(id) || Accept(reg))
                        return true;
                    return false;
                })
                .ToList();
        }

        try
        {
            var normQuery = SearchText.Normalize(query);
            var tokens = andMode
                ? normQuery.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                : new[] { normQuery };

            static int ScoreMatchLocal(string textLower, string qLower)
            {
                if (string.IsNullOrEmpty(qLower))
                    return 0;
                if (string.Equals(textLower, qLower, StringComparison.Ordinal))
                    return 100;
                if (textLower.StartsWith(qLower, StringComparison.Ordinal))
                    return 60;
                int idx = textLower.IndexOf(qLower, StringComparison.Ordinal);
                if (idx > 0)
                {
                    char prev = textLower[idx - 1];
                    if (!char.IsLetterOrDigit(prev))
                        return 40;
                    return 20;
                }
                return -1000;
            }

            string Norm(string s) => SearchText.Normalize(s);

            bool useNameF = useName;
            bool useIdF = useId;
            bool useRegF = useReg;
            bool useDescF = useDesc;

            var rescored = new List<(PolicyHit Hit, int Score)>(finalList.Count);
            foreach (var h in finalList)
            {
                string nameN = useNameF ? Norm(h.DisplayName ?? string.Empty) : string.Empty;
                string idN = useIdF ? Norm(h.UniqueId ?? string.Empty) : string.Empty;
                string regN = useRegF ? Norm(h.RegistryPath ?? string.Empty) : string.Empty;

                bool tokenMiss = false;
                int aggregate = 0;
                foreach (var t in tokens)
                {
                    int best = -1000;
                    if (useNameF)
                        best = Math.Max(best, ScoreMatchLocal(nameN, t));
                    if (useIdF)
                        best = Math.Max(best, ScoreMatchLocal(idN, t));
                    if (useRegF)
                        best = Math.Max(best, ScoreMatchLocal(regN, t));
                    if (useDescF) { }

                    if (andMode)
                    {
                        if (best <= -1000)
                        {
                            tokenMiss = true;
                            break;
                        }
                        aggregate += Math.Max(0, best);
                    }
                    else
                    {
                        if (best > aggregate)
                            aggregate = best;
                    }
                }

                if (tokenMiss)
                    continue;

                if (shortQuery && aggregate == 20)
                    continue;

                if (!andMode && !shortQuery && aggregate <= -1000)
                    continue;

                rescored.Add((h, aggregate));
            }

            if (rescored.Count > 0)
            {
                finalList = rescored
                    .Select((x, idx) => (x.Hit, x.Score, idx))
                    .OrderByDescending(e => e.Score)
                    .ThenBy(e => e.idx)
                    .Select(e => e.Hit)
                    .Take(limit)
                    .ToList();
            }
        }
        catch { }

        return finalList;
    }
}
