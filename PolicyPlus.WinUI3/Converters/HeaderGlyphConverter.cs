using Microsoft.UI.Xaml.Data;
using System;

namespace PolicyPlus.WinUI3.Converters
{
    public sealed partial class HeaderGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var text = value as string ?? string.Empty;
            return text.StartsWith("User", StringComparison.OrdinalIgnoreCase) ? "\uE77B" :
                   text.StartsWith("Computer", StringComparison.OrdinalIgnoreCase) ? "\uE211" : string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
}
