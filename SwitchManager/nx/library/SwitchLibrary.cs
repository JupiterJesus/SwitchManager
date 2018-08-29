using SwitchManager.nx.library;
using SwitchManager.nx.system;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using SwitchManager.util;
using SwitchManager.nx.cdn;

namespace SwitchManager.nx.library
{
    /// <summary>
    /// This is the primary class for the switch library and cdn downloader. It manages all existing title keys,
    /// library metadata, downloading, eshop access and anything else pretty much. It has XML attributes because it
    /// is much easier to serialize this class a a copycat of the LibraryMetadata class, but the LibraryMetadata class
    /// is the one to use for loading/deserializing data, which should then be copied into the appropriate collection items.
    /// 
    /// The library should be populated before use by loading from a title keys file or from library metadata, or both.
    /// </summary>
    [XmlRoot(ElementName = "Library")]
    public class SwitchLibrary
    {
        [XmlElement(ElementName = "CollectionItem")]
        public ObservableList<SwitchCollectionItem> Collection { get; set; }

        [XmlIgnore]
        public EshopDownloader Loader { get; set; }

        [XmlIgnore]
        public string RomsPath { get; set; } = "nsp";

        [XmlIgnore]
        public string TempPath { get; set; } = "tmp";

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
            this.Collection = new ObservableList<SwitchCollectionItem>();
        }

