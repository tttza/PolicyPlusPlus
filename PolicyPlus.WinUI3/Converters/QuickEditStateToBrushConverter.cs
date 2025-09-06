using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using PolicyPlus.WinUI3.ViewModels;

namespace PolicyPlus.WinUI3.Converters
{
    public sealed partial class QuickEditStateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var state = value is QuickEditState qs ? qs : QuickEditState.NotConfigured;
            return state switch
            {
                QuickEditState.Enabled => new SolidColorBrush(global::Windows.UI.Color.FromArgb(0x33, 0x28, 0xA7, 0x45)),
                QuickEditState.Disabled => new SolidColorBrush(global::Windows.UI.Color.FromArgb(0x33, 0xD1, 0x37, 0x2A)),
                _ => new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
}
