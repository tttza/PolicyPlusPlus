using System.Globalization;

namespace PolicyPlusCore.Core.Culture;

internal static class CultureNameNormalization
{
    public static string NormalizeCultureName(string culture)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(culture))
                return CultureInfo.CurrentUICulture.Name;
            return CultureInfo.GetCultureInfo(culture).Name;
        }
        catch
        {
            return culture.Trim();
        }
    }
}
