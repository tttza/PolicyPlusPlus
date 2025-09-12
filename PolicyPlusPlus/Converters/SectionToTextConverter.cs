using Microsoft.UI.Xaml.Data;
using PolicyPlusCore.Core;
using System;

namespace PolicyPlusPlus
{
    public sealed partial class SectionToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is AdmxPolicySection s)
            {
                return s switch
                {
                    AdmxPolicySection.Machine => "Computer",
                    AdmxPolicySection.User => "User",
                    _ => "Both"
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
