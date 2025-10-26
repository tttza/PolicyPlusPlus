using System;
using Microsoft.UI.Xaml.Data;

namespace PolicyPlusPlus.Converters
{
    // Returns fallback string when input is null/empty/whitespace; otherwise original string representation.
    public sealed partial class NullOrWhitespaceToFallbackConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var fallback = parameter as string ?? "(no value)";
            if (value is string s)
                return string.IsNullOrWhiteSpace(s) ? fallback : s;
            if (value is null)
                return fallback;
            // Non-string objects: use ToString(), treating empty result as fallback.
            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // One-way usage; just return the incoming value.
            return value;
        }
    }
}
