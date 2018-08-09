using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SwitchManager.nx.collection
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
        public string TitleId { get { return title?.TitleID; } set {  } }

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
                    case SwitchCollectionState.Downloaded: return "Downloaded";
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
                    case "Downloaded": this.state = SwitchCollectionState.Downloaded; break;
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
        public ulong Size { get; set; }

        [XmlElement(ElementName = "Path")]
        public string RomPath { get; set; }

        /// <summary>
        /// Default constructor. I don't like these but XmlSerializer requires it, even though I have NO NO NO
        /// intention of deserializing into this class  (just serializing). Make sure to populate fields if you call
        /// this constructor.
        /// </summary>
        public SwitchCollectionItem()
        {

        }

        public SwitchCollectionItem(string name, string titleid, string titlekey, SwitchCollectionState state, bool isFavorite)
        {
            Title = new SwitchTitle(name, titleid, titlekey);
            State = state;
            IsFavorite = isFavorite;
        }

        public SwitchCollectionItem(string name, string titleid, string titlekey, bool isFavorite) : this(name, titleid, titlekey, SwitchCollectionState.NotOwned, isFavorite)
        {

        }

        public SwitchCollectionItem(string name, string titleid, string titlekey, SwitchCollectionState state) : this(name, titleid, titlekey, state, false)
        {

        }

        public SwitchCollectionItem(string name, string titleid, string titlekey) : this(name, titleid, titlekey, SwitchCollectionState.NotOwned, false)
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;


        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }
}
