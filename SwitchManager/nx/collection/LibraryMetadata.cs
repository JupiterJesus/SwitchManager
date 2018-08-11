using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SwitchManager.nx.library
{
    [XmlRoot(ElementName = "Library")]
    public class LibraryMetadata
    {
        [XmlElement(ElementName = "CollectionItem")]
        public LibraryMetadataItem[] Items { get; set; }
    }

    [XmlRoot(ElementName = "CollectionItem")]
    public class LibraryMetadataItem
    {
        [XmlElement(ElementName = "Title")]
        public string TitleID { get; set; }

        [XmlElement(ElementName = "State")]
        public SwitchCollectionState State { get; set; }

        [XmlElement(ElementName = "Favorite")]
        public bool IsFavorite { get; set; }

        [XmlElement(ElementName = "Path")]
        public string Path { get; set; }
    }
}
