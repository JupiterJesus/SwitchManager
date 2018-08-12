using SwitchManager.util;
using System.ComponentModel;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Describes the "type" of an NCA. Every NCA in a game has a type, which is coded into headers and
    /// describes what sort of content it contains.
    /// </summary>
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum NCAType
    {
        Meta, // Unknown to me, but probably some sort of metadata
        Program, // Main executable code
        Data, // Support data like textures and sound?
        Control, // Unknown, but I do know title icons are in here
        HtmlDocument, // HTML docs, maybe a manual or docs incorporated in the game?
        LegalInformation, // Legal/license bs, don't know if it is just included on the side or if it is actual content
        DeltaFragment, // No idea
    }
}
