using System;
using Microsoft.UI.Xaml.Data;

namespace PolicyPlusPlus.Converters
{
    public sealed partial class BoolPairToGlyphConverter : IValueConverter
    {
        // Maps (enabled, disabled) -> glyph
        // enabled=true  -> check glyph E73E
        // disabled=true -> block glyph E711
        // neither -> empty string
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ValueTuple<bool, bool> tup)
            {
                if (tup.Item1)
                    return "\uE73E"; // Accept
                if (tup.Item2)
                    return "\uE711"; // Block/Cancel
                return string.Empty;
            }
            return string.Empty;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            string language
        ) => throw new NotSupportedException();
    }
}
