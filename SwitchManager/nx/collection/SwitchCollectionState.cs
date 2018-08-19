using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.library
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum SwitchCollectionState
    {
        [Description("Not Owned")]
        NotOwned,

        [Description("Owned")]
        Owned,

        [Description("On Switch")]
        OnSwitch,

        [Description("New Title")]
        New,

        [Description("Hidden")]
        Hidden,
    }
}
