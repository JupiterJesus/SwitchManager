using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Represents a CNMT content entry
    /// See http://switchbrew.org/index.php?title=NCA
    /// </summary>
    [Serializable]
    public class CnmtContentEntry
    {
        [XmlElement(ElementName = "Type")]
        public NCAType Type { get; set; }

        private string id;

        [XmlElement(ElementName = "Id")]
        public string Id { get { return id; } set { this.id = value?.ToLower(); } }

        [XmlElement(ElementName = "Size")]
        public long Size { get; set; }

        [XmlElement(ElementName = "Hash")]
        public string HashString
        {
            get { return BitConverter.ToString(HashData).Replace("-", "").ToLower(); }
            set
            {
                if (value.Length != 64) throw new Exception("Coudn't read CNMT Digest from string");
                this.HashData = new byte[value.Length * 2];
                for (int n = 0; n < this.HashData.Length; n++)
                {
                    string byteValue = value.Substring(n * 2, 2);
                    this.HashData[n] = byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        [XmlElement(ElementName = "KeyGeneration")]
        public byte MasterKeyRevision { get; set; }

        [XmlIgnore]
        public byte Unknown { get; internal set; }

        [XmlIgnore]
        public byte[] HashData { get; internal set; }

    }
}
