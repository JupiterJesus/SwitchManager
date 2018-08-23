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
            if (value is string str)
            {
                bool hasText = !string.IsNullOrWhiteSpace(str);
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
