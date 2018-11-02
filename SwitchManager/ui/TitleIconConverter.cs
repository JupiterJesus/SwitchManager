using SwitchManager.nx.system;
using SwitchManager.util;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SwitchManager.ui
{
    /// <summary>
    /// Given a title, picks an icon to display between the title's metadata icon and the box art url.
    /// </summary>
    public class TitleIconConverter : IMultiValueConverter
    {
        public bool preferBoxArt { get; set; } = true;

        // Gets a title icon
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;
            string boxart = value.Length > 0 ? value[0] as string : null;
            string icon = value.Length > 1 ? value[1] as string : null;
            string filename = null;
            if (preferBoxArt)
            {
                filename = boxart ?? icon;
            }
            else
            {
                filename = icon ?? boxart;
            }
            try
            {
                return filename == null ? null : new BitmapImage(new Uri(filename));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
