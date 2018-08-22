using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SwitchManager.ui
{
    public class TextInputToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Always test MultiValueConverter inputs for non-null 
            // (to avoid crash bugs for views in the designer) 
            if (value is bool)
            {
                bool hasText = !(bool)value;
                if (hasText)
                    return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
