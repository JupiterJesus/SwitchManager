using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchManager.util
{
    public class ObservableList<T> : INotifyCollectionChanged, INotifyPropertyChanged, IList<T>, ICollection<T>, IEnumerable<T>, IListSource
    {
        #region Private Members

        private List<T> list;

        #endregion

        #region Properties

        #endregion

        #region Properties - ICollection

        public int Count => list.Count;

        public bool IsReadOnly => ((IList<T>)list).IsReadOnly;

        #endregion

        #region Properties - IListSource

        public bool ContainsListCollection => true;

        #endregion

        public T this[int index]
        {
            get => list[index];
            set
            {
                var old = list[index];
                list[index] = value;
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,  list[index], old, index));
            }
        }

        #region Events for collectionchanged and propertychanged

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructors

        public ObservableList()
        {
            this.list = new List<T>(); 
        }

        public ObservableList(IEnumerable<T> list)
        {
            this.list = new List<T>(list);
        }

        #endregion

        #region IList and ICollection implementation

        public void Add(T item)
        {
            list.Add(item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Count"));
        }

        public int IndexOf(T item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            list.Insert(index, item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public void RemoveAt(int index)
        {
            var old = list[index];
            list.RemoveAt(index);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, old, index));
        }

        public void Clear()
        {
            list.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(T item)
        {
            return list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            bool r = list.Remove(item);
            if (r)
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            return r;
        }

        #endregion

        #region IEnumberable implementation

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        #endregion

        #region IListSource implementation

        public IList GetList()
        {
            return list;
        }

        #endregion

        public void Sort()
        {
            if (list != null) list.Sort();
        }
    }
}
