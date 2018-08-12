using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Title Type stored in CNMT metadata, in the header at offset 0xE. It is one byte long.
    /// It describes what sort of title you are using or downloading, since the CNMT is basically
    /// metadata for the entire title.
    /// </summary>
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum TitleType
    {
        SystemProgram = 0x1, // Built-in system software?
        SystemData = 0x2, // Data isn't usually an app, so how is this different from SystemProgram? Eh, who cares.
        SystemUpdate = 0x3, // Firmware?
        BootImagePackage = 0x4, // There are titles for updating the boot loader?
        BootImagePackageSafe = 0x5, // Don't know how this is different from the last
        Application = 0x80, // A regular old game
        Patch = 0x81, // A game update
        AddOnContent = 0x82, // Game DLC
        Delta = 0x83, // No clue
    }
}
