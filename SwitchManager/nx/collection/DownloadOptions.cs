using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.library
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum DownloadOptions
    {
        BaseGameOnly,
        UpdateOnly,
        AllDLC,
        UpdateAndDLC,
        BaseGameAndUpdate,
        BaseGameAndDLC,
        BaseGameAndUpdateAndDLC,
    }
}