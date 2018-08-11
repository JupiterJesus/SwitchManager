using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchManager.nx.library
{
    public class SwitchTitleCollection : List<SwitchCollectionItem>
    {
        private SynchronizationContext _synchronizationContext = SynchronizationContext.Current;

        public SwitchTitleCollection()
        {
        }

        public SwitchTitleCollection(IEnumerable<SwitchCollectionItem> list)
            : base(list)
        {
        }
    }
}
