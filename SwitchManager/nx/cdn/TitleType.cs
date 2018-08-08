using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Title Type stored in CNMT metadata, in the header at offset 0xE. It is one byte long.
    /// </summary>
    public enum TitleType
    {
        SystemProgram = 0x1,
        SystemData = 0x2,
        SystemUpdate = 0x3,
        BootImagePackage = 0x4,
        BootImagePackageSafe = 0x5,
        Application = 0x80,
        Patch = 0x81,
        AddOnContent = 0x82,
        Delta = 0x83,
    }
}
