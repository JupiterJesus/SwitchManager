using SwitchManager.nx.img;
using SwitchManager.nx.cdn;
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
        private Dictionary<string, SwitchCollectionItem> titlesByID = new Dictionary<string, SwitchCollectionItem>();

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
            titlesByID[item.Title.TitleID] = item;
            return item;
        }

        /// <summary>
        /// Loads all title icons in the background. It does so asynchronously, so the caller better be able to update
        /// the image file display at any time after the calls complete. If preload is true, it also tries to remotely load
        /// every single image if it isn't found locally.
        /// </summary>
        /// <param name="localPath"></param>
        internal void LoadTitleIcons(string localPath, bool preload = false)
        {
            foreach (SwitchCollectionItem item in Collection)
            {
                Task.Run(()=>LoadTitleIcon(item.Title, preload)); // This is async, let it do its thing we don't need the results now
            }
        }

        /// <summary>
        /// Gets a title icon. If it isn't cached locally, gets it from nintendo.
        /// TODO: Remote and local paths are currently hard-coded in the image loader!
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        private async Task LoadTitleIcon(SwitchTitle game, bool preload = false)
        {
            SwitchImage img = loader.GetLocalImage(game.TitleID);
            if (img == null && preload && SwitchTitle.IsBaseGameID(game.TitleID))
            {
                // Ask the image loader to get the image remotely and cache it
                // Task is potentially asynchronous BUT I'm just waiting for it here
                img = await loader.GetRemoteImage(game).ConfigureAwait(false);
            }
            // Return cached image, or blank if it couldn't be found

            if (img == null)
                game.Icon = blankImage;
            else
                game.Icon = img;
        }

        private static SwitchImage blankImage = new SwitchImage("Images\\blank.jpg");

        public void LoadTitleKeysFile(string filename)
        {
            var lines = File.ReadLines(filename);
            var versions = loader.GetLatestVersions().Result;

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
                        title.Versions = new ObservableCollection<uint> { 0 };
                    }

                    if (title.Name.EndsWith("Demo"))
                        title.Type = SwitchTitleType.DEMO;
                    else if (title.Name.StartsWith("[DLC]"))
                    {
                        // basetid = '%s%s000' % (tid[:-4], str(int(tid[-4], 16) - 1))
                        string baseGameID = SwitchTitle.GetBaseGameIDFromDLC(title.TitleID);
                        title.Type = SwitchTitleType.DLC;
                        try
                        {
                            AddDLCTitle(baseGameID, title.TitleID);
                        }
                        catch (Exception e)
                        {
                            if (GetTitleByID(baseGameID) == null)
                                Console.WriteLine($"WARNING: Couldn't find base game ID {baseGameID} for DLC {title.Name}");
                        }
                    }
                    else
                    {
                        title.Type = SwitchTitleType.GAME;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a DLC title (by ID) to the list of DLC of a base title (also looked up by ID)
        /// </summary>
        /// <param name="baseGameID">Title ID of base game.</param>
        /// <param name="dlcID">Title ID of base game's DLC, to add to the game's DLC list.</param>
        /// <returns>The base title that the DLC was attached to.</returns>
        public SwitchTitle AddDLCTitle(string baseGameID, string dlcID)
        {
            SwitchTitle baseTitle = GetTitleByID(baseGameID)?.Title;
            if (baseTitle == null)
            {
                throw new Exception("Tried to add DLC to a title that isn't in the database. Make sure DLC is at the END of your title keys file!");
            }

            if (baseTitle.DLC == null) baseTitle.DLC = new ObservableCollection<string>();

            baseTitle.DLC.Add(dlcID);
            return baseTitle;
        }

        public SwitchCollectionItem GetTitleByID(string titleID)
        {
            if (titleID == null || titleID.Length != 16)
                return null;

            // In case someone tries to look up by UPDATE TID, convert to base game TID
            if (SwitchTitle.IsUpdateTitleID(titleID))
                titleID = SwitchTitle.GetBaseGameIDFromUpdate(titleID);
            else if (SwitchTitle.IsDLCID(titleID))
                titleID = SwitchTitle.GetBaseGameIDFromDLC(titleID);

            return titlesByID.TryGetValue(titleID, out SwitchCollectionItem returnValue) ? returnValue : null;
        }
    }
}
