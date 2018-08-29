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

namespace SwitchManager.nx.library
{
    public class ObservableList<T> : INotifyCollectionChanged, IList<T>, ICollection<T>, IEnumerable<T>, IListSource
    {
        private List<T> list;

        public int Count => list.Count;

        public bool IsReadOnly => ((IList<T>)list).IsReadOnly;

        public bool ContainsListCollection => throw new NotImplementedException();

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
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObservableList()
        {
            this.list = new List<T>(); 
        }

        public ObservableList(IEnumerable<T> list)
        {
            this.list = new List<T>(list);
        }

        public void Add(T item)
        {
            list.Add(item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
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

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public IList GetList()
        {
            return list;
        }
    }
}
