using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Represents a CNMT content entry
    /// See http://switchbrew.org/index.php?title=NCA
    /// </summary>
    public class CnmtContentEntry
    {
        public byte[] Hash { get; internal set; }
        public string NcaId { get; internal set; }
        public ulong EntrySize { get; internal set; }
        public NCAType NcaType { get; internal set; }
        public byte Unknown { get; internal set; }
    }
}
