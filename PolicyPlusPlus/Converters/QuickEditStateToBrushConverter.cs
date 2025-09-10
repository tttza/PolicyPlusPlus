using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using PolicyPlusPlus.ViewModels;

namespace PolicyPlusPlus.Converters
{
    public sealed partial class QuickEditStateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var state = value is QuickEditState qs ? qs : QuickEditState.NotConfigured;
            string key = state switch
            {
                QuickEditState.Enabled => "StateEnabledBrush",
                QuickEditState.Disabled => "StateDisabledBrush",
                _ => "StateNotConfiguredBrush"
            };
            // Attempt to resolve from Application resources (handles theme dictionaries)
            if (Application.Current.Resources.TryGetValue(key, out object brush) && brush is Brush b)
            {
                return b;
            }
            // Fallback neutral background if not found
            return new SolidColorBrush(Microsoft.UI.Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
}
