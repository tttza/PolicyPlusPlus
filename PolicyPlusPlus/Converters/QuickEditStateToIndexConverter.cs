using System;
using Microsoft.UI.Xaml.Data;
using PolicyPlusPlus.ViewModels;

namespace PolicyPlusPlus.Converters
{
    public sealed partial class QuickEditStateToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value switch
            {
                QuickEditState.NotConfigured => 0,
                QuickEditState.Enabled => 1,
                QuickEditState.Disabled => 2,
                _ => 0,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            int idx = value is int i ? i : 0;
            return idx switch
            {
                1 => QuickEditState.Enabled,
                2 => QuickEditState.Disabled,
                _ => QuickEditState.NotConfigured,
            };
        }
    }
}
