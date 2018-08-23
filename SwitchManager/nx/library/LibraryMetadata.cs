using System;
using System.Collections.Generic;
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

        [XmlElement(ElementName = "Key")]
        public string TitleKey { get; set; }

        [XmlElement(ElementName = "Name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "Developer")]
        public string Developer { get; set; }

        [XmlElement(ElementName = "Description")]
        public string Description { get; set; }

        [XmlElement(ElementName = "ReleaseDate")]
        public DateTime? ReleaseDate { get; set; }

        [XmlElement(ElementName = "State")]
        public SwitchCollectionState State { get; set; }

        [XmlElement(ElementName = "Favorite")]
        public bool IsFavorite { get; set; }

        [XmlElement(ElementName = "Path")]
        public string Path { get; set; }

        [XmlElement(ElementName = "Size")]
        public long? Size { get; set; }

        [XmlElement(ElementName = "Updates")]
        public List<UpdateMetadataItem> Updates { get; set; }
    }

    [XmlRoot(ElementName = "UpdateItem")]
    public class UpdateMetadataItem
    {
        [XmlElement(ElementName = "Title")]
        public string TitleID { get; set; }

        [XmlElement(ElementName = "Key")]
        public string TitleKey { get; set; }

        [XmlElement(ElementName = "Version")]
        public uint Version { get; set; }
    }
}