        public SwitchLibrary(EshopDownloader loader, string imagesPath, string romsPath, string tempPath = null) : this()
        {
            this.Loader = loader;
            this.ImagesPath = imagesPath;
            this.RomsPath = romsPath;
            this.TempPath = tempPath ?? romsPath;
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

        internal SwitchCollectionItem AddGame(SwitchGame game)
        {
            if (game != null)
            {
                var item = new SwitchCollectionItem(game);
                return AddTitle(item);
            }
            return null;
        }

        internal SwitchCollectionItem NewGame(string name, string titleid, string titlekey, SwitchCollectionState state = SwitchCollectionState.NotOwned, bool isFavorite = false)
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
                SwitchGame game = new SwitchGame(name, titleid, titlekey);
                SwitchCollectionItem item = new SwitchCollectionItem(game, state, isFavorite);
                return item;
            }
        }

        internal SwitchCollectionItem AddTitle(SwitchTitle title)
        {
            SwitchCollectionItem item = new SwitchCollectionItem(title);
            AddTitle(item);
            return item;
        }

        /// <summary>
        /// Loads library metadata. This data is related directly to your collection, rather than titles or keys and whatnot.
        /// </summary>
        /// <param name="filename"></param>
        internal async Task LoadMetadata(string path)
        {
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

            await LoadMetadata(metadata?.Items).ConfigureAwait(false);
            Console.WriteLine($"Finished loading library metadata from {path}");
        }

        /// <summary>
        /// Loads library metadata. This data is related directly to your collection, rather than titles or keys and whatnot.
        /// </summary>
        /// <param name="filename"></param>
        internal async Task LoadMetadata(IEnumerable<LibraryMetadataItem> metadata)
        {
            if (metadata != null)
            {
                bool certDenied = false;
                Dictionary<string, uint> versions = null;
                try
                {
                    versions = await Loader.GetLatestVersions().ConfigureAwait(false);
                }
                catch (CertificateDeniedException)
                {
                    certDenied = true;
                }

                foreach (var item in metadata)
                {
                    SwitchCollectionItem ci = GetTitleByID(item.TitleID);
                    if (ci == null)
                    {
                        ci = LoadTitle(item.TitleID, item.TitleKey, item.Name, versions);
                    }
                    else
                    {
                        // UPDATE TITLE KEY JUST IN CASE IT IS MISSING
                        if (item.TitleKey != null) ci.Title.TitleKey = item.TitleKey;

                        // ONLY UPDATE NAME IF IT IS MISSING
                        // I USED TO LET IT UPDATE NAMES FROM THE OFFICIAL SOURCE
                        // BUT THE NAMES ARE OFTEN LOW QUALITY OR MISSING REGION SIGNIFIERS OR DEMO LABELS AND FILLED
                        // WITH UNICODE SYMBOLS
                        if (item.Name != null)
                        {
                            if (string.IsNullOrWhiteSpace(ci.TitleName))
                                ci.Title.Name = item.Name;
                            //else if (ci.Title.IsDemo && !(item.Name.ToUpper().Contains("DEMO") || item.Name.ToUpper().Contains("SPECIAL TRIAL") || item.Name.ToUpper().Contains("TRIAL VER")))
                            //    ci.Title.Name = item.Name + " Demo";
                        }
                    }

                    // Collection State enum
                    if (item.State.HasValue) ci.State = item.State.Value;

                    // long?
                    if (item.Size.HasValue) ci.Size = item.Size;

                    // bool?
                    if (item.IsFavorite.HasValue) ci.IsFavorite = item.IsFavorite.Value;
                    if (item.HasDLC.HasValue) ci.HasDLC = item.HasDLC.Value;
                    if (item.HasAmiibo.HasValue) ci.HasAmiibo = item.HasAmiibo.Value;

                    // datetime
                    if (item.ReleaseDate.HasValue) ci.ReleaseDate = item.ReleaseDate;

                    // string
                    if (!string.IsNullOrWhiteSpace(item.Path)) ci.RomPath = item.Path;
                    if (!string.IsNullOrWhiteSpace(item.Developer)) ci.Developer = item.Developer;
                    if (!string.IsNullOrWhiteSpace(item.Publisher)) ci.Publisher = item.Publisher;
                    if (!string.IsNullOrWhiteSpace(item.Description)) ci.Description = item.Description;
                    if (!string.IsNullOrWhiteSpace(item.Intro)) ci.Intro = item.Intro;
                    if (!string.IsNullOrWhiteSpace(item.Category)) ci.Category = item.Category;
                    if (!string.IsNullOrWhiteSpace(item.BoxArtUrl)) ci.BoxArtUrl = item.BoxArtUrl;
                    if (!string.IsNullOrWhiteSpace(item.Icon)) ci.Icon = item.Icon;
                    if (!string.IsNullOrWhiteSpace(item.Rating)) ci.Rating = item.Rating;
                    if (!string.IsNullOrWhiteSpace(item.NumPlayers)) ci.NumPlayers = item.NumPlayers;
                    if (!string.IsNullOrWhiteSpace(item.NsuId)) ci.NsuId = item.NsuId;
                    if (!string.IsNullOrWhiteSpace(item.Code)) ci.Code = item.Code;
                    if (!string.IsNullOrWhiteSpace(item.RatingContent)) ci.RatingContent = item.RatingContent;
                    if (!string.IsNullOrWhiteSpace(item.Price)) ci.Price = item.Price;

                    if (item.Updates != null)
                        foreach (var update in item.Updates)
                        {
                            AddUpdateTitle(update.TitleID, item.TitleID, item.Name, update.Version, update.TitleKey);
                        }
                }

                if (certDenied) throw new CertificateDeniedException();
            }
        }

        internal void SaveMetadata(string path)
        {
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
                long size = nspFile.Length;

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
                        if (name.ToUpper().Contains("DEMO"))
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
                            verPart = verPart.Remove(verPart.Length - 1);
                        if (verPart.StartsWith("v"))
                            verPart = verPart.Remove(0, 1);
                        version = verPart;
                    }
                    if (metaParts.Length > 0)
                    {
                        string idPart = metaParts[0];
                        if (idPart.StartsWith("["))
                            idPart = idPart.Remove(0, 1);
                        if (idPart.EndsWith("]"))
                            idPart = idPart.Remove(idPart.Length - 1);
                        id = idPart;
                    }
                }

                switch (type)
                {
                    case SwitchTitleType.DLC:
                    case SwitchTitleType.Game:
                    case SwitchTitleType.Demo:
                        var item = GetTitleByID(id);
                        if (item != null && id.Equals(item.TitleId))
                        {
                            item.RomPath = nspFile.FullName;

                            // If you haven't already marked the file as on switch, mark it owned
                            if (item.State != SwitchCollectionState.OnSwitch)
                                item.State = SwitchCollectionState.Owned;

                            item.Size = size;
                        }
                        break;
                    case SwitchTitleType.Update:
                        SwitchUpdate u = AddUpdateTitle(id, null, name, uint.Parse(version), null);
                        // No size... right now updates don't have a size, because size is associated with collection items,
                        // and updates don't have their own collection item.
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Removes a title from the collection, permanently.
        /// </summary>
        /// <param name="title">Title to remove.</param>
        internal void DeleteTitle(SwitchTitle title)
        {
            var item = GetTitleByID(title?.TitleID);
            Collection.Remove(item);
        }

        /// <summary>
        /// Removes a collection item from the collection, permanently.
        /// </summary>
        /// <param name="item">Title to remove.</param>
        internal void DeleteTitle(SwitchCollectionItem item)
        {
            Collection.Remove(item);
        }

        private SwitchUpdate AddUpdateTitle(string updateid, string gameid, string name, uint version, string titlekey)
        {
            if (gameid == null)
                gameid = (updateid == null ? null : SwitchTitle.GetBaseGameIDFromUpdate(updateid));
            if (updateid != null && gameid != null)
            {
                SwitchUpdate update = new SwitchUpdate(name, updateid, gameid, version, titlekey);
                return AddUpdateTitle(update);
            }
            return null;
        }

        private SwitchUpdate AddUpdateTitle(SwitchUpdate update)
        {
            var baseT = GetTitleByID(update.GameID); // Let's try adding this to the base game's list
            if (baseT == null)
            {
                Console.WriteLine("WARNING: Found an update for a game that doesn't exist.");
                return null;
            }
            else if (baseT.Title == null)
            {
                Console.WriteLine("WARNING: Found a collection item in the library with a null title.");
                return null;
            }
            else if (baseT.Title.IsGame)
            {
                SwitchGame game = baseT.Title as SwitchGame;
                if (game.Updates == null)
                    game.Updates = new List<SwitchUpdate>();

                game.Updates.Add(update);
            }
            return update;
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

            string dir = this.TempPath + Path.DirectorySeparatorChar + title.TitleID;
            DirectoryInfo dinfo = new DirectoryInfo(dir);
            if (!dinfo.Exists)
                dinfo.Create();
            
            try
            {
                // Download a base version with a game ID
                if (v == 0)
                {
                    if (title.IsGame || title.IsDemo || title.IsDLC)
                    {
                        return await DoNspDownloadAndRepack(title, v, dinfo, repack, verify).ConfigureAwait(false);
                    }
                    else
                        throw new Exception("Don't try to download an update with version 0!");
                }
                else
                {
                    if (title.IsGame || title.IsDemo)
                        throw new Exception("Don't try to download an update using base game's ID!");
                    else
                    {
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

            if (repack && nsp != null)
            {
                string titleName = Miscellaneous.SanitizeFileName(title.Name);

                // format is
                // [DLC] at the start, plus space, if it is DLC - this is already party of the name for DLC, typically
                // title name
                // [UPD] if it is an update
                // [titleid]
                // [vXXXXXX], where XXXXXX is the version number in decimal
                string nspFile = (title.Type == SwitchTitleType.DLC && !titleName.StartsWith("[DLC]") ? "[DLC] " : "") + titleName + (title.Type == SwitchTitleType.Update?" [UPD]":" ") + $"[{title.TitleID}][v{version}].nsp";
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
        internal async Task DownloadTitle(SwitchCollectionItem titleItem, uint v, DownloadOptions options, bool repack, bool verify)
        {
            SwitchTitle title = titleItem?.Title;
            if (title == null)
                throw new Exception($"No title selected for download");

            switch (options)
            {
                case DownloadOptions.AllDLC:
                    if (title.IsDLC)
                    {
                        uint latestVersion = await Loader.GetLatestVersion(title).ConfigureAwait(false);
                        string dlcPath = await DownloadTitle(title, latestVersion, repack, verify);
                        if (titleItem.Title.IsTitleKeyValid)
                            titleItem.State = SwitchCollectionState.Owned;
                        else
                            titleItem.State = SwitchCollectionState.Downloaded;
                        titleItem.RomPath = Path.GetFullPath(dlcPath);
                        titleItem.Size = Miscellaneous.GetFileSystemSize(dlcPath);
                    }
                    else if (title.IsGame)
                    {
                        SwitchGame game = title as SwitchGame;
                        if (game.DLC != null && game.DLC.Count > 0)
                            foreach (var t in game.DLC)
                            {
                                SwitchCollectionItem dlcTitle = GetTitleByID(t?.TitleID);
                                uint latestVersion = await Loader.GetLatestVersion(dlcTitle.Title).ConfigureAwait(false);
                                string dlcPath = await DownloadTitle(dlcTitle?.Title, latestVersion, repack, verify);
                                if (dlcTitle.Title.IsTitleKeyValid)
                                        dlcTitle.State = SwitchCollectionState.Owned;
                                else
                                        dlcTitle.State = SwitchCollectionState.Downloaded;

                                dlcTitle.RomPath = Path.GetFullPath(dlcPath);
                                dlcTitle.Size = Miscellaneous.GetFileSystemSize(dlcPath);
                            }
                    }
                    break;
                case DownloadOptions.UpdateAndDLC:
                    goto case DownloadOptions.UpdateOnly;

                case DownloadOptions.BaseGameAndUpdateAndDLC:
                    goto case DownloadOptions.BaseGameOnly;

                case DownloadOptions.BaseGameAndUpdate:
                    goto case DownloadOptions.BaseGameOnly;
                    
                case DownloadOptions.UpdateOnly:
                    if (v == 0) return;

                    // If a version greater than 0 is selected, download it and every version below it
                    while (v > 0)
                    {
                        SwitchUpdate update = title.GetUpdateTitle(v);
                        string updatePath = await DownloadTitle(update, v, repack, verify);
                        AddUpdateTitle(update);
                        v -= 0x10000;
                    }

                    if (options == DownloadOptions.UpdateAndDLC || options == DownloadOptions.BaseGameAndUpdateAndDLC)
                        goto case DownloadOptions.AllDLC;
                    break;

                case DownloadOptions.BaseGameAndDLC:
                    goto case DownloadOptions.BaseGameOnly;

                case DownloadOptions.BaseGameOnly:
                default:
                    if (title.IsGame)
                    {
                        string romPath = await DownloadTitle(title, title.BaseVersion, repack, verify);

                        if (titleItem.Title.IsTitleKeyValid)
                            titleItem.State = SwitchCollectionState.Owned;
                        else
                            titleItem.State = SwitchCollectionState.Downloaded;

                        titleItem.RomPath = Path.GetFullPath(romPath);
                        titleItem.Size = Miscellaneous.GetFileSystemSize(romPath);
                    }
                    if (options == DownloadOptions.BaseGameAndUpdate || options == DownloadOptions.BaseGameAndUpdateAndDLC)
                        goto case DownloadOptions.UpdateOnly;
                    else if (options == DownloadOptions.BaseGameAndDLC)
                        goto case DownloadOptions.AllDLC;
                    break;
            }
        }

        /// <summary>
        /// Loads all title icons in the background. It does so asynchronously, so the caller better be able to update
        /// the image file display at any time after the calls complete. If preload is true, it also tries to remotely load
        /// every single image if it isn't found locally.
        /// </summary>
        /// <param name="localPath"></param>
        internal void LoadTitleIcons(string localPath)
        {
            foreach (SwitchCollectionItem item in Collection)
            {
                if (item.Title.Icon == null)
                    Task.Run(()=>LoadTitleIcon(item.Title, true)); // This is async, let it do its thing we don't need the results now
            }
        }

        /// <summary>
        /// Gets a title icon. If it isn't cached locally, gets it from nintendo. Only loads a local image if downloadRemote is false, but will download
        /// from the CDN if downloadRemote is true.
        /// </summary>
        /// <param name="title">Title whose icon you wish to load</param>
        /// <param name="downloadRemote">If true, loads the image from nintendo if it isn't found in cache</param>
        /// <returns></returns>
        public async Task LoadTitleIcon(SwitchTitle title, bool downloadRemote = false)
        {
            string img = GetLocalImage(title.TitleID);
            if (img == null && downloadRemote && SwitchTitle.IsBaseGameID(title.TitleID))
            {
                // Ask the image loader to get the image remotely and cache it
                await Loader.DownloadRemoteImage(title);
                img = GetLocalImage(title.TitleID);
            }

            // Return cached image
            title.Icon = img;
        }

        public string GetLocalImage(string titleID)
        {
            string path = Path.GetFullPath(this.ImagesPath);
            if (Directory.Exists(path))
            {
                string location = path + Path.DirectorySeparatorChar + titleID + ".jpg";
                if (File.Exists(location))
                {
                    return location;
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
        
        public static string BlankImage { get { return "blank.jpg"; } }


        public async Task LoadTitleKeysFile(string filename)
        {
            var lines = File.ReadLines(filename);
            bool certDenied = false;

            Dictionary<string, uint> versions = null;
            try
            {
                versions = await Loader.GetLatestVersions().ConfigureAwait(false);
            }
            catch (CertificateDeniedException)
            {
                certDenied = true;
            }

            foreach (var line in lines)
            {
                string[] split = line.Split('|');
                string tid = split[0]?.Trim()?.Substring(0, 16);
                string tkey = split[1]?.Trim()?.Substring(0, 32);
                string name = split[2]?.Trim();

                var item = LoadTitle(tid, tkey, name, versions);
            }

            if (certDenied) throw new CertificateDeniedException();
        }

        public async Task<ICollection<SwitchCollectionItem>> UpdateTitleKeysFile(string file)
        {
            var lines = File.ReadLines(file);
            var versions = await Loader.GetLatestVersions().ConfigureAwait(false);

            var newTitles = new List<SwitchCollectionItem>();
            foreach (var line in lines)
            {
                string[] split = line.Split('|');
                string tid = string.IsNullOrWhiteSpace(split[0]) ? null : split[0];
                if (tid == null || tid.Length < 16)
                    continue;
                else
                    tid = tid.Trim().Substring(0, 16).ToLower();

                SwitchCollectionItem item = tid == null ? null : GetTitleByID(tid);

                // We only care about new titles, and ignore messed up entries that are missing a title ID for some reason
                if (tid != null)
                {
                    string tkey = string.IsNullOrWhiteSpace(split[1]) ? null : split[1]?.Trim()?.Substring(0, 32).ToLower();
                    string name = string.IsNullOrWhiteSpace(split[2]) ? null : split[2]?.Trim();
                    if (item == null)
                    {
                        // New title!!

                        // A missing tkey can always be added in later
                        item = LoadTitle(tid, tkey, name, versions);

                        // If the key is missing, mark it as such, otherwise it is a properly working New title
                        if (item.Title.IsTitleKeyValid)
                            item.State = SwitchCollectionState.New;
                        else
                            item.State = SwitchCollectionState.NoKey;

                        newTitles.Add(item);
                    }
                    else
                    {
                        // Existing title
                        // The only thing to do here is check for new data if the existing entry is missing anything
                        if (!item.Title.IsTitleKeyValid)
                            item.Title.TitleKey = tkey;

                        if (string.IsNullOrWhiteSpace(item.Title.Name))
                            item.Title.Name = name;
                    }
                }
            }

            return newTitles;
        }

        /// <summary>
        /// Loads a title into the collection. Whether the collection is being populated by a titlekeys file or by
        /// the library metadata, you'll want to call this for each item to make sure all the right logic gets executed
        /// for adding a game (or DLC, or update) to the library.
        /// </summary>
        /// <param name="tid">16-digit hex Title ID</param>
        /// <param name="tkey">32-digit hex Title Key</param>
        /// <param name="name">The name of game or DLC</param>
        /// <param name="versions">The dictionary of all the latest versions for every game. Get this via the CDN.</param>
        /// <returns></returns>
        private SwitchCollectionItem LoadTitle(string tid, string tkey, string name, Dictionary<string,uint> versions)
        {
            if (string.IsNullOrWhiteSpace(tid)) return null;
            else tid = tid.ToLower();

            if (string.IsNullOrWhiteSpace(tkey)) tkey = null;
            else tkey = tkey.ToLower();

            if (SwitchTitle.IsBaseGameID(tid))
            {
                var item = NewGame(name, tid, tkey);
                var game = item?.Title as SwitchGame;
                if (versions != null && versions.ContainsKey(game.TitleID))
                {
                    uint v = versions[game.TitleID];
                    game.LatestVersion = v;
                }

                AddTitle(item);
                return item;
            }
            else if (name.StartsWith("[DLC]") || SwitchTitle.IsDLCID(tid))
            {
                // basetid = '%s%s000' % (tid[:-4], str(int(tid[-4], 16) - 1))
                string baseGameID = SwitchTitle.GetBaseGameIDFromDLC(tid);
                var dlc = new SwitchDLC(name, tid, baseGameID, tkey);

                try
                {
                    return AddDLCTitle(baseGameID, dlc);
                }
                catch (Exception)
                {
                    if (GetBaseTitleByID(baseGameID) == null)
                        Console.WriteLine($"WARNING: Couldn't find base game ID {baseGameID} for DLC {dlc.Name}");
                }
            }
            else
            {
                // ?? huh ??
            }
            return null;
        }

        /// <summary>
        /// Adds a DLC title (by ID) to the list of DLC of a base title (also looked up by ID)
        /// </summary>
        /// <param name="baseGameID">Title ID of base game.</param>
        /// <param name="dlcID">Title ID of base game's DLC, to add to the game's DLC list.</param>
        /// <returns>The base title that the DLC was attached to.</returns>
        public SwitchCollectionItem AddDLCTitle(string baseGameID, SwitchDLC dlc)
        {
            SwitchGame baseGame = GetBaseTitleByID(baseGameID)?.Title as SwitchGame;
            if (baseGame == null)
            {
                // This can happen if you put the DLC before the title, or if your titlekeys file has DLC for
                // titles that aren't in it. The one I'm using, for example, has fire emblem warriors JP dlc,
                // but not the game
                // If the game ends up being added later, AddGame is able to slide in the proper info over the stub we add here
                string name = dlc.Name.Replace("[DLC] ", "");
                baseGame = new SwitchGame(name, baseGameID, null);
                AddTitle(baseGame);
            }

            if (baseGame.IsGame)
            {
                SwitchGame game = baseGame as SwitchGame;
                if (game.DLC == null) game.DLC = new List<SwitchDLC>();

                game.DLC.Add(dlc);
            }
            return AddTitle(dlc);
        }

        public SwitchCollectionItem GetTitleByID(string titleID)
        {
            if (titleID == null || titleID.Length != 16)
                return null;

            return titlesByID.TryGetValue(titleID, out SwitchCollectionItem returnValue) ? returnValue : null;
        }

        public SwitchCollectionItem GetBaseTitleByID(string titleID)
        {
            if (titleID == null || titleID.Length != 16)
                return null;

            // In case someone tries to look up by UPDATE TID, convert to base game TID
            if (SwitchTitle.IsUpdateTitleID(titleID))
                titleID = SwitchTitle.GetBaseGameIDFromUpdate(titleID);
            else if (SwitchTitle.IsDLCID(titleID))
                titleID = SwitchTitle.GetBaseGameIDFromDLC(titleID);

            return GetTitleByID(titleID);
        }

        public SwitchGame GetBaseGameByID(string baseGameID)
        {
            return GetBaseTitleByID(baseGameID)?.Title as SwitchGame;
        }
    }
}
