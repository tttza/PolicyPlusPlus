using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Utilities;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCachePolicyUpsertService
{
    // Optimized variant using prepared commands created by the caller for batch processing.
    public static async Task UpsertOnePolicyAsync(
        SqliteConnection conn,
        PolicyPlusPolicy pol,
        string culture,
        CancellationToken ct,
        SqliteCommand cmdPolicy,
        SqliteParameter p_ns,
        SqliteParameter p_name,
        SqliteParameter p_cat,
        SqliteParameter p_hive,
        SqliteParameter p_rkey,
        SqliteParameter p_rval,
        SqliteParameter p_vtype,
        SqliteParameter p_ph,
        SqliteCommand cmdI18n,
        SqliteParameter s_pid,
        SqliteParameter s_culture,
        SqliteParameter s_dname,
        SqliteParameter s_desc,
        SqliteParameter s_cat,
        SqliteParameter s_kana,
        SqliteParameter s_pres,
        SqliteCommand cmdIdx,
        SqliteParameter i_tn,
        SqliteParameter i_dn,
        SqliteParameter i_tl,
        SqliteParameter i_dl,
        SqliteParameter i_rp,
        SqliteParameter i_tags,
        SqliteCommand cmdIdxMap,
        SqliteParameter m_rowid,
        SqliteParameter m_pid,
        SqliteParameter m_culture,
        Func<AdmxPolicy, string> inferValueType,
        Func<PolicyPlusCategory?, string> buildCategoryPath,
        Func<string, string?, string?, string> getRegistryPath
    )
    {
        var raw = pol.RawPolicy;
        var ns = raw.DefinedIn.AdmxNamespace;
        var policyName = raw.ID;
        var catKey = pol.Category?.UniqueID ?? string.Empty;
        var hive =
            raw.Section == AdmxPolicySection.Machine ? "HKLM"
            : raw.Section == AdmxPolicySection.User ? "HKCU"
            : string.Empty;
        var regKey = raw.RegistryKey;
        var regVal = raw.RegistryValue;
        var vtype = inferValueType(raw);
        var productHint = pol.SupportedOn?.DisplayName ?? string.Empty;

        p_ns.Value = ns;
        p_name.Value = policyName;
        p_cat.Value = catKey;
        p_hive.Value = hive;
        p_rkey.Value = (object?)regKey ?? DBNull.Value;
        p_rval.Value = (object?)regVal ?? DBNull.Value;
        p_vtype.Value = vtype;
        p_ph.Value = productHint;
        var obj = await cmdPolicy.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var policyId = Convert.ToInt64(obj, CultureInfo.InvariantCulture);

        var dname = pol.DisplayName ?? string.Empty;
        var desc = pol.DisplayExplanation ?? string.Empty;
        var catPath = buildCategoryPath(pol.Category);
        var presJson = pol.Presentation is null ? null : JsonSerializer.Serialize(pol.Presentation);

        if (string.IsNullOrWhiteSpace(dname))
        {
            return; // skip fallback/empty localization persistence
        }

        s_pid.Value = policyId;
        s_culture.Value = culture;
        s_dname.Value = dname;
        s_desc.Value = desc;
        s_cat.Value = catPath;
        s_kana.Value = DBNull.Value;
        if (presJson is null)
            s_pres.Value = DBNull.Value;
        else
            s_pres.Value = Encoding.UTF8.GetBytes(presJson);
        await cmdI18n.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        var dnameStrict = TextNormalization.NormalizeStrict(dname);
        var descStrict = TextNormalization.NormalizeStrict(desc);
        var titleNorm = TextNormalization.ToNGramTokens(dnameStrict);
        var descNorm = TextNormalization.ToNGramTokens(descStrict);
        var titleLoose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(dnameStrict)
        );
        var descLoose = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeLooseFromStrict(descStrict)
        );
        var regPath = TextNormalization.ToNGramTokens(
            TextNormalization.NormalizeStrict(getRegistryPath(hive, regKey, regVal))
        );
        var tags = string.Join(
            ' ',
            new[] { ns, vtype, productHint, policyName }.Where(s0 => !string.IsNullOrWhiteSpace(s0))
        );

        i_tn.Value = titleNorm;
        i_dn.Value = descNorm;
        i_tl.Value = titleLoose;
        i_dl.Value = descLoose;
        i_rp.Value = regPath;
        i_tags.Value = tags;
        var objRow = await cmdIdx.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var rowid = Convert.ToInt64(objRow, CultureInfo.InvariantCulture);

        m_rowid.Value = rowid;
        m_pid.Value = policyId;
        m_culture.Value = culture;
        await cmdIdxMap.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
