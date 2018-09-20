using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.library
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum SwitchCollectionState
    {
        [Description("Available")]
        NotOwned,

        [Description("Owned")]
        Owned,

        [Description("On Switch")]
        OnSwitch,

        [Description("New Title")]
        New,

        [Description("Hidden")]
        Hidden,

        [Description("Preloadable")]
        NoKey,

        [Description("New Preloadable")]
        NewNoKey,

        [Description("Preloaded")]
        Downloaded,

        [Description("Unlockable")]
        Unlockable,
    }
}
