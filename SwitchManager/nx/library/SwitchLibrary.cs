using log4net;
using Newtonsoft.Json.Linq;
using Supremes;
using SwitchManager.io;
using SwitchManager.nx.cdn;
using SwitchManager.nx.system;
using SwitchManager.util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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
    /// The library should be populated before use by loading from a title keys file or from library metadata, or both.
    /// </summary>
    [XmlRoot(ElementName = "Library")]
    public class SwitchLibrary
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(SwitchLibrary));

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

        [XmlIgnore]
        public string PreferredRegion { get; set; }

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
                logger.Warn("Library metadata XML file doesn't exist, one will be created when the app closes.");
                return;
            }

            using (FileStream fs = File.OpenRead(path))
                metadata = xml.Deserialize(fs) as LibraryMetadata;

            await LoadMetadata(metadata?.Items, true).ConfigureAwait(false);
            logger.Info($"Finished loading library metadata from {path}");
        }

        /// <summary>
        /// Loads library metadata. This data is related directly to your collection, rather than titles or keys and whatnot.
        /// </summary>
        /// <param name="filename"></param>
        public async Task LoadMetadata(IEnumerable<LibraryMetadataItem> metadata, bool allowRepair)
        {
            if (metadata != null)
            {
                ProgressJob job = new ProgressJob("Load library data", metadata.Count(), 0);
                await Task.Run(delegate
                {
                    job.Start();

                    foreach (var item in metadata)
                    {
                        if (allowRepair) RepairMetadata(item);
                        LoadMetadata(item, allowRepair);
                        job.UpdateProgress(1);
                    }
                    job.Finish();
                }).ConfigureAwait(false);
            }
        }

        public void LoadMetadata(LibraryMetadataItem item, bool allowRepair)
        {
            SwitchCollectionItem ci = GetTitleByID(item.TitleID);
            if (ci == null)
            {
                ci = LoadTitle(item.TitleID, item.TitleKey, item.Name, 0 /*ignore,only for updates*/);
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
            if (item.RequiredSystemVersion.HasValue) ci.RequiredSystemVersion = item.RequiredSystemVersion;

            // uint?
            if (item.LatestVersion.HasValue) ci.LatestVersion = item.LatestVersion;
            if (item.NumPlayers.HasValue) ci.NumPlayers = item.NumPlayers;

            // byte?
            if (item.MasterKeyRevision.HasValue) ci.MasterKeyRevision = item.MasterKeyRevision;

            // bool?
            if (item.IsFavorite.HasValue) ci.IsFavorite = item.IsFavorite.Value;
            if (item.HasDLC.HasValue) ci.HasDLC = item.HasDLC;
            if (item.HasAmiibo.HasValue) ci.HasAmiibo = item.HasAmiibo;
            if (item.IsDemo.HasValue) ci.IsDemo = item.IsDemo.Value;

            // datetime
            if (item.ReleaseDate.HasValue) ci.ReleaseDate = item.ReleaseDate;
            if (item.Added.HasValue) ci.Added = item.Added;

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
            if (!string.IsNullOrWhiteSpace(item.NsuId)) ci.NsuId = item.NsuId;
            if (!string.IsNullOrWhiteSpace(item.ProductCode)) ci.ProductCode = item.ProductCode;
            if (!string.IsNullOrWhiteSpace(item.ProductId)) ci.ProductId = item.ProductId;
            if (!string.IsNullOrWhiteSpace(item.SLUG)) ci.SLUG = item.SLUG;
            if (!string.IsNullOrWhiteSpace(item.RatingContent)) ci.RatingContent = item.RatingContent;
            if (!string.IsNullOrWhiteSpace(item.Price)) ci.Price = item.Price;
            if (!string.IsNullOrWhiteSpace(item.Region)) ci.Region = item.Region;
            if (!string.IsNullOrWhiteSpace(item.DisplayVersion)) ci.DisplayVersion = item.DisplayVersion;
            if (!string.IsNullOrWhiteSpace(item.OfficialSite)) ci.OfficialSite = item.OfficialSite;

            if (item.Updates != null)
                foreach (var update in item.Updates)
                {
                    if (update.Version > 0)
                    {
                        if (allowRepair) RepairMetadata(update);
                        AddUpdateTitle(update, item.TitleID);
                    }
                }
        }

        private void RepairMetadata(LibraryMetadataItem item)
        {
            if (FileUtils.FileExists(item.Path))
            {
                if (item.State != SwitchCollectionState.OnSwitch && item.State != SwitchCollectionState.Owned)
                {
                    if (item.State != SwitchCollectionState.Hidden)
                    {
                        item.State = SwitchCollectionState.Owned;
                        item.Added = DateTime.Now;
                    }
                    item.Size = FileUtils.GetFileSystemSize(item.Path);
                }
            }
            else if (FileUtils.DirectoryExists(item.Path))
            {
                if (item.State != SwitchCollectionState.Downloaded && item.State != SwitchCollectionState.Unlockable)
                {
                    if (item.State != SwitchCollectionState.Hidden)
                    {
                        if (SwitchTitle.CheckValidTitleKey(item.TitleKey))
                            item.State = SwitchCollectionState.Unlockable;
                        else
                            item.State = SwitchCollectionState.Downloaded;
                        item.Added = DateTime.Now;
                    }
                    item.Size = FileUtils.GetFileSystemSize(item.Path);
                }
            }
            else if (!SwitchTitle.CheckValidTitleKey(item.TitleKey))
            {
                item.Path = null;
                item.Added = null;
                if (SwitchTitle.IsUpdateTitleID(item.TitleID))
                    item.State = SwitchCollectionState.NotOwned;
                else if (item.State != SwitchCollectionState.Hidden && item.State != SwitchCollectionState.NewNoKey)
                    item.State = SwitchCollectionState.NoKey;
            }
            else
            {
                item.Path = null;
                item.Added = null;
                if (item.State != SwitchCollectionState.Hidden && item.State != SwitchCollectionState.New)
                    item.State = SwitchCollectionState.NotOwned;
            }

            if (SwitchTitle.IsDLCID(item.TitleID))
                item.Updates.Clear();

            if ((item.IsDemo ?? false) && item.BoxArtUrl != null)
            {
                item.Icon = null;
                item.BoxArtUrl = null;
            }
        }

        public void ExportLibrary(string exportFile)
        {
            ExportLibrary(exportFile, this.Collection.ToArray());
        }

        public void ExportLibrary(string exportFile, params SwitchCollectionItem[] items)
        {
            if (items == null || items.Length == 0) return;

            using (var stream = FileUtils.OpenWriteStream(exportFile))
            {
                if (exportFile.EndsWith("xml"))
                {
                    XmlAttributeOverrides overrides = new XmlAttributeOverrides();
                    XmlAttributes attribs = new XmlAttributes
                    {
                        XmlIgnore = true
                    };
                    attribs.XmlElements.Add(new XmlElementAttribute("Icon"));
                    attribs.XmlElements.Add(new XmlElementAttribute("State"));
                    attribs.XmlElements.Add(new XmlElementAttribute("Favorite"));
                    attribs.XmlElements.Add(new XmlElementAttribute("Added"));
                    attribs.XmlElements.Add(new XmlElementAttribute("Path"));
                    overrides.Add(typeof(SwitchCollectionItem), attribs);

                    XmlSerializer ser = new XmlSerializer(typeof(SwitchCollectionItem), overrides);
                    ser.Serialize(stream, items);
                }
                else if (exportFile.EndsWith("json"))
                {
                    JObject rootJson = new JObject();
                    foreach (var ci in items)
                    {
                        string tid = ci.TitleId;
                        JObject key = this.CreateGameInfoJson(ci);
                    }
                }
            }
        }

        internal void SaveMetadata(string path)
        {
            path = Path.GetFullPath(path);

            XmlSerializer xml = new XmlSerializer(typeof(SwitchLibrary));

            // Create a new file stream to write the serialized object to a file
            FileStream fs = FileUtils.OpenWriteStream(path);
            xml.Serialize(fs, this);
            fs.Dispose();

            logger.Info($"Finished saving library metadata to {path}");
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
                            if (item.State != SwitchCollectionState.OnSwitch && item.State != SwitchCollectionState.Owned)
                            {
                                item.State = SwitchCollectionState.Owned;
                                item.Added = DateTime.Now;
                            }

                            item.Size = size;
                        }
                        break;
                    case SwitchTitleType.Update:
                        var u = AddUpdateTitle(id, null, uint.Parse(version), null);
                        u.Size = size;
                        u.RomPath = nspFile.FullName;

                        // If you haven't already marked the file as on switch, mark it owned
                        if (u.State != SwitchCollectionState.OnSwitch && u.State != SwitchCollectionState.Owned)
                        {
                            u.State = SwitchCollectionState.Owned;
                            u.Added = DateTime.Now;
                        }
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

        private UpdateCollectionItem AddUpdateTitle(string updateid, string gameid, uint version, string titlekey)
        {
            if (gameid == null)
                gameid = (updateid == null ? null : SwitchTitle.GetBaseGameIDFromUpdate(updateid));
            if (updateid != null && gameid != null)
            {
                var parent = GetTitleByID(gameid);
                UpdateCollectionItem item = parent.GetUpdate(version);
                if (item == null)
                {
                    //uint ver = version / 0x10000;
                    //string name = parent.TitleName + " v" + ver;
                    SwitchUpdate update = new SwitchUpdate(parent.TitleName, updateid, gameid, version, titlekey);
                    item = new UpdateCollectionItem(update);
                    parent.Updates.Add(item);
                    //item = AddOrGetUpdateTitle(update);
                }
                return item;
            }
            return null;
        }

        private UpdateCollectionItem AddUpdateTitle(UpdateMetadataItem update, string parentid)
        {
            if (parentid == null)
                parentid = (update.TitleID == null ? null : SwitchTitle.GetBaseGameIDFromUpdate(update.TitleID));
            if (update.TitleID != null && parentid != null)
            {
                var parent = GetTitleByID(parentid);

                UpdateCollectionItem item = parent.GetUpdate(update.Version);
                if (item == null)
                {
                    SwitchUpdate su = new SwitchUpdate(update.Name, update.TitleID, parentid, update.Version, update.TitleKey);

                    item = AddOrGetUpdateTitle(su);
                }

                if (update.Size.HasValue) item.Size = update.Size;
                if (update.Added.HasValue) item.Added = update.Added;
                if (update.State.HasValue) item.State = update.State.Value;
                if (!string.IsNullOrWhiteSpace(update.Path)) item.RomPath = update.Path;
                if (update.IsFavorite.HasValue) item.IsFavorite = update.IsFavorite.Value;

                return item;
            }
            return null;
        }

        private UpdateCollectionItem AddOrGetUpdateTitle(SwitchUpdate update)
        {
            var parent = GetTitleByID(update.GameID); // Let's try adding this to the base game's list

            if (parent == null)
            {
                logger.Warn("Found an update for a game that doesn't exist.");
                return null;
            }
            else if (parent.Title == null)
            {
                logger.Warn("Found a collection item in the library with a null title.");
                return null;
            }
            else if (parent.Title.IsGame)
            {
                UpdateCollectionItem item = parent.GetUpdate(update.Version);
                if (item == null)
                {
                    item = new UpdateCollectionItem(update);
                    if (parent.Updates == null)
                        parent.Updates = new List<UpdateCollectionItem>();

                    parent.Updates.Add(item);
                    item.Size = 0;
                    item.IsFavorite = false;
                    item.State = SwitchCollectionState.NotOwned;
                }
                return item;
            }
            return null;
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
                        return await DoNspDownloadAndRepack(title, v, dinfo, repack).ConfigureAwait(false);
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
                        return await DoNspDownloadAndRepack(title, v, dinfo, repack).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                // TODO delete directory after
                //dinfo.Delete(true);
            }
        }

        public async Task<string> DoNspDownloadAndRepack(SwitchTitle title, uint version, DirectoryInfo dir, bool repack)
        {
            var nsp = await Loader.DownloadTitle(title, version, dir.FullName, repack).ConfigureAwait(false);

            if (repack && nsp != null)
            {
                string nspPath = await DoNspRepack(title, version, nsp);

                if (this.RemoveContentAfterRepack)
                    dir.Delete(true);

                return nspPath;
            }
            return dir.FullName;
        }

        public async Task<string> DoNspRepack(SwitchTitle title, uint version, NSP nsp)
        {
            string titleName = Miscellaneous.SanitizeFileName(title.Name);

            // format is
            // [DLC] at the start, plus space, if it is DLC - this is already party of the name for DLC, typically
            // title name
            // [UPD] if it is an update
            // [titleid]
            // [vXXXXXX], where XXXXXX is the version number in decimal
            string nspFile = (title.IsDLC && !titleName.StartsWith("[DLC]") ? "[DLC] " : "") + titleName + (title.IsUpdate ? " [UPD]" : " ") + $"[{title.TitleID}][v{version}].nsp";
            string nspPath = $"{this.RomsPath}{Path.DirectorySeparatorChar}{nspFile}";

            // Repack the game files into an NSP
            bool success = await nsp.Repack(nspPath).ConfigureAwait(false);

            // If the NSP failed somehow but the file exists any, remove it
            //if (!success && File.Exists(nspPath))
            //    FileUtils.DeleteFile(nspPath);

            return nspPath;
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

                        titleItem.SetNspFile(dlcPath);
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

                                dlcTitle.SetNspFile(dlcPath);
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

                    if (title.IsUpdate)
                    {
                        string updatePath = await DownloadTitle(titleItem.Title, v, repack, verify);
                        titleItem.RomPath = Path.GetFullPath(updatePath);
                        titleItem.Size = FileUtils.GetFileSystemSize(updatePath);
                        titleItem.Added = DateTime.Now;
                        titleItem.State = SwitchCollectionState.Owned;
                    }
                    else if (title.IsGame)
                    {
                        // If a version greater than 0 is selected, download it and every version below it
                        while (v > 0)
                        {
                            var updateItem = titleItem.GetUpdate(v);
                            string updatePath = await DownloadTitle(updateItem.Title, v, repack, verify);

                            updateItem.SetNspFile(updatePath);
                            v -= 0x10000;
                        }
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

                        titleItem.SetNspFile(romPath);
                    }
                    if (options == DownloadOptions.BaseGameAndUpdate || options == DownloadOptions.BaseGameAndUpdateAndDLC)
                        goto case DownloadOptions.UpdateOnly;
                    else if (options == DownloadOptions.BaseGameAndDLC)
                        goto case DownloadOptions.AllDLC;
                    break;
            }
        }

        /// <summary>
        /// Gets a title icon. If it isn't cached locally, gets it from nintendo. Only loads a local image if downloadRemote is false, but will download
        /// from the CDN if downloadRemote is true.
        /// </summary>
        /// <param name="title">Title whose icon you wish to load</param>
        /// <param name="downloadRemote">If true, loads the image from nintendo if it isn't found in cache</param>
        /// <returns></returns>
        public async Task UpdateInternalMetadata(SwitchTitle title)
        {
            string img = GetLocalImage(title.TitleID);
            //if (img == null && downloadRemote && SwitchTitle.IsBaseGameID(title.TitleID))

            if (title.IsMissingMetadata)
            {
                string titleDir = TempPath + Path.DirectorySeparatorChar + title.TitleID;
                await Loader.DownloadAndUpdateTitleMetadata(title, titleDir).ConfigureAwait(false);
                if (title.IsMissingIconData) img = GetLocalImage(title.TitleID);
            }

            // Set cached image
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

        public async Task<JObject> ReadEShopAPI(SwitchTitle title, string region, string lang)
        {
            var response = await Loader.GetEshopData(title, region, lang).ConfigureAwait(false);
            if (response != null)
            {
                string r = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JObject.Parse(r);
            }

            return null;
        }

        public JObject ReadEShopPage(SwitchCollectionItem title, string region)
        {
            string link = title?.Title?.EshopLink;
            if (link == null) return null;
            var doc = Dcsoup.Parse(new Uri(link), 5000);

            JObject json = new JObject();
            json.Add("titleid", title.TitleId);
            json.Add("eshop_link", link);

            // irritating, what comes up when you look up a demo is the page for the main game
            if (title.IsDemo)
            {
                var demoLink = doc.Select("a#demo-download]");
                if (demoLink != null)
                    json.Add("nsuid", demoLink.Attr("data-nsuid"));
            }
            if (title.Size.HasValue) json.Add("Game_size", title.Size.Value);

            string description = doc.Select("section#overview div[itemprop=description] p").Text;
            json.Add("description", description);

            string intro = doc.Select("section#overview h1 p").Text;
            json.Add("intro", intro);

            var hasDLC = doc.Select("span.dlc-info");
            if (hasDLC != null && hasDLC.Count > 0 && !title.IsDemo)
                json.Add("dlc", "true");
            else
                json.Add("dlc", "false");

            var hasAmiibo = doc.Select("section#amiibo");
            if (hasAmiibo != null && hasAmiibo.Count > 0 && !title.IsDemo)
                json.Add("amiibo_compatibility", "true");
            else
                json.Add("amiibo_compatibility", "false");

            if (!title.IsDemo)
            {
                var boxart = doc.Select("span.boxart img");
                json.Add("front_box_art", boxart.Attr("src"));
            }

            var site = doc.Select("a[itemprop='URL sameAs']");
            json.Add("official_site", site.Attr("href"));

            var date_added = DateTime.Now.ToString("yyyyMMdd");
            json.Add("date_added", date_added);

            var rating_content = doc.Select("span.esrb-rating span.descriptors div").Text;
            json.Add("content", rating_content);

            var list = doc.Select("div.flex dl div");
            foreach (var item in list)
            {
                var dt = item.Select("dt").Text;
                var dd = item.Select("dd").Text;
                switch (dt)
                {
                    case "No. of Players":
                        json.Add("number_of_players", dd);
                        break;

                    case "Release Date":

                        json.Add("release_date_string", dd);
                        if (DateTime.TryParseExact(dd, "MMM dd, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime rDate))
                        {
                            string release_date_iso = rDate.ToString("yyyyMMdd");
                            json.Add("release_date_iso", release_date_iso);
                        }
                        break;

                    case "Category":
                        json.Add("category", dd);
                        break;

                    case "Publisher":
                        json.Add("publisher", dd);
                        break;

                    case "Developer":
                        json.Add("developer", dd);
                        break;
                }
            }

            var scripts = doc.Select("script");
            foreach (var scr in scripts)
            {
                string text = scr.Html;
                if (text.Contains("window.game"))
                {
                    Match m = Regex.Match(text, "window\\.game = Object\\.freeze\\(([\\S\\s]+?)\\);");
                    if (m.Success && m.Groups.Count > 1)
                    {
                        text = m.Groups[1].Value;
                        var gj = JObject.Parse(text);

                        if (!json.ContainsKey("product_id")) json.Add("product_id", gj.Value<string>("id"));
                        if (!json.ContainsKey("publisher")) json.Add("title", gj.Value<string>("publisher"));
                        if (!json.ContainsKey("category")) json.Add("category", gj.Value<string>("genre"));
                        if (!json.ContainsKey("slug")) json.Add("slug", gj.Value<string>("slug"));
                        if (!json.ContainsKey("nsuid")) json.Add("nsuid", gj.Value<string>("nsuid"));
                        if (!json.ContainsKey("game_code")) json.Add("game_code", gj.Value<string>("productCode"));

                        if (!json.ContainsKey("title"))
                        {
                            string name = gj.Value<string>("title");
                            if (title.IsDemo)
                                name += " (Demo)";
                            json.Add("title", name);
                        }

                        if (!json.ContainsKey("price") && !title.IsDemo)
                        {
                            string price = gj.Value<string>("msrp");
                            json.Add("price", price);
                            json.Add(region + "_price", price);
                        }

                        if (!json.ContainsKey("release_date_iso") && DateTime.TryParse(gj.Value<string>("releaseDate"), out DateTime rdate))
                        {
                            string release_date_iso = rdate.ToString("yyyyMMdd");
                            json.Add("release_date_iso", release_date_iso);
                            string release_date_string = rdate.ToString("MMM dd, yyyy");
                            json.Add("release_date_string", release_date_string);
                        }
                        if (!json.ContainsKey("rating"))
                        {
                            string rating = gj.Value<string>("esrbRating")?.ToUpper();
                            switch (rating)
                            {
                                case "E": json.Add("rating", "Everyone"); break;
                                case "E10+": json.Add("rating", "Everyone 10 and older"); break;
                                case "T": json.Add("rating", "Teen"); break;
                                case "M": json.Add("rating", "Mature"); break;
                                case "AO": json.Add("rating", "Adults Only"); break;
                            }
                        }
                    }
                }
            }
            return json;
        }

        public async Task UpdateEShopData(SwitchCollectionItem item, string lang = "en")
        {
            var title = item?.Title;
            string region = item?.Region ?? this.PreferredRegion;

            if (title != null && title.IsGame)
            {
                if (Loader.EShopLoginToken == null)
                {
                    JObject data = ReadEShopPage(item, region);
                    var meta = ParseGameInfoJson(title?.TitleID, data);
                    LoadMetadata(meta, false);
                }
                else
                {
                    JObject data = await ReadEShopAPI(title, region, lang).ConfigureAwait(false);
                    UpdateEShopData(item, data);
                }
            }
        }

        public void UpdateEShopData(SwitchCollectionItem item, JObject json)
        {
            var title = item?.Title;
            if (title != null && json != null && title.IsGame)
            {
                title.Name = json.Value<string>("formal_name");
                title.Description = json.Value<string>("description");
                title.Category = json.Value<string>("genre");
                title.Intro = json.Value<string>("catch_copy");
                title.HasDLC = json.Value<bool>("in_app_purchase");

                string disclaimer = json.Value<string>("disclaimer");
                string featureDesc = json.Value<string>("network_feature_description");
                if ((disclaimer != null && disclaimer.Contains("amiibo™ compatible game")) || (featureDesc != null && featureDesc.Contains("amiibo")))
                    title.HasAmiibo = true;

                if (!item.Size.HasValue || !FileUtils.FileExists(item.RomPath))
                {
                    string sSize = json.Value<string>("total_rom_size");
                    if (long.TryParse(sSize, out long size))
                        item.Size = size;
                }

                if (DateTime.TryParseExact(json?.Value<string>("release_date_on_eshop"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime rDate))
                    title.ReleaseDate = rDate;

                var pubtoken = json.GetValue("publisher");
                if (pubtoken != null)
                    title.Publisher = pubtoken.Value<string>("name");

                var ratingToken = json["rating_info"]["rating"];
                if (ratingToken != null)
                {
                    string rating = ratingToken.Value<string>("name");
                    switch (rating)
                    {
                        case "E": title.Rating = "Everyone"; break;
                        case "E10+": title.Rating = "Everyone 10 and older"; break;
                        case "T": title.Rating = "Teen"; break;
                        case "M": title.Rating = "Mature"; break;
                        case "AO": title.Rating = "Adults Only"; break;
                            // no such thing as AO on switch right?
                    }
                }

                var ratingContentToken = json["rating_info"]["content_descriptors"];
                if (ratingContentToken != null)
                {
                    List<string> descriptors = new List<string>();
                    foreach (var desc in ratingContentToken)
                    {
                        string name = desc.Value<string>("name");
                        if (!string.IsNullOrWhiteSpace(name))
                            descriptors.Add(name);
                    }
                    if (descriptors.Count > 0)
                        title.RatingContent = string.Join(",", descriptors);
                }

                var playerstoken = json.GetValue("player_number");
                if (playerstoken != null)
                {
                    string offPlayers = playerstoken.Value<string>("offline_max");
                    string onPlayers = playerstoken.Value<string>("online_max");
                    if (uint.TryParse(offPlayers, out uint np) && uint.TryParse(onPlayers, out uint npo))
                        title.NumPlayers = Math.Max(np, npo);
                }

                // todo rating info
                // todo size?
                // todo amiibo?
                // todo check a demo, see if it is flagged as a demo, otherwise find
                // something with a demo and see if it shows its demo so i can mark it as demo
                // could get images here but why bother, lots of other places
                string bannerImageUrl = $"https://bugyo.hac.lp1.eshop.nintendo.net/{json.Value<string>("hero_banner_url")}?w=640";
                // TODO: banner image url?

                var sshotsToken = json["screenshots"];
                if (sshotsToken != null)
                    foreach (var sshot in sshotsToken)
                    {
                        var imagesToken = sshot["images"];
                        foreach (var img in imagesToken)
                        {
                            string sshotUrl = $"https://bugyo.hac.lp1.eshop.nintendo.net/{img.Value<string>("url")}?w=640";
                            // TODO: screenshots?
                        }
                    }
            }
        }

        public List<SwitchCollectionItem> UpdateNutFile(string filename)
        {
            var newTitles = new List<SwitchCollectionItem>();
            var lines = File.ReadLines(filename);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var entry = line.Trim();

                if (entry.StartsWith("#")) continue;

                string[] split = entry.Split('|');

                string id = split.Length > 0 ? split[0]?.Trim() : null;
                if (id?.Length != 16)
                    continue;
                else
                    id = id?.Substring(0, 16);

                string rightsId = split.Length > 1 ? split[1]?.Trim()?.Substring(0, 32) : null;
                string key = split.Length > 2 ? split[2]?.Trim()?.Substring(0, 32) : null;

                bool isUpdate = (split.Length > 3 && "1".Equals(split[3]?.Trim())) || SwitchTitle.IsUpdateTitleID(id);
                bool isDLC = (split.Length > 4 && "1".Equals(split[4]?.Trim())) || SwitchTitle.IsDLCID(id);
                bool isDemo = split.Length > 5 && "1".Equals(split[5]?.Trim());

                string name = split.Length > 6 ? split[6]?.Trim() : null;
                uint version = 0;
                if (split.Length > 7)
                    uint.TryParse(split[7]?.Trim(), out version);

                string region = split.Length > 8 ? split[8]?.Trim() : null;
                bool retailOnly = split.Length > 9 && "1".Equals(split[9]?.Trim());
                string englishName = split.Length > 10 ? split[10]?.Trim() : null;

                // Skip retail only (not on eshop)
                if (retailOnly) continue;

                SwitchCollectionItem item = null;

                if (isUpdate)
                {
                    if (version == 0) continue;
                    if (!SwitchTitle.CheckValidTitleKey(key)) continue;

                    var parent = GetBaseTitleByID(id);
                    if (parent == null) continue; // This is pretty much always because of an update that references a retail-only ID that was skipped previously
                    else item = parent.GetUpdate(version);

                    if (item == null)
                        item = LoadTitle(id, key, name, version);
                }
                else
                {
                    item = GetTitleByID(id);
                    if (item == null) // not interested in adding new asian games
                    {
                        string pregion = this.PreferredRegion ?? "US";
                        if (pregion.Equals(region))
                        {
                            item = LoadTitle(id, key, name, version);
                            if (item != null)
                            {
                                newTitles.Add(item);

                                // If the key is missing, mark it as such, otherwise it is a properly working New title
                                if (item.Title.IsTitleKeyValid)
                                    item.State = SwitchCollectionState.New;
                                else
                                    item.State = SwitchCollectionState.NewNoKey;
                            }
                        }
                    }
                    else
                    {
                        // If we replace an invalid title key with a valid one
                        if (!item.Title.IsTitleKeyValid && SwitchTitle.CheckValidTitleKey(key))
                        {
                            if (item.State == SwitchCollectionState.NoKey)
                                item.State = SwitchCollectionState.New;
                            else if (item.State == SwitchCollectionState.Downloaded)
                                item.State = SwitchCollectionState.Unlockable;
                            newTitles.Add(item);
                        }
                    }
                }
                if (item == null) continue;

                if (SwitchTitle.CheckValidTitleKey(key)) item.TitleKey = key;
                if (string.IsNullOrWhiteSpace(item.TitleName)) item.TitleName = name;

                item.Region = region;

                englishName = " (" + englishName + ")";
                if ("JP".Equals(region) && !item.TitleName.EndsWith(englishName))
                    item.TitleName += englishName;

                item.IsDemo = isDemo;
            }

            return newTitles;
        }

        public ICollection<SwitchCollectionItem> UpdateTitleKeysFile(string file)
        {
            var lines = File.ReadLines(file);

            var newTitles = new List<SwitchCollectionItem>();
            foreach (var line in lines)
            {
                string[] split = line.Split('|');
                string tid = split?.Length > 0 && string.IsNullOrWhiteSpace(split[0]) ? null : split[0];
                if (tid == null || tid.Length < 16)
                    continue;
                else
                    tid = tid.Trim().Substring(0, 16).ToLower();

                SwitchCollectionItem item = tid == null ? null : GetTitleByID(tid);

                // We only care about new titles, and ignore messed up entries that are missing a title ID for some reason
                if (tid != null)
                {
                    string tkey = split?.Length > 1 && string.IsNullOrWhiteSpace(split[1]) ? null : split[1]?.Trim()?.Substring(0, 32).ToLower();
                    string name = split?.Length > 2 && string.IsNullOrWhiteSpace(split[2]) ? null : split[2]?.Trim();
                    if (item == null)
                    {
                        // New title!!

                        // A missing tkey can always be added in later
                        item = LoadTitle(tid, tkey, name, 0);

                        // If the key is missing, mark it as such, otherwise it is a properly working New title
                        if (item.Title.IsTitleKeyValid)
                            item.State = SwitchCollectionState.New;
                        else
                            item.State = SwitchCollectionState.NewNoKey;

                        newTitles.Add(item);
                    }
                    else
                    {
                        // If we replace an invalid title key with a valid one
                        if (!item.Title.IsTitleKeyValid && SwitchTitle.CheckValidTitleKey(tkey))
                        {
                            if (item.State == SwitchCollectionState.NoKey)
                                item.State = SwitchCollectionState.New;
                            else if (item.State == SwitchCollectionState.Downloaded)
                                item.State = SwitchCollectionState.Unlockable;
                            newTitles.Add(item);
                        }

                        // Existing title, replace title key
                        if (SwitchTitle.CheckValidTitleKey(tkey))
                        {
                            // hack to not update torna dlc key
                            if (!tid.Equals("0100e95004039001"))
                                item.Title.TitleKey = tkey;
                        }

                        // Replace existing name only if it is missing
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
        private SwitchCollectionItem LoadTitle(string tid, string tkey, string name, uint version)
        {
            if (!SwitchTitle.CheckValidTitleID(tid)) return null;
            else tid = tid.ToLower();

            if (string.IsNullOrWhiteSpace(tkey)) tkey = null;
            else tkey = tkey.ToLower();

            if (SwitchTitle.IsBaseGameID(tid))
            {
                var item = NewGame(name, tid, tkey);
                if (name.ToUpper().Contains("DEMO") || name.ToUpper().Contains("TRIAL VER") || name.ToUpper().Contains("SPECIAL TRIAL"))
                    item.IsDemo = true;

                var game = item?.Title as SwitchGame;

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
                        logger.Warn("Couldn't find base game ID {baseGameID} for DLC {dlc.Name}");
                }
            }
            else if (SwitchTitle.IsUpdateTitleID(tid))
            {
                return AddUpdateTitle(tid, null, version, tkey);
            }
            return null;
        }

        public async Task UpdateVersions()
        {
            await UpdateVersions(this.Collection).ConfigureAwait(false);
        }

        public async Task UpdateVersions(ICollection<SwitchCollectionItem> titles)
        {
            if (this.Collection == null || this.Loader == null) return;

            Dictionary<string, uint> versions = null;
            try
            {
                // I figure it might be faster to get individual versions for a small number instead of all at once
                if (titles.Count > 0) versions = await Loader.GetLatestVersions().ConfigureAwait(false);

                foreach (var i in titles)
                {
                    var t = i.Title;

                    if (versions != null && versions.TryGetValue(t.TitleID, out uint ver))
                    {
                        UpdateVersion(t, ver);
                    }
                    else
                    {
                        ver = await Loader.GetLatestVersion(t).ConfigureAwait(false);
                        UpdateVersion(t, ver);
                    }
                }
            }
            finally
            {

            }
        }

        public void UpdateVersion(SwitchTitle title, uint ver)
        {
            if (!title.LatestVersion.HasValue || ver > title.LatestVersion.Value)
            {
                title.LatestVersion = ver;

                if (title.IsDLC) return;

                string updateid = SwitchTitle.GetUpdateIDFromBaseGame(title.TitleID);
                for (uint i = 0x10000; i <= title.LatestVersion; i += 0x10000)
                {
                    AddUpdateTitle(updateid, title.TitleID, i, null);
                }
            }
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

        public JObject CreateGameInfoJson(SwitchCollectionItem title)
        {
            string link = title?.Title?.EshopLink;
            if (link == null) return null;
            var doc = Dcsoup.Parse(new Uri(link), 5000);

            JObject json = new JObject();
            json.Add("titleid", title.TitleId);
            json.Add("title", title.TitleName);
            json.Add("eshop_link", link);
            if (title.Size.HasValue) json.Add("Game_size", title.Size.Value);
            json.Add("description", title.Description);
            json.Add("intro", title.Intro);
            json.Add("dlc", title.HasDLC ?? false);
            json.Add("amiibo_compatibility", title.HasAmiibo ?? false);
            json.Add("demo", title.IsDemo);
            if (title.RequiredSystemVersion.HasValue) json.Add("required_system_version", title.RequiredSystemVersion.Value);
            if (title.MasterKeyRevision.HasValue) json.Add("master_key_revision", title.MasterKeyRevision.Value);
            json.Add("region", title.Region);
            json.Add("front_box_art", title.BoxArtUrl);
            json.Add("official_site", title.OfficialSite);
            json.Add("date_added", DateTime.Now.ToString("yyyyMMdd"));
            json.Add("content", title.RatingContent);
            json.Add("release_date_string", title.ReleaseDate?.ToString("MMM dd, yyyy"));
            json.Add("release_date_iso", title.ReleaseDate?.ToString("yyyyMMdd"));
            json.Add("category", title.Category);
            json.Add("publisher", title.Publisher);
            json.Add("developer", title.Developer);
            json.Add("nsuid", title.NsuId);
            json.Add("product_id", title.ProductId);
            json.Add("slug", title.Title.SLUG);
            json.Add("game_code", title.ProductCode);
            json.Add("price", title.Price);
            json.Add(title.Region + "_price", title.Price);
            json.Add("rating", title.Rating);

            switch (title.NumPlayers)
            {
                case 1: json.Add("number_of_players", "1 player"); break;
                case 2: json.Add("number_of_players", "2 players simultaneous"); break;
                default: json.Add("number_of_players", $"up to {title.NumPlayers} players"); break;
            }

            return json;
        }

        public LibraryMetadataItem ParseGameInfoJson(string tid, JToken jsonGame)
        {
            if (SwitchTitle.IsBaseGameID(tid) && jsonGame.HasValues)
            {
                LibraryMetadataItem game = new LibraryMetadataItem();

                game.TitleID = jsonGame?.Value<string>("titleid");
                game.Name = jsonGame?.Value<string>("title");
                game.Publisher = jsonGame?.Value<string>("publisher");
                game.Developer = jsonGame?.Value<string>("developer");
                game.Description = jsonGame?.Value<string>("description");
                game.Intro = jsonGame?.Value<string>("intro");
                game.Category = jsonGame?.Value<string>("category");
                game.BoxArtUrl = jsonGame?.Value<string>("front_box_art");
                game.Rating = jsonGame?.Value<string>("rating");
                game.NsuId = jsonGame?.Value<string>("nsuid");
                game.ProductCode = jsonGame?.Value<string>("game_code");
                game.RatingContent = jsonGame?.Value<string>("content");
                game.OfficialSite = jsonGame?.Value<string>("official_site");
                game.ProductId = jsonGame?.Value<string>("product_id");
                game.SLUG = jsonGame?.Value<string>("slug");
                game.Region = jsonGame?.Value<string>("region");

                string price = jsonGame?.Value<string>("price");
                if (price == null)
                {
                    price = jsonGame?.Value<string>("US_price");
                }
                game.Price = price;

                string sNumPlayers = jsonGame?.Value<string>("number_of_players");
                if (sNumPlayers != null)
                {
                    if (sNumPlayers.StartsWith("1"))
                    {
                        game.NumPlayers = 1;
                    }
                    else if (sNumPlayers.StartsWith("2")) game.NumPlayers = 2;
                    else
                    {
                        sNumPlayers = sNumPlayers.Replace("up to ", string.Empty);
                        sNumPlayers = sNumPlayers.Replace(" players", string.Empty);
                        if (uint.TryParse(sNumPlayers, out uint np))
                            game.NumPlayers = np;
                    }
                }

                string s = jsonGame?.Value<string>("dlc");
                if (!string.IsNullOrWhiteSpace(s) && bool.TryParse(s, out bool hasDlc))
                    game.HasDLC = hasDlc;
                else
                    game.HasDLC = false;

                s = jsonGame?.Value<string>("amiibo_compatibility");
                if (!string.IsNullOrWhiteSpace(s) && bool.TryParse(s, out bool hasA))
                    game.HasAmiibo = hasA;
                else
                    game.HasAmiibo = false;

                s = jsonGame?.Value<string>("demo");
                if (!string.IsNullOrWhiteSpace(s) && bool.TryParse(s, out bool demo))
                    game.IsDemo = demo;
                else
                    game.IsDemo = false;

                string sSize = jsonGame?.Value<string>("Game_size");
                if (long.TryParse(sSize, out long size))
                    game.Size = size;

                string sRequiredVersion = jsonGame?.Value<string>("required_system_version");
                if (uint.TryParse(sRequiredVersion, out uint requiredVersion))
                    game.RequiredSystemVersion = requiredVersion;

                string sRevision = jsonGame?.Value<string>("master_key_revision");
                if (byte.TryParse(sRevision, out byte revision))
                    game.MasterKeyRevision = revision;

                if (DateTime.TryParseExact(jsonGame?.Value<string>("release_date_iso"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime rDate))
                    game.ReleaseDate = rDate;
                return game;
            }
            return null;
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
