using SwitchManager.nx.img;
using SwitchManager.nx.net;
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
        public ObservableCollection<SwitchCollectionItem> Collection { get; set; }
        private CDNDownloader loader;

        internal SwitchCollection(CDNDownloader loader)
        {
            Collection = new ObservableCollection<SwitchCollectionItem>();
            this.loader = loader;
        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey, SwitchCollectionState state, bool isFavorite)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey, state, isFavorite);
            Collection.Add(item);
            return item;
        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey, bool isFavorite)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey, isFavorite);
            Collection.Add(item);
            return item;
        }

        internal void LoadMetadata(string filename)
        {

        }

        internal void SaveMetadata(string filename)
        {

        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey, SwitchCollectionState state)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey, state);
            Collection.Add(item);
            return item;
        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey);
            Collection.Add(item);
            return item;
        }

        internal void LoadTitleIcons(string localPath)
        {
            foreach (SwitchCollectionItem item in Collection)
            {
                item.Title.Icon = LoadTitleIcon(item.Title);
            }
        }

        /// <summary>
        /// Gets a title icon. If it isn't cached locally, gets it from nintendo.
        /// TODO: Remote and local paths are currently hard-coded in the image loader!
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private SwitchImage LoadTitleIcon(SwitchTitle game)
        {
            SwitchImage img = loader.GetLocalImage(game.TitleID);
            if (img == null)
            {
                // Ask the image loader to get the image remotely and cache it
                //img = loader.GetRemoteImage(game);
                img = null; // TODO: Fix remote image loading
            }

            // Return cached image
            return img;
        }

        public void LoadTitleKeysFile(string filename)
        {
            var lines = File.ReadLines(filename);
            var versions = loader.GetLatestVersions();
            var ids = versions.Keys.ToList();
            ids.Sort() ;

            foreach (var line in lines)
            {
                string[] split = line.Split('|');
                string tid = split[0]?.Trim()?.Substring(0, 16);
                string tkey = split[1]?.Trim()?.Substring(0, 32);
                string name = split[2]?.Trim();

                var title = AddGame(name, tid, tkey)?.Title;
                if (title != null)
                {
                    if (versions.ContainsKey(title.TitleID))
                    {
                        uint v = versions[title.TitleID];
                        title.Versions = loader.GetAllVersions(v);
                    }
                    else
                    {
                        // The database does NOT contain data for any game whose update is 0, which is to say there is no update
                        // So just give it an updates list of 0
                        title.Versions = new ObservableCollection<uint>();
                        title.Versions.Add(0);
                    }
                    if (title.Name.EndsWith("Demo"))
                        title.Type = SwitchTitleType.DEMO;
                    else if (title.Name.StartsWith("[DLC]"))
                        title.Type = SwitchTitleType.DLC;
                    else
                        title.Type = SwitchTitleType.GAME;
                }
            }
        }
    }
}
