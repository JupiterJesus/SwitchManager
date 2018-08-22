using SwitchManager.util;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SwitchManager.ui
{
    /// <summary>
    /// Converts a file size in bytes (long value) into a short file size string whose number is in KB, MB, GB, etc.
    /// </summary>
    public class FileSizeConverter : IValueConverter
    {
        // Converts a long representing a file size in bytes into a string suitable for displaying the file
        // size in KB, MB, GB, and so on.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // File size must be a non-null long value
            if (value is long?)
            {
                long? size = (long?)value;
                if (size.HasValue)
                    return Miscellaneous.ToFileSize(size.Value);
            }
            return "?? bytes";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
