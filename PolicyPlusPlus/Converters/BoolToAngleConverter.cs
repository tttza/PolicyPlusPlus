using Microsoft.UI.Xaml.Data;
using System;

namespace PolicyPlusPlus.Converters
{
    public sealed partial class BoolToAngleConverter : IValueConverter
    {
        // False -> 0 (right), True -> 90 (down)
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b) return b ? 90.0 : 0.0;
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            try
            {
                double angle = System.Convert.ToDouble(value);
                return Math.Abs(angle - 90.0) < 0.1;
            }
            catch { return false; }
        }
    }
}
