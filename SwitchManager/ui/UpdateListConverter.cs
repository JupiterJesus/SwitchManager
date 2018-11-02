using SwitchManager.nx.library;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Linq;
using System.IO;

namespace SwitchManager.ui
{
    public class UpdateListConverter : IValueConverter
    {
        private VersionsConverter vc = new VersionsConverter();

        public SwitchLibrary Library { get; internal set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<UpdateCollectionItem> list)
            {
                if (list.Count == 0) return "No Updates";

                StringBuilder result = new StringBuilder();
                foreach (var update in list.OrderBy(i => i.Version))
                {
                    string ver = vc.Convert(update.Version, typeof(string), "Updates", CultureInfo.InvariantCulture) as string;
                    string romPath = update.RomPath == null ? "Not Owned" : Path.GetFileName(update.RomPath);

                    result.Append($"{ver:3} {romPath}");

                    if (update.RomPath != null)
                    {
                        if (update.Title.RequiredSystemVersion == null)
                            Library.UpdateInternalMetadata(update.Title).Wait();
                        if (update.Title.RequiredSystemVersion != null)
                            result.Append($" (Requires FW {update.Title.RequiredFirmware})");
                    }

                    result.Append("\r\n");
                }

                return result.ToString();
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
