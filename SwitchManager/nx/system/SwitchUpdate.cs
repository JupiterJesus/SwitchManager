using SwitchManager.nx.library;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.system
{
    public class SwitchUpdate : SwitchTitle
    {
        private string gameid;
        public string GameID
        {
            get { return gameid; }
            set { this.gameid = value; NotifyPropertyChanged("GameID"); }
        }
        private uint version;
        public uint Version
        {
            get { return version; }
            set { this.version = value; NotifyPropertyChanged("Version"); }
        }

        public override bool IsGame => false;
        public override bool IsDLC => false;
        public override bool IsUpdate => true;

        internal SwitchUpdate(string name, string gameid, uint version, string titlekey) : this(name, GetUpdateIDFromBaseGame(gameid), gameid, version, titlekey)
        {

        }

        internal SwitchUpdate(string name, string titleid, string gameid, uint version, string titlekey) : base(name, titleid, titlekey)
        {
            this.gameid = gameid;
            this.version = version;
        }

        internal override SwitchUpdate GetUpdateTitle(uint v, string titlekey = null)
        {
            SwitchUpdate title = new SwitchUpdate(this.Name, this.gameid, v, titlekey);
            
            return title;
        }

        public override string ToString()
        {
            if (TitleID == null && Name == null)
                return "Unknown Title [v" + Version/0x10000 + "]";
            else if (TitleID == null)
                return Name;
            else if (Name == null)
                return "[" + TitleID + "][v" + Version/0x10000 + "]";
            else
                return Name + " [" + TitleID + "][v" + Version/0x10000 + "]";
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is SwitchTitle))
                return false;

            SwitchTitle other = obj as SwitchTitle;
            if (TitleID == null && other.TitleID == null)
                return true;

            if (TitleID == null || other.TitleID == null)
                return false;

            return TitleID.Equals(other.TitleID);
        }

        public override int GetHashCode()
        {
            string h = (TitleID ?? "") + this.Version;
            return h.GetHashCode();
        }
    }
}
