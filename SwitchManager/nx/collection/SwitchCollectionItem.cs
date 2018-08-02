using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.collection
{
    public class SwitchCollectionItem
    {
        public SwitchGame Game { get; set; }
        public SwitchCollectionState State { get; set; }
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
        }

        public bool IsFavorite { get; set; }

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
    }
}
