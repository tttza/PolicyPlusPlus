using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PolicyPlusPlus.Converters
{
    public sealed partial class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool val = value is bool b && b;
            if (parameter is string s && s.Equals("Opacity", StringComparison.OrdinalIgnoreCase))
            {
                return val ? 1.0 : 0.15; // used for bookmark faded state
            }
            return val ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility v)
                return v == Visibility.Visible;
            if (
                value is double d
                && parameter is string s
                && s.Equals("Opacity", StringComparison.OrdinalIgnoreCase)
            )
            {
                return d > 0.5;
            }
            return false;
        }
    }
}
