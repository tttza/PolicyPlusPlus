using System.Globalization;
using System.Xml;

namespace PolicyPlusCore.Utils
{
    public static class XmlExtensions
    {
        public static string? AttributeOrNull(this XmlNode node, string name)
        {
            return node?.Attributes?[name]?.Value;
        }

        public static T AttributeOrDefault<T>(this XmlNode node, string name, T @default)
        {
            var attr = node?.Attributes?[name]?.Value;
            if (string.IsNullOrEmpty(attr)) return @default;

            try
            {
                return (T)ConvertFromString(typeof(T), attr, @default!);
            }
            catch
            {
                return @default;
            }
        }

        private static object ConvertFromString(Type t, string value, object fallback)
        {
            // Fast paths for common primitive types
            if (t == typeof(string)) return value;
            if (t == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            if (t == typeof(bool) && bool.TryParse(value, out var b)) return b;
            if (t == typeof(double) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            if (t == typeof(float) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
            if (t == typeof(long) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            if (t.IsEnum)
            {
                try { return Enum.Parse(t, value, ignoreCase: true); } catch { return fallback; }
            }

            // Nullable<T>
            var underlying = Nullable.GetUnderlyingType(t);
            if (underlying != null)
            {
                if (string.IsNullOrWhiteSpace(value)) return null!;
                return ConvertFromString(underlying, value, fallback);
            }

            // DateTime, TimeSpan, Guid, Uri
            if (t == typeof(DateTime) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) return dt;
            if (t == typeof(TimeSpan) && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts)) return ts;
            if (t == typeof(Guid) && Guid.TryParse(value, out var g)) return g;
            if (t == typeof(Uri) && Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri)) return uri;

            return fallback;
        }
    }
}
