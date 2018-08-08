using SwitchManager.nx.collection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.collection
{
    public class SwitchGame
    {
        public string Name { get; set; }
        public string TitleKey { get; set; }
        public string TitleID { get; set; }

        public SwitchImage Icon { get; set; }
        public ulong Size { get; set; }

        public List<string> DLC { get; }
        public List<string> Updates { get; }
        public List<string> Versions { get; }

        internal SwitchGame(string name, string titleid, string titlekey)
        {
            Name = name;
            TitleID = titleid;
            TitleKey = titlekey;
            this.DLC = new List<string>();
            this.Updates = new List<string>();
            this.Versions = new List<string>();
        }
    }
}
