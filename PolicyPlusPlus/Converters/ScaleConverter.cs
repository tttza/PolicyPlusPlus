using Microsoft.UI.Xaml.Data;
using System;

namespace PolicyPlusPlus.Converters
{
    public sealed partial class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d && double.TryParse(parameter?.ToString(), out var scale))
            {
                return d * scale;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
