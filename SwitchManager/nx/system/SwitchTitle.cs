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

        public SwitchImage icon;
        public SwitchImage Icon
        {
            get { return this.icon; }
            set { this.icon = value; NotifyPropertyChanged("Icon"); }
        }

        public uint BaseVersion { get { return 0; } }

        private uint latestVersion;
        private ObservableCollection<uint> versions = new ObservableCollection<uint>();
        public uint LatestVersion 
        {
            get { return latestVersion; }
            set { this.latestVersion = value;  this.versions.Clear(); GetAllVersions(this.latestVersion, this.versions); NotifyPropertyChanged("LatestVersion"); NotifyPropertyChanged("Versions"); }
        }

        public Collection<uint> Versions
        {
            get { return this.versions.Count > 0 ? versions : new ObservableCollection<uint> { 0 }; }
        }

        public abstract bool IsGame { get; }
        public abstract bool IsDLC { get; }
        public abstract bool IsUpdate { get; }
        public abstract bool IsDemo { get; }
        public bool IsTitleKeyValid { get { return !string.IsNullOrWhiteSpace(TitleKey) && TitleKey.Length == 32; }  }

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
        public static Collection<uint> GetAllVersions(uint versionNo)
        {
            var versions = new ObservableCollection<uint>();
            GetAllVersions(versionNo, versions);
            return versions;
        }

        /// <summary>
        /// Converts a single version number into a list of all available versions.
        /// </summary>
        /// <param name="versionNo"></param>
        /// <returns></returns>
        public static void GetAllVersions(uint versionNo, Collection<uint> versions)
        {
            for (uint v = versionNo; v > 0; v -= 0x10000)
            {
                versions.Add(v);
            }

            versions.Add(0);
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
