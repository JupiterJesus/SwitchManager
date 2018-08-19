using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Languages supported by the switch. The name of the enum and the number/index are both important.
    /// </summary>
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum SwitchLanguage
    {
        AmericanEnglish,
        BritishEnglish,
        Japanese,
        French,
        German,
        LatinAmericanSpanish,
        Spanish,
        Italian,
        Dutch,
        CanadianFrench,
        Portuguese,
        Russian,
        Korean,
        Taiwanese,
        Chinese,

    }
}
