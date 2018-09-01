using SwitchManager.util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SwitchManager.ui
{
    /// <summary>
    /// Converts a Nintendo Switch version from its numberic value (multiples of 0x10000) and the actual version number (0, 1, 2, etc).
    /// </summary>
    public class VersionsConverter : IValueConverter
    {
        // Converts a multiple of 0x10000 (65536) into a more description form, like v0, v1, v2.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                if (value is uint ver)
                {
                    return NumberToString(ver);
                }
               else if (value is ICollection<uint> versions)
                {
                    List<string> newVersions = new List<string>(versions.Count);
                    foreach (var v in versions)
                    {
                        newVersions.Add(NumberToString(v));
                    }
                    return newVersions;
                }
                else if (value is string s)
                {
                    return StringToNumber(s);
                }
                else if (value is ICollection<string> sVersions)
                {
                    List<uint> newVersions = new List<uint>(sVersions.Count);
                    foreach (var vs in sVersions)
                    {
                        uint iVer = StringToNumber(vs);
                        newVersions.Add(iVer);
                    }
                    return newVersions;
                }
            }
            return null;
        }

        private string NumberToString(uint ver)
        {
            ver /= 0x10000;
            return $"v{ver}";
        }

        private uint StringToNumber(string s)
        {
            if (s.StartsWith("v"))
                s = s.Remove(0, 1);

            if (uint.TryParse(s, out uint ver))
            {
                ver *= 0x10000;
                return ver;
            }

            return 0;
        }

        /// <summary>
        /// Converts a version string, like v0, into a multiple of 0x10000.
        /// </summary>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                if (value is string s)
                {
                    return StringToNumber(s);
                }
                else if (value is ICollection<string> sVersions)
                {
                    List<uint> newVersions = new List<uint>(sVersions.Count);
                    foreach (var vs in sVersions)
                    {
                        uint ver = StringToNumber(vs);
                        newVersions.Add(ver);
                    }
                    return newVersions;
                }
                else if (value is uint ver)
                {
                    return NumberToString(ver);
                }
                else if (value is ICollection<uint> iVersions)
                {
                    List<string> newVersions = new List<string>(iVersions.Count);
                    foreach (var v in iVersions)
                    {
                        newVersions.Add(NumberToString(v));
                    }
                    return newVersions;
                }
            }
            return null;
        }
    }
}
