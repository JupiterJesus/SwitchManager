using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.collection
{
    internal class SwitchCollection
    {
        internal ObservableCollection<SwitchCollectionItem> Collection { get; set; }

        internal SwitchCollection()
        {
            Collection = new ObservableCollection<SwitchCollectionItem>();
        }

        internal void AddGame(string name, string titleid, string titlekey, SwitchCollectionState state, bool isFavorite)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey, state, isFavorite);
            Collection.Add(item);
        }

        internal void AddGame(string name, string titleid, string titlekey, bool isFavorite)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey, isFavorite);
            Collection.Add(item);
        }

        internal void AddGame(string name, string titleid, string titlekey, SwitchCollectionState state)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey, state);
            Collection.Add(item);
        }

        internal void AddGame(string name, string titleid, string titlekey)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey);
            Collection.Add(item);
        }

        public void LoadTitleKeysFile(string filename)
        {
            var lines = File.ReadLines(filename);
            foreach (var line in lines)
            {
                string[] split = line.Split('|');
                string tid = split[0]?.Trim()?.Substring(0, 16);
                string tkey = split[1]?.Trim()?.Substring(0, 32);
                string name = split[2]?.Trim();

                AddGame(name, tid, tkey);
            }
        }
    }
}
