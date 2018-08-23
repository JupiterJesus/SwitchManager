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
    public class SwitchDLC : SwitchTitle
    {
        private string gameid;
        public string GameID
        {
            get { return gameid; }
            set { this.gameid = value; NotifyPropertyChanged("GameID"); }
        }

        public override bool IsGame => false;
        public override bool IsDLC => true;
        public override bool IsUpdate => false;
        public override bool IsDemo => false;

        internal SwitchDLC(string name, string titleid, string gameid, string titlekey) : base(name, titleid, titlekey)
        {
            this.GameID = gameid;
        }
    }
}
