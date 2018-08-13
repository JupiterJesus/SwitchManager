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
        public string TitleId { get { return title?.TitleID; } set { } }

        [XmlElement(ElementName = "Name")]
        public string TitleName { get { return title?.Name; } set { } }

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
        public long Size
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RomPath))
                    return this.size;
                else if (Directory.Exists(RomPath))
                    return DirSize(new DirectoryInfo(RomPath));
                else if (File.Exists(RomPath))
                    return new FileInfo(RomPath).Length;
                else
                    return this.size;
            }
            set { this.size = value; NotifyPropertyChanged("Size"); NotifyPropertyChanged("PrettySize"); }
        }
        private long size;

        [XmlIgnore]
        public string PrettySize {  get { return Miscellaneous.ToFileSize(Size); } }

        private static long DirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            foreach (FileInfo fi in d.EnumerateFiles())
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            foreach (DirectoryInfo di in d.EnumerateDirectories())
            {
                size += DirSize(di);
            }
            return size;
        }

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
