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

        [XmlElement(ElementName = "Icon")]
        public string Icon { get { return title?.Icon; } set { if (title != null) title.Icon = value; } }

        [XmlElement(ElementName = "Price")]
        public string Price { get { return title?.Price; } set { if (title != null) title.Price = value; } }

        [XmlElement(ElementName = "Code")]
        public string Code { get { return title?.Code; } set { if (title != null) title.Code = value; } }

        [XmlElement(ElementName = "NsuId")]
        public string NsuId { get { return title?.NsuId; } set { if (title != null) title.NsuId = value; } }

        [XmlElement(ElementName = "NumPlayers")]
        public string NumPlayers { get { return title?.NumPlayers; } set { if (title != null) title.NumPlayers = value; } }

        [XmlElement(ElementName = "Rating")]
        public string Rating { get { return title?.Rating; } set { if (title != null) title.Rating = value; } }

        [XmlElement(ElementName = "RatingContent")]
        public string RatingContent { get { return title?.RatingContent; } set { if (title != null) title.RatingContent = value; } }

        [XmlElement(ElementName = "BoxArtUrl")]
        public string BoxArtUrl { get { return title?.BoxArtUrl; } set { if (title != null) title.BoxArtUrl = value; } }

        [XmlElement(ElementName = "Category")]
        public string Category { get { return title?.Category; } set { if (title != null) title.Category = value; } }

        [XmlElement(ElementName = "HasAmiibo")]
        public bool HasAmiibo { get { return title?.HasAmiibo ?? false; } set { if (title != null) title.HasAmiibo = value; } }

        [XmlElement(ElementName = "HasDLC")]
        public bool HasDLC { get { return title?.HasDLC ?? false; } set { if (title != null) title.HasDLC = value; } }

        [XmlElement(ElementName = "Intro")]
        public string Intro { get { return title?.Intro; } set { if (title != null) title.Intro = value; } }

        [XmlElement(ElementName = "Description")]
        public string Description { get { return title?.Description; } set { if (title != null) title.Description = value; } }

        [XmlElement(ElementName = "Publisher")]
        public string Publisher { get { return title?.Publisher; } set { if (title != null) title.Publisher = value; } }

        [XmlElement(ElementName = "Developer")]
        public string Developer { get { return title?.Developer; } set { if (title != null) title.Developer = value; } }

        [XmlElement(ElementName = "ReleaseDate")]
        public DateTime? ReleaseDate { get { return title?.ReleaseDate; } set { if (title != null) title.ReleaseDate = value; } }

        [XmlElement(ElementName = "State")]
        public SwitchCollectionState State
        {
            get { return this.state; }
            set { this.state = value; NotifyPropertyChanged("State"); }
        }
        private SwitchCollectionState state;

        [XmlIgnore]
        public string StateName
        {
            get
            {
                switch (this.State)
                {
                    case SwitchCollectionState.Owned: return "Owned";
                    case SwitchCollectionState.OnSwitch: return "On Switch";
                    case SwitchCollectionState.New: return "New";
                    default: return "Not Owned";
                }
            }
            set
            {
                switch (value)
                {
                    case "Owned": this.state = SwitchCollectionState.Owned; break;
                    case "On Switch": this.state = SwitchCollectionState.OnSwitch; break;
                    case "New": this.state = SwitchCollectionState.New; break;
                    default: this.state = SwitchCollectionState.NotOwned; break;
                }
                NotifyPropertyChanged("StateName");
            }
        }
        
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

        [XmlElement(ElementName = "Path")]
        public string RomPath
        {
            get { return romPath; }
            set
            {
                this.romPath = value;
                NotifyPropertyChanged("RomPath");
                NotifyPropertyChanged("PrettySize");
                NotifyPropertyChanged("Size");
            }
        }
        private string romPath;

        [XmlElement(ElementName = "Updates")]
        public List<UpdateMetadataItem> Updates
        {
            get
            {
                if (this.title?.Type != SwitchTitleType.Game) return null;

                SwitchGame game = this.title as SwitchGame;
                List<UpdateMetadataItem> updates = new List<UpdateMetadataItem>(game?.Updates?.Count ?? 0);
                if (game?.Updates != null)
                {
                    foreach (var update in game.Updates)
                    {
                        var meta = new UpdateMetadataItem();
                        meta.TitleID = update.TitleID;
                        meta.TitleKey = update.TitleKey;
                        meta.Version = update.Version;
                        updates.Add(meta);
                    }
                }
                return updates;
            }
        }

        [XmlIgnore]
        public bool IsOwned
        {
            get { return State == SwitchCollectionState.Owned || State == SwitchCollectionState.OnSwitch; }
        }

        [XmlIgnore]
        public bool IsDownloaded
        {
            get { return IsOwned || State == SwitchCollectionState.Downloaded; }
        }
        
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
    }
}
