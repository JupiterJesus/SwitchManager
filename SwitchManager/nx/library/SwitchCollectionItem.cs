using SwitchManager.io;
using SwitchManager.nx.system;
using SwitchManager.util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SwitchManager.nx.library
{
    /// <summary>
    /// 
    /// </summary>
    [XmlRoot(ElementName = "CollectionItem")]
    public class SwitchCollectionItem : INotifyPropertyChanged
    {
        [XmlIgnore]
        public SwitchTitle Title
        {
            get { return this.title; }
            set { this.title = value; NotifyPropertyChanged("Title"); }
        }
        private SwitchTitle title;

        [XmlElement(ElementName = "Title")]
        public string TitleId { get { return title?.TitleID; } set { if (title != null) title.TitleID = value; } }

        [XmlElement(ElementName = "Key")]
        public string TitleKey { get { return title?.TitleKey; } set { if (title != null) title.TitleKey = value; } }

        [XmlElement(ElementName = "Name")]
        public string TitleName { get { return title?.Name; } set { if (title != null) title.Name = value; } }

        [XmlElement(ElementName = "Version")]
        public uint? Version { get { return title?.Version; } set { if (title != null) title.Version = value; } }

        [XmlElement(ElementName = "Icon")]
        public string Icon { get { return title?.Icon; } set { if (title != null) title.Icon = value; } }

        [XmlElement(ElementName = "Price")]
        public string Price { get { return title?.Price; } set { if (title != null) title.Price = value; } }

        [XmlElement(ElementName = "ProductCode")]
        public string ProductCode { get { return title?.ProductCode; } set { if (title != null) title.ProductCode = value; } }

        [XmlElement(ElementName = "NsuId")]
        public string NsuId { get { return title?.NsuId; } set { if (title != null) title.NsuId = value; } }

        [XmlElement(ElementName = "ProductId")]
        public string ProductId { get { return title?.ProductId; } set { if (title != null) title.ProductId = value; } }

        [XmlElement(ElementName = "SLUG")]
        public string SLUG { get { return title?.SLUG; } set { if (title != null) title.SLUG = value; } }

        [XmlElement(ElementName = "NumPlayers")]
        public uint? NumPlayers { get { return title?.NumPlayers; } set { if (title != null) title.NumPlayers = value; } }

        [XmlElement(ElementName = "Rating")]
        public string Rating { get { return title?.Rating; } set { if (title != null) title.Rating = value; } }

        [XmlElement(ElementName = "RatingContent")]
        public string RatingContent { get { return title?.RatingContent; } set { if (title != null) title.RatingContent = value; } }

        [XmlElement(ElementName = "BoxArt")]
        public string BoxArtUrl { get { return title?.BoxArtUrl; } set { if (title != null) title.BoxArtUrl = value; } }

        [XmlElement(ElementName = "Category")]
        public string Category { get { return title?.Category; } set { if (title != null) title.Category = value; } }

        [XmlElement(ElementName = "IsDemo")]
        public bool IsDemo { get { return title?.IsDemo ?? false; } set { if (title != null) title.IsDemo = value; } }

        [XmlElement(ElementName = "HasAmiibo")]
        public bool? HasAmiibo { get { return title?.HasAmiibo; } set { if (title != null) title.HasAmiibo = value; } }

        [XmlElement(ElementName = "HasDLC")]
        public bool? HasDLC { get { return title?.HasDLC; } set { if (title != null) title.HasDLC = value; } }

        [XmlElement(ElementName = "Intro")]
        public string Intro { get { return title?.Intro; } set { if (title != null) title.Intro = value; } }

        [XmlElement(ElementName = "Description")]
        public string Description { get { return title?.Description; } set { if (title != null) title.Description = value; } }
        
        [XmlElement(ElementName = "Region")]
        public string Region { get { return title?.Region; } set { if (title != null) title.Region = value; } }

        [XmlElement(ElementName = "Publisher")]
        public string Publisher { get { return title?.Publisher; } set { if (title != null) title.Publisher = value; } }

        [XmlElement(ElementName = "OfficialSite")]
        public string OfficialSite { get { return title?.OfficialSite; } set { if (title != null) title.OfficialSite = value; } }

        [XmlElement(ElementName = "DisplayVersion")]
        public string DisplayVersion { get { return title?.DisplayVersion; } set { if (title != null) title.DisplayVersion = value; } }

        [XmlElement(ElementName = "Developer")]
        public string Developer { get { return title?.Developer; } set { if (title != null) title.Developer = value; } }

        [XmlElement(ElementName = "ReleaseDate")]
        public DateTime? ReleaseDate { get { return title?.ReleaseDate; } set { if (title != null) title.ReleaseDate = value; } }

        [XmlElement(ElementName = "LatestVersion")]
        public uint? LatestVersion { get { return title?.LatestVersion; } set { if (title != null) title.LatestVersion = value; } }
        
        [XmlElement(ElementName = "RequiredSystemVersion")]
        public long? RequiredSystemVersion { get { return title?.RequiredSystemVersion; } set { if (title != null) title.RequiredSystemVersion = value; } }

        [XmlElement(ElementName = "MasterKeyRevision")]
        public byte? MasterKeyRevision { get { return title?.MasterKeyRevision; } set { if (title != null) title.MasterKeyRevision = value; } }

        [XmlElement(ElementName = "State")]
        public SwitchCollectionState State
        {
            get { return this.state; }
            set { this.state = value; NotifyPropertyChanged("State"); }
        }
        private SwitchCollectionState state;
        
        [XmlElement(ElementName = "Favorite")]
        public bool IsFavorite
        {
            get { return isFavorite; }
            set { this.isFavorite = value; NotifyPropertyChanged("IsFavorite"); }
        }
        private bool isFavorite;

        [XmlElement(ElementName = "Size")]
        public long? Size
        {
            get { return this.size; }
            set { this.size = value; NotifyPropertyChanged("Size"); }
        }
        private long? size;

        [XmlElement(ElementName = "Added")]
        public DateTime? Added { get; set; }

        [XmlElement(ElementName = "Path")]
        public string RomPath
        {
            get { return romPath; }
            set { this.romPath = value; NotifyPropertyChanged("RomPath"); }
        }
        private string romPath;

        [XmlElement(ElementName = "Update")]
        public List<UpdateCollectionItem> Updates { get { return this.updates; } set { this.updates = value; NotifyPropertyChanged("Updates"); } }
        private List<UpdateCollectionItem> updates = new List<UpdateCollectionItem>();

        [XmlIgnore]
        public bool IsOwned
        {
            get { return State == SwitchCollectionState.Owned; }
        }

        [XmlIgnore]
        public bool IsDownloaded
        {
            get { return IsOwned || IsPreloaded || IsUnlockable; }
        }

        [XmlIgnore]
        public bool IsNew
        {
            get { return State == SwitchCollectionState.New || State == SwitchCollectionState.NewNoKey || State == SwitchCollectionState.Unlockable; }
        }

        [XmlIgnore]
        public bool IsAvailable
        {
            get { return IsNew || State == SwitchCollectionState.NotOwned || State == SwitchCollectionState.NoKey; }
        }

        [XmlIgnore]
        public bool IsUnlockable
        {
            get { return State == SwitchCollectionState.Unlockable; }
        }

        [XmlIgnore]
        public bool IsHidden
        {
            get { return State == SwitchCollectionState.Hidden; }
        }

        [XmlIgnore]
        public bool IsPreloadable
        {
            get { return State == SwitchCollectionState.NoKey || State == SwitchCollectionState.NewNoKey; }
        }

        [XmlIgnore]
        public bool IsPreloaded
        {
            get { return State == SwitchCollectionState.Downloaded; }
        }

        #region XML

        public virtual bool ShouldSerializeHasDLC() { return true; }
        public virtual bool ShouldSerializeIsDemo() { return true; }
        public virtual bool ShouldSerializeHasAmiibo() { return true; }
        public virtual bool ShouldSerializeReleaseDate() { return true; }
        public virtual bool ShouldSerializeLatestVersion() { return true; }
        public virtual bool ShouldSerializeNumPlayers() { return true; }

        #endregion

        /// <summary>
        /// Default constructor. I don't like these but XmlSerializer requires it, even though I have NO NO NO
        /// intention of deserializing into this class  (just serializing). Make sure to populate fields if you call
        /// this constructor.
        /// </summary>
        public SwitchCollectionItem()
        {

        }

        public SwitchCollectionItem(SwitchTitle title, SwitchCollectionState state, bool isFavorite)
        {
            this.title = title;
            State = state;
            IsFavorite = isFavorite;
        }

        public SwitchCollectionItem(SwitchTitle title) : this(title, SwitchCollectionState.NotOwned, false)
        {
        }

        public SwitchCollectionItem(SwitchTitle title, bool isFavorite) : this(title, SwitchCollectionState.NotOwned, isFavorite)
        {

        }

        public SwitchCollectionItem(SwitchTitle title, SwitchCollectionState state) : this(title, state, false)
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;


        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        public override string ToString()
        {
            return title?.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is SwitchCollectionItem))
                return false;

            SwitchCollectionItem other = obj as SwitchCollectionItem;
            if (title == null && other.title == null)
                return true;

            if (title == null || other.title == null)
                return false;

            return title.Equals(other.title);
        }

        public override int GetHashCode()
        {
            return title?.GetHashCode() ?? 0;
        }

        internal UpdateCollectionItem GetUpdate(uint version)
        {
            if (Updates == null || Updates.Count == 0) return null;

            foreach (var u in Updates)
            {
                if (u.Version == version) return u;
            }
            return null;
        }

        internal void SetNspFile(string nspFile)
        {
            if (Title.IsTitleKeyValid)
                State = SwitchCollectionState.Owned;
            else
                State = SwitchCollectionState.Downloaded;
            Added = DateTime.Now;
            this.RomPath = nspFile;
            this.Size = FileUtils.GetFileSystemSize(nspFile);
        }
    }
}
