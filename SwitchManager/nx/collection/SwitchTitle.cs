using SwitchManager.nx.img;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.collection
{
    public class SwitchTitle
    {
        public string Name { get; set; }
        public string TitleKey { get; set; }
        public string TitleID { get; set; }
        public SwitchTitleType Type { get; set; }

        public SwitchImage Icon { get; set; }
        public ulong Size { get; set; }

        public ObservableCollection<string> DLC { get; set; }
        public ObservableCollection<string> Updates { get; set;  }
        public ObservableCollection<uint> Versions { get; set; }

        internal SwitchTitle(string name, string titleid, string titlekey)
        {
            Name = name;
            TitleID = titleid;
            TitleKey = titlekey; 
        }
    }
}
