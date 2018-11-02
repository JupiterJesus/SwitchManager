using SwitchManager.nx.system;
using System;
using System.Xml.Serialization;

namespace SwitchManager.nx.library
{
    /// <summary>
    /// 
    /// </summary>
    public class UpdateCollectionItem : SwitchCollectionItem
    {
        private SwitchUpdate update;
        
        public override bool ShouldSerializeHasDLC() { return false; }
        public override bool ShouldSerializeIsDemo() { return false; }
        public override bool ShouldSerializeHasAmiibo() { return false; }
        public override bool ShouldSerializeReleaseDate() { return false; }
        public override bool ShouldSerializeLatestVersion() { return false; }
        public override bool ShouldSerializeNumPlayers() { return false; }

        /// <summary>
        /// Default constructor. I don't like these but XmlSerializer requires it, even though I have NO NO NO
        /// intention of deserializing into this class  (just serializing). Make sure to populate fields if you call
        /// this constructor.
        /// </summary>
        public UpdateCollectionItem() : base()
        {
            if (Title is SwitchUpdate)
                update = Title as SwitchUpdate;
        }

        public UpdateCollectionItem(SwitchTitle title, SwitchCollectionState state, bool isFavorite) : base(title, state, isFavorite)
        {
            if (Title is SwitchUpdate)
                update = Title as SwitchUpdate;
        }

        public UpdateCollectionItem(SwitchTitle title) : base(title)
        {
            if (Title is SwitchUpdate)
                update = Title as SwitchUpdate;
        }

        public UpdateCollectionItem(SwitchTitle title, bool isFavorite) : base(title, isFavorite)
        {
            if (Title is SwitchUpdate)
                update = Title as SwitchUpdate;
        }

        public UpdateCollectionItem(SwitchTitle title, SwitchCollectionState state) : base(title, state)
        {

        }

        public override string ToString()
        {
            return update.ToString();   
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is UpdateCollectionItem other))
                return false;

            return Version.Equals(other.Version) && base.Equals(other);
        }

        public override int GetHashCode()
        {
            return (TitleId + Version).GetHashCode();
        }
    }
}
