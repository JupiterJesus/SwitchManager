using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.system
{
    /// <summary>
    /// Represents a metadata entry in a cnmt file 
    /// See http://switchbrew.org/index.php?title=NCA
    /// </summary>
    public class CnmtMetaEntry
    {
        public string TitleID { get; set; }
        public uint Version { get; set; }
        public TitleType Type { get; set; }
        public byte Flag { get; set; }
        public ushort Unknown { get; set; }
    }
}
