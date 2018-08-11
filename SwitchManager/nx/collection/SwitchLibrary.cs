using SwitchManager.nx.library;
using SwitchManager.nx.cdn;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SwitchManager.nx.library
{
    /// <summary>
    /// This is the primary class for the switch library and cdn downloader. It manages all existing title keys,
    /// library metadata, downloading, eshop access and anything else pretty much. It has XML attributes because it
    /// is much easier to serialize this class a a copycat of the LibraryMetadata class, but the LibraryMetadata class
    /// is the one to use for loading/deserializing data, which should then be copied into the appropriate collection items.
    /// 
    /// If one were to deserialize a SwitchCollection from the library metadata, they would have to rewrite the loading of title keys to modify the existing
    /// collection instead of building it from the ground up. I do think that the existing AddGame functions know how to
    /// handle a collection item already existing and modifying it with relevant data, but you might have to write
    /// a new LoadTitleKeys method that ensure all of the data is correctly loaded. Maybe not, it is possible that I wrote
    /// everything so awesomely that it just works even in this unintended situation.
    /// 
    /// Anyway, the intended use is to call LoadTitleKeys first to populate the collection, THEN modify the collection with
    /// any metadata you care to track.
    /// </summary>
    [XmlRoot(ElementName = "Library")]
    public class SwitchLibrary
    {
        [XmlElement(ElementName = "CollectionItem")]
        public SwitchTitleCollection Collection { get; set; }

        [XmlIgnore]
        public CDNDownloader Loader { get; set; }

        [XmlIgnore]
        public string RomsPath { get; set; } = ".";

        [XmlIgnore]
        public bool RemoveContentAfterRepack { get; set; } = false;

        [XmlIgnore]
        public string ImagesPath { get; set; }

        private Dictionary<string, SwitchCollectionItem> titlesByID = new Dictionary<string, SwitchCollectionItem>();

        /// <summary>
        /// This default constructor is ONLY so that XmlSerializer will stop complaining. Don't use it, unless
        /// you remember to also set the loader, the image path and the rom path!
        /// </summary>
        public SwitchLibrary()
        {
            this.Collection = new SwitchTitleCollection();
        }

        public SwitchLibrary(CDNDownloader loader, string imagesPath, string romsPath) : this()
        {
            this.Loader = loader;
            this.ImagesPath = imagesPath;
            this.RomsPath = romsPath;
        }

        internal SwitchCollectionItem AddTitle(SwitchCollectionItem item)
        {
            if (item != null)
            {
                Collection.Add(item);
                titlesByID[item.Title.TitleID] = item;
            }
            return item;
        }

        internal SwitchCollectionItem NewTitle(string name, string titleid, string titlekey, SwitchCollectionState state = SwitchCollectionState.NotOwned, bool isFavorite = false)
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
                return item;
            }
        }

        internal SwitchCollectionItem AddTitle(string name, string titleid, string titlekey)
        {
            SwitchCollectionItem item = NewTitle(name, titleid, titlekey);
            AddTitle(item);
            return item;
        }

        /// <summary>
        /// Loads library metadata. This data is related directly to your collection, rather than titles or keys and whatnot.
        /// </summary>
        /// <param name="filename"></param>
        internal void LoadMetadata(string path)
        {
            if (!path.EndsWith(".xml"))
                path += ".xml";
            path = Path.GetFullPath(path);

            XmlSerializer xml = new XmlSerializer(typeof(LibraryMetadata));
            LibraryMetadata metadata;
            // Create a new file stream to write the serialized object to a file

            if (!File.Exists(path))
            {
                Console.WriteLine("Library metadata XML file doesn't exist, one will be created when the app closes.");
                return;
            }

            using (FileStream fs = File.OpenRead(path))
                metadata = xml.Deserialize(fs) as LibraryMetadata;

            foreach (var item in metadata.Items)
            {
                SwitchCollectionItem ci = GetTitleByID(item.TitleID);
                if (ci == null)
                {
                    Console.WriteLine("Found metadata for a title that doesn't exist: " + ci);
                    continue;
                }

                ci.IsFavorite = item.IsFavorite;
                ci.RomPath = item.Path;
                ci.State = item.State;
            }

            Console.WriteLine($"Finished loading library metadata from {path}");
        }

        internal void SaveMetadata(string path)
        {
            if (!path.EndsWith(".xml"))
                path += ".xml";
            path = Path.GetFullPath(path);

            XmlSerializer xml = new XmlSerializer(typeof(SwitchLibrary));

            // Create a new file stream to write the serialized object to a file
            FileStream fs = File.Exists(path) ? File.Open(path, FileMode.Truncate, FileAccess.Write) : File.Create(path);
            xml.Serialize(fs, this);
            fs.Dispose();

            Console.WriteLine($"Finished saving library metadata to {path}");
        }

        /// <summary>
        /// Scans a folder for existing roms and updates the collection.
        /// </summary>
        /// <param name="path"></param>
        internal void ScanRomsFolder(string path)
        {
            DirectoryInfo dinfo = new DirectoryInfo(path);
            if (!dinfo.Exists)
                throw new DirectoryNotFoundException($"Roms directory {path} not found.");

            foreach (var nspFile in dinfo.EnumerateFiles("*.nsp"))
            {
                string fname = nspFile.Name; // base name
                fname = Path.GetFileNameWithoutExtension(fname); // remove .nsp
                var fileParts = fname.Split();
                if (fileParts == null || fileParts.Length < 2)
                    continue;

                string meta = fileParts.Last();

                SwitchTitleType type = SwitchTitleType.Unknown;
                string name = null;
                string id = null;
                string version = null;

                // Lets parse the file name to get name, id and version
                // Also check for [DLC] and [UPD] signifiers
                // I could use a Regex but I'm not sure that would be faster or easier to do
                if ("[DLC]".Equals(fileParts[0].ToLower()))
                {
                    type = SwitchTitleType.DLC;
                    name = string.Join(" ", fileParts.Where((s, idx) => idx > 0 && idx < fileParts.Length - 1));
                }
                else
                {
                    name = string.Join(" ", fileParts.Where((s, idx) => idx < fileParts.Length - 1));
                    if (meta.StartsWith("[UPD]"))
                    {
                        type = SwitchTitleType.Update;
                        meta = meta.Remove(0, 5);
                    }
                    else
                    {
                        if (name.EndsWith("Demo"))
                        {
                            type = SwitchTitleType.Demo;
                        }
                        else
                        {
                            type = SwitchTitleType.Game;
                        }
                    }
                }

                if (meta.StartsWith("[") && meta.EndsWith("]"))
                {
                    string[] metaParts = meta.Split(new string[] { "][" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (metaParts.Length > 1)
                    {
                        string verPart = metaParts[1];
                        if (verPart.EndsWith("]"))
                            version = verPart.Remove(verPart.Length - 1);
                    }
                    if (metaParts.Length > 0)
                    {
                        string idPart = metaParts[0];
                        if (idPart.StartsWith("["))
                        {
                            id = idPart.Remove(0, 1);
                            if (idPart.EndsWith("]"))
                                id = idPart.Remove(idPart.Length - 1);
                        }
                    }
                }

                /* TODO Scan Roms
                var item = GetTitleByID(id);
                if (item  == null)
                {
                    item = AddGame(name, id, null);
                }
                item.File = nspFile;
                item.Title.Type = type;
                if (type == SwitchTitleType.Update)
                    AddUpdateTitle(id)
                */
            }
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
        public async Task<string> DownloadTitle(SwitchTitle title, uint v, bool repack, bool verify)
        {
            if (title == null)
                throw new Exception($"No title selected for download");

            string dir = this.RomsPath + Path.DirectorySeparatorChar + title.TitleID;
            DirectoryInfo dinfo = new DirectoryInfo(dir);
            if (!dinfo.Exists)
                dinfo.Create();
            
            try
            {
                // Download a base version with a game ID
                if (v == 0)
                {
                    if (SwitchTitle.IsBaseGameID(title.TitleID))
                    {
                        return await DoNspDownloadAndRepack(title, v, dinfo, repack, verify).ConfigureAwait(false);
                    }
                    else
                        throw new Exception("Don't try to download a game with version greater than 0!");
                }
                else
                {
                    if (SwitchTitle.IsBaseGameID(title.TitleID))
                        throw new Exception("Don't try to download an update using base game's ID!");
                    else if (SwitchTitle.IsDLCID(title.TitleID))
                    {
                        // TODO Handle downloading of DLC
                        return null;
                    }
                    else
                    {
                        // TODO: Handle downloading of update
                        return await DoNspDownloadAndRepack(title, v, dinfo, repack, verify).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                // TODO delete directory after
                //dinfo.Delete(true);
            }
        }

        private async Task<string> DoNspDownloadAndRepack(SwitchTitle title, uint version, DirectoryInfo dir, bool repack, bool verify)
        {
            var nsp = await Loader.DownloadTitle(title, version, dir.FullName, repack, verify).ConfigureAwait(false);

            if (repack)
            {
                string nspFile = $"{title.Name} [{title.TitleID}][{version}].nsp";
                string nspPath = $"{this.RomsPath}{Path.DirectorySeparatorChar}{nspFile}";

                // Repack the game files into an NSP
                bool success = await nsp.Repack(nspPath).ConfigureAwait(false);

                // If the NSP failed somehow but the file exists any, remove it
                if (!success && File.Exists(nspPath))
                    File.Delete(nspPath);

                if (this.RemoveContentAfterRepack)
                    dir.Delete(true);

                return nspPath;
            }
            return dir.FullName;
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
                    string romPath = await DownloadTitle(title, v, repack, verify);
                    titleItem.State = SwitchCollectionState.Owned;
                    titleItem.RomPath = Path.GetFullPath(romPath);
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
                await Loader.DownloadRemoteImage(title);
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
            string path = Path.GetFullPath(this.ImagesPath);
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
                Directory.CreateDirectory(this.ImagesPath);
            }

            return null;
        }
        
        private static SwitchImage blankImage = new SwitchImage("Images\\blank.jpg");

        public async Task LoadTitleKeysFile(string filename)
        {
            var lines = File.ReadLines(filename);
            var versions = await Loader.GetLatestVersions().ConfigureAwait(false);

            foreach (var line in lines)
            {
                string[] split = line.Split('|');
                string tid = split[0]?.Trim()?.Substring(0, 16);
                string tkey = split[1]?.Trim()?.Substring(0, 32);
                string name = split[2]?.Trim();

                LoadTitle(tid, tkey, name, versions);
            }
        }

        public async Task<ICollection<SwitchCollectionItem>> UpdateTitleKeysFile(string file)
        {
            var lines = File.ReadLines(file);
            var versions = await Loader.GetLatestVersions().ConfigureAwait(false);

            var newTitles = new List<SwitchCollectionItem>();
            foreach (var line in lines)
            {
                string[] split = line.Split('|');
                string tid = split[0]?.Trim()?.Substring(0, 16);
                SwitchCollectionItem item = GetTitleByID(tid);
                if (item == null)
                {
                    // New title!!
                    string tkey = split[1]?.Trim()?.Substring(0, 32);
                    string name = split[2]?.Trim();
                    item = LoadTitle(tid, tkey, name, versions);
                    item.State = SwitchCollectionState.New;
                    newTitles.Add(item);
                }
            }

            return newTitles;
        }

        private SwitchCollectionItem LoadTitle(string tid, string tkey, string name, Dictionary<string,uint> versions)
        {
            var item = NewTitle(name, tid, tkey);
            var title = item?.Title;
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
                    catch (Exception)
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
            AddTitle(item);
            return item;
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
                SwitchCollectionItem item = NewTitle(name, baseGameID, null);
                AddTitle(item);
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
