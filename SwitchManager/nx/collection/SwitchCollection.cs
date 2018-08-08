using SwitchManager.nx.collection;
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
        public CDNDownloader Loader { get; set; }
        private string imagesPath;
        public string RomsPath { get; set; }
        private Dictionary<string, SwitchCollectionItem> titlesByID = new Dictionary<string, SwitchCollectionItem>();

        internal SwitchCollection(CDNDownloader loader, string imagesPath, string romsPath)
        {
            Collection = new ObservableCollection<SwitchCollectionItem>();
            this.Loader = loader;
            this.imagesPath = imagesPath;
            this.RomsPath = romsPath;
        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey, SwitchCollectionState state, bool isFavorite)
        {
            // Already there, probably because DLC was listed before a title
            if (titlesByID.ContainsKey(titleid))
            {
                SwitchCollectionItem item = titlesByID[titleid];
                item.Title.Name = name;
                item.Title.TitleKey = titlekey;
                item.State = state;
                item.IsFavorite = isFavorite;
                return item;
            }
            else
            {
                SwitchCollectionItem item = new SwitchCollectionItem(name, titleid, titlekey, state, isFavorite);
                Collection.Add(item);
                titlesByID[titleid] = item;
                return item;
            }
        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey, bool isFavorite)
        {
            return AddGame(name, titleid, titlekey, SwitchCollectionState.NotOwned, isFavorite);
        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey, SwitchCollectionState state)
        {
            return AddGame(name, titleid, titlekey, state, false);
        }

        internal SwitchCollectionItem AddGame(string name, string titleid, string titlekey)
        {
            return AddGame(name, titleid, titlekey, SwitchCollectionState.NotOwned, false);
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

        /// <summary>
        /// Initiates a title download. 
        /// Note - you MUST match the version and the title id!
        /// If you try to download a game title with a version number greater than 0, it will fail!
        /// If you try to download an update title with a version number of 0, it will fail!
        /// I have no idea what will even happen if you try to download a DLC.
        /// </summary>
        /// <param name="titleItem"></param>
        /// <param name="v"></param>
        /// <param name="repack"></param>
        /// <param name="verify"></param>
        /// <returns></returns>
        internal async Task DownloadTitle(SwitchTitle title, uint v, bool repack, bool verify)
        {
            if (title == null)
                throw new Exception($"No title selected for download");

            string dir = this.RomsPath + Path.DirectorySeparatorChar + title.TitleID;
            DirectoryInfo dinfo = new DirectoryInfo(dir);
            if (!dinfo.Exists)
                dinfo.Create();
            
            NSP nsp = null;

            try
            {

                // Download a base version with a game ID
                if (v == 0)
                {
                    if (SwitchTitle.IsBaseGameID(title.TitleID))
                    {
                        nsp = await Loader.DownloadTitle(title, v, dir, repack, verify).ConfigureAwait(false);
                        // TODO Handle all the files
                        if (repack)
                        {
                            string nspFile = $"{this.RomsPath}{Path.DirectorySeparatorChar}[{title.TitleID}][{v}].nsp";
                            nsp.Repack(nspFile);
                        }
                    }
                    else
                        throw new Exception("Don't try to download a game with version greater than 0!");
                }
                else
                {
                    if (SwitchTitle.IsBaseGameID(title.TitleID))
                        throw new Exception("Don't try to download an update using base game's ID!");
                    else
                    {
                        nsp = await Loader.DownloadTitle(title, v, dir, repack, verify).ConfigureAwait(false);

                        if (repack)
                        {
                            string titleid = SwitchTitle.GetBaseGameIDFromUpdate(title.TitleID);
                            string nspFile = $"{this.RomsPath}{Path.DirectorySeparatorChar}[{titleid}][{v}].nsp";
                            nsp.Repack(nspFile);
                        }
                    }
                }
            }
            finally
            {
                // TODO delete directory after
                //dinfo.Delete(true);
            }
        }

        /// <summary>
        /// Executes a download of a title and/or updates/DLC, according to the options presented.
        /// TODO: Test this
        /// TODO: DLC
        /// </summary>
        /// <param name="titleItem"></param>
        /// <param name="v"></param>
        /// <param name="options"></param>
        /// <param name="repack"></param>
        /// <param name="verify"></param>
        /// <returns></returns>
        internal async Task DownloadGame(SwitchCollectionItem titleItem, uint v, DownloadOptions options, bool repack, bool verify)
        {
            SwitchTitle title = titleItem?.Title;
            if (title == null)
                throw new Exception($"No title selected for download");

            switch (options)
            {
                case DownloadOptions.AllDLC:
                    break;
                case DownloadOptions.UpdateAndDLC:
                    break;
                case DownloadOptions.BaseGameAndUpdateAndDLC:
                    
                case DownloadOptions.BaseGameAndUpdate:

                    // Get the base game version first
                    uint baseVersion = title.Versions.Last();
                    await DownloadTitle(title, baseVersion, repack, verify);
                    
                    // If a version greater than 0 is selected, download it and every version below it
                    while (v > 0)
                    {
                        SwitchTitle update = title.GetUpdateTitle(v);
                        await DownloadTitle(update, v, repack, verify);
                        v -= 0x10000;
                    }
                    break;
                case DownloadOptions.UpdateOnly:
                    if (v == 0) return;

                    // If a version greater than 0 is selected, download it and every version below it
                    while (v > 0)
                    {
                        SwitchTitle update = SwitchTitle.IsUpdateTitleID(title.TitleID) ? title : title.GetUpdateTitle(v);
                        await DownloadTitle(update, v, repack, verify);
                        v -= 0x10000;
                    }
                    break;
                case DownloadOptions.BaseGameAndDLC:
                    break;
                case DownloadOptions.BaseGameOnly:
                default:
                    v = title.Versions.Last();
                    await DownloadTitle(title, v, repack, verify);
                    break;
            }
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
                await Loader.DownloadRemoteImage(title).ConfigureAwait(false);
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
            var versions = Loader.GetLatestVersions().Result;

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
                        title.Versions = Loader.GetAllVersions(v);
                    }
                    else
                    {
                        // The database does NOT contain data for any game whose update is 0, which is to say there is no update
                        // So just give it an updates list of 0
                        title.Versions = new ObservableCollection<uint> { 0 };
                    }

                    if (title.Name.EndsWith("Demo"))
                        title.Type = SwitchTitleType.Demo;
                    else if (title.Name.StartsWith("[DLC]"))
                    {
                        // basetid = '%s%s000' % (tid[:-4], str(int(tid[-4], 16) - 1))
                        string baseGameID = SwitchTitle.GetBaseGameIDFromDLC(title.TitleID);
                        title.Type = SwitchTitleType.DLC;
                        try
                        {
                            AddDLCTitle(baseGameID, title);
                        }
                        catch (Exception e)
                        {
                            if (GetTitleByID(baseGameID) == null)
                                Console.WriteLine($"WARNING: Couldn't find base game ID {baseGameID} for DLC {title.Name}");
                        }
                    }
                    else
                    {
                        title.Type = SwitchTitleType.Game;
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
        public SwitchTitle AddDLCTitle(string baseGameID, SwitchTitle dlctitle)
        {
            SwitchTitle baseTitle = GetTitleByID(baseGameID)?.Title;
            if (baseTitle == null)
            {
                // This can happen if you put the DLC before the title, or if your titlekeys file has DLC for
                // titles that aren't in it. The one I'm using, for example, has fire emblem warriors JP dlc,
                // but not the game
                // If the game ends up being added later, AddGame is able to slide in the proper info over the stub we add here
                string name = dlctitle.Name.Replace("[DLC] ", "");
                SwitchCollectionItem item = AddGame(name, baseGameID, null);
                baseTitle = item.Title;
            }

            if (baseTitle.DLC == null) baseTitle.DLC = new ObservableCollection<string>();

            baseTitle.DLC.Add(dlctitle.TitleID);
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
