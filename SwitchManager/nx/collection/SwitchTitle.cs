using SwitchManager.nx.img;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.collection
{
    public class SwitchTitle : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string name;
        public string Name
        {
            get { return this.name; }
            set
            {
                this.name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
            }
        }
        public string TitleKey { get; set; }
        public string TitleID { get; set; }
        public SwitchTitleType Type { get; set; }

        public SwitchImage icon;
        public SwitchImage Icon
        {
            get { return this.icon; }
            set
            {
                this.icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Icon"));
            }
        }

        public ulong Size { get; set; }

        public ObservableCollection<string> DLC { get; set; }
        public ObservableCollection<string> Updates { get; set;  }
        public ObservableCollection<uint> Versions { get; set; }

        internal SwitchTitle(string name, string titleid, string titlekey)
        {
            Name = name;
            TitleID = titleid;
            TitleKey = titlekey;
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
    }
}
