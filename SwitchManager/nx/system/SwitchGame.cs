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
    public class SwitchGame : SwitchTitle
    {
        public ObservableCollection<SwitchDLC> DLC { get; set; }
        public ObservableCollection<SwitchUpdate> Updates { get; set;  }

        public override bool IsGame => true;
        public override bool IsDemo => Name.ToUpper().Contains("DEMO") || Name.ToUpper().Contains("TRIAL VER");
        public override bool IsDLC => false;
        public override bool IsUpdate => false;

        internal SwitchGame(string name, string titleid, string titlekey) : base(name, titleid, titlekey)
        {

        }
    }
}
