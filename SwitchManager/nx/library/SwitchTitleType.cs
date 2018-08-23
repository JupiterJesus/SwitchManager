using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.library
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum SwitchTitleType
    {
        Unknown,
        Demo,
        Game,
        Update,
        DLC,
    }
}
