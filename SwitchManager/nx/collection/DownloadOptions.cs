using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.library
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum DownloadOptions
    {
        [Description("Base Game Only")]
        BaseGameOnly,

        [Description("Update Only")]
        UpdateOnly,

        [Description("DLC Only")]
        AllDLC,

        [Description("Update + DLC Only")]
        UpdateAndDLC,

        [Description("Game + Update")]
        BaseGameAndUpdate,

        [Description("Game + DLC")]
        BaseGameAndDLC,

        [Description("Game + Update + DLC")]
        BaseGameAndUpdateAndDLC,
    }
}