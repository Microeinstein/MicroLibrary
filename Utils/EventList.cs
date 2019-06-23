using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Micro.Utils {
    /// <summary>
    /// A list with events. Warning: an object of this class can be locked and so could throw exceptions everywhere.
    /// </summary>
    public class EventList<T> : IList<T>, ICollection<T>, IEnumerable<T>, IReadOnlyList<T>, IReadOnlyCollection<T>, IDisposable {
        public delegate void ItemEventHandler(T item, int index);
        public delegate void ItemSetEventHandler(T oldItem, T newItem, int index);
        public delegate void CollectionEventHandler(IEnumerable<T> items, IEnumerable<int> indexes);
        public event ItemEventHandler ItemAdd, ItemRemove, ItemMove;
        public event ItemSetEventHandler ItemSet;
        public event CollectionEventHandler CollectionAdd, CollectionRemove;
        public event Action ListClearing, ListCleared, ListReversing, ListReversed, ListSorting, ListSorted, ListChanged;
        public int Count => l.Count;
        public bool IsReadOnly => ((IList<T>)l).IsReadOnly;
        public bool IsLocked
            => lockKey != null;
        object lockKey;
        List<T> l;

        public T this[int index] {
            get => this[index, null];
            set => this[index, null] = value;
        }
        public T this[int index, object key] {
            get => l[index];
            set {
                checkLock(key);
                var old = l[index];
                if (!value.Equals(old)) {
                    l[index] = value;
                    ListChanged?.Invoke();
                    ItemSet?.Invoke(old, value, index);
                }
            }
        }

        public EventList() {
            l = new List<T>();
        }
        public EventList(List<T> list) {
            l = list;
        }
        public void ClearEvents() {
            ListChanged = null;
            ItemAdd = null;
            ItemSet = null;
            ItemRemove = null;
            ItemMove = null;
            CollectionAdd = null;
            CollectionRemove = null;
            ListClearing = null;
            ListCleared = null;
            ListReversing = null;
            ListReversed = null;
            ListSorting = null;
            ListSorted = null;
        }
        public void Dispose() {
            ClearEvents();
            Clear();
        }

        public void Add(T item, object key) {
            checkLock(key);
            var i = Count;
            l.Add(item);
            ItemAdd?.Invoke(item, i);
            ListChanged?.Invoke();
        }
        public void AddRange(IEnumerable<T> collection, object key = null) {
            checkLock(key);
            var i = Count;
            l.AddRange(collection);
            CollectionAdd?.Invoke(collection, Enumerable.Range(i, collection.Count()));
            ListChanged?.Invoke();
        }
        public void Insert(int index, T item, object key) {
            checkLock(key);
            var i = Count;
            l.Insert(index, item);
            ItemAdd?.Invoke(item, i);
            ListChanged?.Invoke();
        }
        public void InsertRange(int index, IEnumerable<T> collection, object key = null) {
            checkLock(key);
            l.InsertRange(index, collection);
            CollectionAdd?.Invoke(collection, Enumerable.Range(index, collection.Count()));
            ListChanged?.Invoke();
        }
        public bool Remove(T item, object key) {
            var i = IndexOf(item);
            if (i == -1)
                return false;
            RemoveAt(i, key);
            return true;
        }
        public int RemoveAll(Predicate<T> match, object key = null) {
            checkLock(key);
            var e = l.FindAll(match);
            if (e.Count > 0) {
                int j = -1;
                var indexes =
                    from a in e
                    select j = l.IndexOf(a, j + 1);
                l.RemoveAll(match);
                CollectionRemove?.Invoke(e, indexes);
                ListChanged?.Invoke();
            }
            return e.Count;
        }
        public void RemoveAt(int index, object key) {
            checkLock(key);
            var e = l[index];
            l.RemoveAt(index);
            ItemRemove?.Invoke(e, index);
            ListChanged?.Invoke();
        }
        public void RemoveRange(int index, int count, object key = null) {
            checkLock(key);
            var c = l.GetRange(index, count);
            l.RemoveRange(index, count);
            CollectionRemove?.Invoke(c, Enumerable.Range(index, count));
            ListChanged?.Invoke();
        }
        public void Clear(object key) {
            checkLock(key);
            ListClearing?.Invoke();
            l.Clear();
            ListCleared?.Invoke();
            ListChanged?.Invoke();
        }
        public void Reverse(object key = null) {
            checkLock(key);
            ListReversing?.Invoke();
            l.Reverse();
            ListReversed?.Invoke();
            ListChanged?.Invoke();
        }
        public void Reverse(int index, int count, object key = null) {
            checkLock(key);
            ListReversing?.Invoke();
            l.Reverse(index, count);
            ListReversed?.Invoke();
            ListChanged?.Invoke();
        }
        public void Sort(object key = null) {
            checkLock(key);
            ListSorting?.Invoke();
            l.Sort();
            ListSorted?.Invoke();
            ListChanged?.Invoke();
        }
        public void Sort(Comparison<T> comparison, object key = null) {
            checkLock(key);
            ListSorting?.Invoke();
            l.Sort(comparison);
            ListSorted?.Invoke();
            ListChanged?.Invoke();
        }
        public void Sort(IComparer<T> comparer, object key = null) {
            checkLock(key);
            ListSorting?.Invoke();
            l.Sort(comparer);
            ListSorted?.Invoke();
            ListChanged?.Invoke();
        }
        public void Sort(int index, int count, IComparer<T> comparer, object key = null) {
            checkLock(key);
            ListSorting?.Invoke();
            l.Sort(index, count, comparer);
            ListSorted?.Invoke();
            ListChanged?.Invoke();
        }
        public void Move(int fromIndex, int toIndex, object key = null) {
            checkLock(key);
            var e = l[fromIndex];
            l.RemoveAt(fromIndex);
            l.Insert(toIndex, e);
            ItemMove?.Invoke(e, toIndex);
            ListChanged?.Invoke();
        }
        public void Lock(object key) {
            if (IsLocked)
                throw new InvalidOperationException("This object is already locked.");
            lockKey = key ?? throw new ArgumentNullException(nameof(key));
        }
        public void Unlock(object key) {
            if (!IsLocked)
                throw new InvalidOperationException("This object is not locked.");
            checkLock(key);
            lockKey = null;
        }
        void checkLock(object key) {
            if (IsLocked && lockKey != key)
                throw new InvalidOperationException("Invalid key to use this object.");
        }

        public void Add(T item)
            => Add(item, null);
        public void Insert(int index, T item)
            => Insert(index, item, null);
        public bool Remove(T item)
            => Remove(item, null);
        public void RemoveAt(int index)
            => RemoveAt(index, null);
        public void Clear()
            => Clear(null);
        public void CopyTo(T[] array, int arrayIndex)
            => l.CopyTo(array, arrayIndex);
        public bool Contains(T item)
            => l.Contains(item);
        public int IndexOf(T item)
            => l.IndexOf(item);
        public IEnumerator<T> GetEnumerator()
            => l.AsReadOnly().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
            => l.AsReadOnly().GetEnumerator();

        public static implicit operator EventList<T>(List<T> a)
            => new EventList<T>(a);
    }
}