using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.library
{
    public class SwitchImage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string location;
        public string Location
        {
            get { return this.location; }
            set
            {
                this.location = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Location"));
            }
        }

        public SwitchImage(string location)
        {
            this.Location = location;
        }

        public override string ToString()
        {
            return Location;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (!(obj is SwitchImage))
                return false;

            SwitchImage other = obj as SwitchImage;
            if (Location == null && other.Location == null)
                return true;

            if (Location == null || other.Location == null)
                return false;

            return Location.Equals(other.Location);
        }

        public override int GetHashCode()
        {
            return Location?.GetHashCode() ?? 0;
        }
    }
}
