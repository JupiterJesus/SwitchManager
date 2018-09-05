using Newtonsoft.Json.Linq;
using SwitchManager.util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SwitchManager.nx.system
{
    /// <summary>
    /// Represents a CNMT content entry
    /// See http://switchbrew.org/index.php?title=NCA
    /// </summary>
    [XmlRoot(ElementName = "Content")]
    public class CnmtContentEntry
    {
        public CnmtContentEntry()
        {

        }

        [XmlElement(ElementName = "Type")]
        public NCAType Type { get; set; }

        [XmlElement(ElementName = "Id")]
        public string Id { get { return id; } set { this.id = value?.ToLower(); } }
        private string id;

        [XmlElement(ElementName = "Size")]
        public long Size { get; set; }

        [XmlElement(ElementName = "Hash")]
        public string HashString
        {
            get { return BitConverter.ToString(HashData).Replace("-", "").ToLower(); }
            set
            {
                if (value.Length != 64) throw new Exception("Coudn't read CNMT Digest from string");
                this.HashData = Miscellaneous.HexToBytes(value);
            }
        }

        [XmlElement(ElementName = "KeyGeneration")]
        public byte MasterKeyRevision { get; set; }

        [XmlElement(ElementName = "IdOffset")]
        public byte IdOffset { get; set; }

        [XmlIgnore]
        public byte[] HashData { get; internal set; }

    }
}
