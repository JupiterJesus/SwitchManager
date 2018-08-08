using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.collection
{
    public class SwitchCollectionItem : INotifyPropertyChanged
    {
        private bool isFavorite;

        public SwitchTitle title;
        public SwitchTitle Title
        {
            get { return this.title; }
            set { this.title = value; NotifyPropertyChanged("Title"); }
        }

        private SwitchCollectionState state;
        public SwitchCollectionState State
        {
            get { return this.state; }
            set { this.state = value; NotifyPropertyChanged("State"); }
        }

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

        public bool IsFavorite
        {
            get { return isFavorite; }
            set { this.isFavorite = value; NotifyPropertyChanged("IsFavorite"); }
        }

        public ulong Size { get; set; }

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
