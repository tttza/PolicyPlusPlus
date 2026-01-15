using PolicyPlusCore.Core.Culture;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheCulture
{
    public static string NormalizeCultureName(string culture)
    {
        return CultureNameNormalization.NormalizeCultureName(culture);
    }
}
