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
        private SwitchCollectionState state;
        private bool isFavorite;

        public SwitchGame Game { get; set; }
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
                    case SwitchCollectionState.DOWNLOADED: return "Downloaded";
                    case SwitchCollectionState.OWNED: return "Owned";
                    case SwitchCollectionState.ON_SWITCH: return "On Switch";
                    case SwitchCollectionState.NEW: return "New";
                    default: return "Not Owned";
                }
            }
            set
            {
                switch (value)
                {
                    case "Downloaded": this.state = SwitchCollectionState.DOWNLOADED; break;
                    case "Owned": this.state = SwitchCollectionState.OWNED; break;
                    case "On Switch": this.state = SwitchCollectionState.ON_SWITCH; break;
                    case "New": this.state = SwitchCollectionState.NEW; break;
                    default: this.state = SwitchCollectionState.NOT_OWNED; break;
                }
                NotifyPropertyChanged("StateName");
            }
        }

        public bool IsFavorite
        {
            get { return isFavorite; }
            set { this.isFavorite = value; NotifyPropertyChanged("IsFavorite"); }
        }

        public SwitchCollectionItem(string name, string titleid, string titlekey, SwitchCollectionState state, bool isFavorite)
        {
            Game = new SwitchGame(name, titleid, titlekey);
            State = state;
            IsFavorite = isFavorite;
        }

        public SwitchCollectionItem(string name, string titleid, string titlekey, bool isFavorite) : this(name, titleid, titlekey, SwitchCollectionState.NOT_OWNED, isFavorite)
        {

        }

        public SwitchCollectionItem(string name, string titleid, string titlekey, SwitchCollectionState state) : this(name, titleid, titlekey, state, false)
        {

        }

        public SwitchCollectionItem(string name, string titleid, string titlekey) : this(name, titleid, titlekey, SwitchCollectionState.NOT_OWNED, false)
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;


        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }
}
