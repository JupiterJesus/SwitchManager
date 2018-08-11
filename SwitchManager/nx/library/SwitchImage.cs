using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.collection
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
    }
}
