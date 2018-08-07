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
        private string imagesPath;
        private Dictionary<string, SwitchCollectionItem> titlesByID = new Dictionary<string, SwitchCollectionItem>();

        internal SwitchCollection(CDNDownloader loader, string imagesPath)
        {
            Collection = new ObservableCollection<SwitchCollectionItem>();
            this.loader = loader;
            this.imagesPath = imagesPath;
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

        /// <summary>
        /// Loads library metadata. This data is related directly to your collection, rather than titles or keys and whatnot.
        /// </summary>
        /// <param name="filename"></param>
        internal void LoadMetadata(string filename)
        {
            // TODO: LoadMetadata
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
        /// <param name="preload">Set preload to true to load all images at once if they aren't available locally. False will return a blank image if it isn't found in the cache.</param>
        internal void LoadTitleIcons(string localPath, bool preload = false)
        {
            foreach (SwitchCollectionItem item in Collection)
            {
                Task.Run(()=>LoadTitleIcon(item.Title, preload)); // This is async, let it do its thing we don't need the results now
            }
        }

        /// <summary>
        /// Gets a title icon. If it isn't cached locally, gets it from nintendo. Only loads a local image if downloadRemote is false, but will download
        /// from the CDN if downloadRemote is true.
        /// </summary>
        /// <param name="title">Title whose icon you wish to load</param>
        /// <param name="downloadRemote">If true, loads the image from nintendo if it isn't found in cache</param>
        /// <returns></returns>
        private async Task LoadTitleIcon(SwitchTitle title, bool downloadRemote = false)
        {
            SwitchImage img = GetLocalImage(title.TitleID);
            if (img == null && downloadRemote && SwitchTitle.IsBaseGameID(title.TitleID))
            {
                // Ask the image loader to get the image remotely and cache it
                // Task is potentially asynchronous BUT I'm just waiting for it here
                await loader.DownloadRemoteImage(title).ConfigureAwait(false);
                img = GetLocalImage(title.TitleID);
            }
            // Return cached image, or blank if it couldn't be found

            if (img == null)
                title.Icon = blankImage;
            else
                title.Icon = img;
        }

        public SwitchImage GetLocalImage(string titleID)
        {
            string path = Path.GetFullPath(this.imagesPath);
            if (Directory.Exists(path))
            {
                string location = path + Path.DirectorySeparatorChar + titleID + ".jpg";
                if (File.Exists(location))
                {
                    SwitchImage img = new SwitchImage(location);
                    return img;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                Directory.CreateDirectory(this.imagesPath);
            }

            return null;
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
