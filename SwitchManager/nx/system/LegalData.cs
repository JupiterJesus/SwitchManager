using log4net;
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
    /// I don't know much about this. There is a file called control.nacp packed into the CONTROL NCA.
    /// It seems to contain metadata like language, version, title id, etc. It is alongside the game's icons,
    /// one for each supported language. The icon languages match the languages within the nacp file.
    /// I have seen some NSPs contain a file that ends in .nacp.xml. I have yet to read one of these files and compare
    /// it to the contents of the control.nacp file.
    /// 
    /// See http://switchbrew.org/index.php?title=Control.nacp.
    /// </summary>
    [XmlRoot("SoftwareLegalInformation", Namespace = null, IsNullable = false)]
    public class LegalData
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(LegalData));

        [XmlElement(ElementName = "OutputDateTime")]
        public DateTime OutputDateTime { get; set; }

        [XmlArray(ElementName = "CopyrightNotations")]
        [XmlArrayItem("TechnologyName")]
        public string[] CopyrightNotations { get; set; }

        [XmlArray(ElementName = "DeclarationRequiredNotations")]
        [XmlArrayItem("TechnologyName")]
        public string[] DeclarationRequiredNotations { get; set; }

        [XmlArray(ElementName = "ProductRegions")]
        [XmlArrayItem("Usa", Type = typeof(UsaRegion))]
        [XmlArrayItem("Japan", Type = typeof(JapanRegion))]
        [XmlArrayItem("Europe", Type = typeof(EuropeRegion))]
        public ProductRegion[] ProductRegions { get; set; }

        [XmlElement(ElementName = "FormatVersion")]
        public string FormatVersion { get; set; }

        [XmlElement(ElementName = "ApplicationId")]
        public string ApplicationId { get; set; }

        [XmlElement(ElementName = "DataHash")]
        public string DataHash { get; set; }

        public LegalData()
        {
        }

        public static LegalData Parse(string file)
        {
            using (var fs = File.OpenRead(file))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    LegalData data = new LegalData();
                    // TODO: Parse legalinfo? or not

                    return data;
                }
            }
        }

        internal void GenerateXml(string legalXmlFile)
        {
            // Create a new file stream to write the serialized object to a file
            using (TextWriter writer = new StreamWriter(legalXmlFile))
            {
                XmlSerializer xmls = new XmlSerializer(typeof(LegalData));
                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                xmls.Serialize(writer, this, ns);
            }

            logger.Info($"Generated XML file {Path.GetFileName(legalXmlFile)}!");
        }

        /// <summary>
        /// Generates a ControlData based on an XML representation of the object.
        /// </summary>
        /// <param name="inFile"></param>
        /// <returns></returns>
        public static LegalData FromXml(string inFile)
        {
            using (TextReader reader = new StreamReader(inFile))
            {
                XmlSerializer xmls = new XmlSerializer(typeof(LegalData));
                LegalData l = xmls.Deserialize(reader) as LegalData;
                return l;
            }
        }

        internal bool SupportsUsa()
        {
            if (ProductRegions != null && ProductRegions.Length > 0)
                foreach (var r in ProductRegions)
                {
                    if (r is UsaRegion)
                        return (r.Supported);
                }
            return false;
        }

        internal bool SupportsEurope()
        {
            if (ProductRegions != null && ProductRegions.Length > 0)
                foreach (var r in ProductRegions)
                {
                    if (r is EuropeRegion)
                        return (r.Supported);
                }
            return false;
        }

        internal bool SupportsJapan()
        {
            if (ProductRegions != null && ProductRegions.Length > 0)
                foreach (var r in ProductRegions)
                {
                    if (r is JapanRegion)
                        return (r.Supported);
                }
            return false;
        }
    }

    public abstract class ProductRegion
    {
        [XmlText]
        public bool Supported { get; set; }
    }

    public sealed class UsaRegion : ProductRegion{ }
    public sealed class JapanRegion : ProductRegion { }
    public sealed class EuropeRegion : ProductRegion { }
}
