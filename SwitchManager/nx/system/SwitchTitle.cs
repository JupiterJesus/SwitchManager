using SwitchManager.nx.library;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.system
{
    public abstract class SwitchTitle : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        private string name;
        public string Name
        {
            get { return this.name; }
            set { this.name = value; NotifyPropertyChanged("Name"); }
        }

        private string dev;
        public string Developer
        {
            get { return this.dev; }
            set { this.dev = value; NotifyPropertyChanged("Developer"); }
        }

        private string desc;
        public string Description
        {
            get { return this.desc; }
            set { this.desc = value; NotifyPropertyChanged("Description"); }
        }

        private string region;
        public string Region
        {
            get { return this.region; }
            set { this.region = value; NotifyPropertyChanged("Region"); }
        }

        private DateTime? releaseDate;
        public DateTime? ReleaseDate
        {
            get { return this.releaseDate; }
            set { this.releaseDate = value; NotifyPropertyChanged("ReleaseDate"); }
        }

        private string titlekey;
        public string TitleKey
        {
            get { return titlekey; }
            set { this.titlekey = value; NotifyPropertyChanged("TitleKey"); }
        }

        private string titleId;
        public string TitleID
        {
            get { return titleId; }
            set { this.titleId = value; NotifyPropertyChanged("TitleID"); }
        }
        
        public SwitchTitleType Type
        {
            get
            {
                if (IsDemo) return SwitchTitleType.Demo;
                else if (IsDLC) return SwitchTitleType.DLC;
                else if (IsUpdate) return SwitchTitleType.Update;
                else if (IsGame) return SwitchTitleType.Game;
                else return SwitchTitleType.Unknown;
            }
        }

        public string icon;
        public string Icon
        {
            get { return this.BoxArtUrl ?? this.icon; }
            set { this.icon = value; NotifyPropertyChanged("Icon"); }
        }

        private string boxArtUrl;
        public string BoxArtUrl
        {
            get { return boxArtUrl; }
            set { this.boxArtUrl = value; NotifyPropertyChanged("BoxArtUrl"); NotifyPropertyChanged("Icon"); }
        }

        public uint BaseVersion { get { return 0; } }

        private uint? latestVersion;
        private List<uint> versions = new List<uint>();
        public uint? LatestVersion 
        {
            get { return latestVersion; }
            set
            {
                if (this.latestVersion != value)
                {
                    this.latestVersion = value;
                    this.versions.Clear();
                    GetAllVersions(this.latestVersion ?? 0, this.versions);
                    NotifyPropertyChanged("LatestVersion");
                    NotifyPropertyChanged("Versions");
                }
            }
        }

        public List<uint> Versions
        {
            get { return this.versions.Count > 0 ? versions : new List<uint> { 0 }; }
        }

        private string price;
        public string Price
        {
            get { return price; }
            set { this.price = value; NotifyPropertyChanged("Price"); }
        }

        private string code;
        public string Code
        {
            get { return code; }
            set { this.code = value; NotifyPropertyChanged("Code"); }
        }

        private string nsuId;
        public string NsuId
        {
            get { return nsuId; }
            set { this.nsuId = value; NotifyPropertyChanged("NsuId"); }
        }

        private string numPlayers;
        public string NumPlayers
        {
            get { return numPlayers; }
            set { this.numPlayers = value; NotifyPropertyChanged("NumPlayers"); }
        }

        private string rating;
        public string Rating
        {
            get { return rating; }
            set { this.rating = value; NotifyPropertyChanged("Rating"); }
        }

        private string ratingContent;
        public string RatingContent
        {
            get { return ratingContent; }
            set { this.ratingContent = value; NotifyPropertyChanged("RatingContent"); }
        }

        private string category;
        public string Category
        {
            get { return category; }
            set { this.category = value; NotifyPropertyChanged("Category"); }
        }

        private bool? hasAmiibo;
        public bool? HasAmiibo
        {
            get { return hasAmiibo; }
            set { this.hasAmiibo = value; NotifyPropertyChanged("HasAmiibo"); }
        }

        private bool? hasDLC;
        public bool? HasDLC
        {
            get { return hasDLC; }
            set { this.hasDLC = value; NotifyPropertyChanged("HasDLC"); }
        }

        private string intro;
        public string Intro
        {
            get { return intro; }
            set { this.intro = value; NotifyPropertyChanged("Intro"); }
        }

        private string publisher;
        public string Publisher
        {
            get { return publisher; }
            set { this.publisher = value; NotifyPropertyChanged("Publisher"); }
        }

        private string displayVersion;
        public string DisplayVersion
        {
            get { return displayVersion; }
            set { this.displayVersion = value; NotifyPropertyChanged("DisplayVersion"); }
        }

        private bool isDemo;
        public bool IsDemo
        {
            get { return isDemo; }
            set { this.isDemo = value; NotifyPropertyChanged("IsDemo"); }
        }

        private long? requiredSystemVersion;
        public long? RequiredSystemVersion
        {
            get { return requiredSystemVersion; }
            set { this.requiredSystemVersion = value; NotifyPropertyChanged("RequiredSystemVersion"); }
        }

        private byte? masterKeyRevision;
        public byte? MasterKeyRevision
        {
            get { return masterKeyRevision; }
            set { this.masterKeyRevision = value; NotifyPropertyChanged("MasterKeyRevision"); }
        }

        public string RequiredFirmware
        {
            get { return SwitchFirmware.VersionToString(requiredSystemVersion); }
        }

        public abstract bool IsGame { get; }
        public abstract bool IsDLC { get; }
        public abstract bool IsUpdate { get; }
        public bool IsTitleKeyValid { get { return SwitchTitle.CheckValidTitleKey(this.titlekey); }  }

        internal SwitchTitle(string name, string titleid, string titlekey)
        {
            Name = name;
            TitleID = titleid;
            TitleKey = titlekey;
            LatestVersion = 0;
        }

        /// <summary>
        /// Checks if the given Title ID refers to a Title Update.
        /// </summary>
        /// <param name="titleID">Title ID</param>
        /// <returns>true if the ID refers to and can be used for downloading game updates.</returns>
        internal static bool IsUpdateTitleID(string titleID)
        {
            return titleID?.EndsWith("800") ?? false;
        }

        /// <summary>
        /// Checks if the given Title ID refers to a Base Game.
        /// </summary>
        /// <param name="titleID">Title ID</param>
        /// <returns>true if the ID refers to and can be used for downloading a full game title.</returns>
        internal static bool IsBaseGameID(string titleID)
        {
            return titleID?.EndsWith("000") ?? false;
        }

        /// <summary>
        /// Checks if the given Title ID refers to a piece of DLC.
        /// </summary>
        /// <param name="titleID">Title ID</param>
        /// <returns>true if the ID refers to and can be used for downloading game DLC.</returns>
        internal static bool IsDLCID(string titleID)
        {
            return !IsBaseGameID(titleID) && !IsUpdateTitleID(titleID);
        }

        /// <summary>
        /// Gets the Title ID of a base game, given the Title ID of its updates.
        /// </summary>
        /// <param name="titleID">Title ID of game update.</param>
        /// <returns></returns>
        internal static string GetBaseGameIDFromUpdate(string titleID)
        {
            return titleID.Substring(0, 13) + "000";
        }

        /// <summary>
        /// Gets the Title ID of a base game, given the Title ID of any of its DLC.
        /// </summary>
        /// <param name="titleID">Title ID of DLC</param>
        /// <returns></returns>
        internal static string GetBaseGameIDFromDLC(string titleID)
        {
            string idBase = titleID.Substring(0, 12); // first 12 characters of DLC's TID
            string idEnd = titleID.ElementAt(12).ToString(); // 13th character for DLC is 1 higher than 13th character of its base game...
            byte nIdEnd = Convert.ToByte(idEnd, 16); // Parse to a number so we can subtract...
            
            string baseGameID = string.Format("{0}{1}000", idBase, (nIdEnd - 1).ToString("x")); // Combine first 12, then 13th character (less 1) in hex, then 000 (all titles end in 000)
            return baseGameID;
        }

        /// <summary>
        /// Gets the Title ID of a game's updates, given the Title ID of its base game.
        /// </summary>
        /// <param name="titleID">Title ID of game update.</param>
        /// <returns></returns>
        internal static string GetUpdateIDFromBaseGame(string titleID)
        {
            return titleID.Substring(0, 13) + "800";
        }

        internal virtual SwitchUpdate GetUpdateTitle(uint version, string titlekey = null)
        {
            SwitchUpdate title = new SwitchUpdate(this.name, this.TitleID, version, titlekey);
            
            return title;
        }

        /// <summary>
        /// Converts a single version number into a list of all available versions.
        /// </summary>
        /// <param name="versionNo"></param>
        /// <returns></returns>
        public static List<uint> GetAllVersions(uint versionNo)
        {
            var versions = new List<uint>();
            GetAllVersions(versionNo, versions);
            return versions;
        }

        /// <summary>
        /// Converts a single version number into a list of all available versions.
        /// </summary>
        /// <param name="versionNo"></param>
        /// <returns></returns>
        public static void GetAllVersions(uint versionNo, List<uint> versions)
        {
            for (uint v = versionNo; v > 0; v -= 0x10000)
            {
                versions.Add(v);
            }

            versions.Add(0);
        }

        public static bool CheckValidTitleKey(string tkey)
        {
            string zerokey = "00000000000000000000000000000000";
            if (string.IsNullOrWhiteSpace(tkey) || tkey.Length != 32) return false;
            if (zerokey.Equals(tkey)) return false;

            return true;
        }

        public override string ToString()
        {
            if (TitleID == null && Name == null)
                return "Unknown Title";
            else if (TitleID == null)
                return Name;
            else if (Name == null)
                return "[" + TitleID + "]";
            else
                return Name + " [" + TitleID + "]";
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is SwitchTitle))
                return false;

            SwitchTitle other = obj as SwitchTitle;
            if (TitleID == null && other.TitleID == null)
                return true;

            if (TitleID == null || other.TitleID == null)
                return false;

            return TitleID.Equals(other.TitleID);
        }

        public override int GetHashCode()
        {
            return TitleID?.GetHashCode() ?? 0;
        }
    }
}
