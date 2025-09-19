using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using PolicyPlusCore.Core;

namespace PolicyPlusPlus.Converters
{
    public sealed partial class CategoryVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is PolicyPlusCategory ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
