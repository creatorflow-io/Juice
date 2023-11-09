using System.Collections;

namespace Juice.Collections
{
    public class OrderedConcurrentBag<T> : IReadOnlyCollection<T>
        where T : class, IEquatable<T>
    {
        public OrderedConcurrentBag(IComparer<T>? comparer = default)
        {
            if (comparer == null && !typeof(T).IsAssignableTo(typeof(IComparable<T>)))
            {
                throw new ArgumentException($"The type '{typeof(T).FullName}' does not implement the '{typeof(IComparable<T>).FullName}' interface, so you must pass a comparer to the constructor.", "comparer");
            }
            _comparer = comparer;
        }
        public OrderedConcurrentBag(IEnumerable<T> values, IComparer<T> comparer) : this(comparer)
        {
            _items = values.ToArray();
        }

        private IComparer<T>? _comparer;
        private T[] _items = Array.Empty<T>();
        private object _lock = new();


        /// <summary>
        /// Add an item to the bag if it does not exist.
        /// <para></para>If the <c>sort</c> parameter is <c>True</c>, the bag will be sorted after adding the item, otherwise it will not be sorted and you maybe sort it latter by the <see cref="Sort"/> method.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="sort"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool TryAdd(T item, bool sort = true)
        {
            if (item == null) { throw new ArgumentNullException("item"); }
            lock (_lock)
            {
                if (_items.Any(x => x.Equals(item)))
                {
                    return false;
                }
                Array.Resize(ref _items, _items.Length + 1);
                _items[^1] = item;
                if (sort)
                {
                    Array.Sort(_items, _comparer);
                }
                return true;
            }
        }

        /// <summary>
        /// Take the first item and remove it from the bag.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TryTake(out T item)
        {
            lock (_lock)
            {
                if (_items.Length > 0)
                {

                    item = _items.First();
                    var copies = new T[_items.Length - 1];
                    Array.Copy(_items, 1, copies, 0, _items.Length - 1);
                    _items = copies;
                    return true;
                }
                else
                {
                    item = default!;
                    return false;
                }
            }
        }

        /// <summary>
        /// Take all items and empty the bag.
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public bool TryTakeAll(out IReadOnlyCollection<T> items)
        {
            lock (_lock)
            {
                if (_items.Length > 0)
                {
                    items = _items;
                    Array.Clear(_items);
                    return true;
                }
                else
                {
                    items = default!;
                    return false;
                }
            }
        }

        /// <summary>
        /// Peek the first item but keep it in the bag.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TryPeek(out T item)
        {
            lock (_lock)
            {
                if (_items.Length > 0)
                {
                    item = _items.First();
                    return true;
                }
                else
                {
                    item = default!;
                    return false;
                }
            }
        }

        /// <summary>
        /// Return true if item exist and was updated, otherwise return false.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public bool TryUpdate(T item)
        {
            if (item == null) { throw new ArgumentNullException("item"); }
            lock (_lock)
            {
                var index = Array.FindIndex(_items, x => x.Equals(item));
                if (index >= 0)
                {
                    _items[index] = item;
                    Array.Sort(_items, _comparer);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Update item if it exists, otherwise add it.
        /// </summary>
        /// <param name="item"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void UpdateOrCreate(T item)
        {
            if (item == null) { throw new ArgumentNullException("item"); }
            lock (_lock)
            {
                var index = Array.FindIndex(_items, x => x.Equals(item));
                if (index >= 0)
                {
                    _items[index] = item;
                }
                else
                {
                    Array.Resize(ref _items, _items.Length + 1);
                    _items[^1] = item;
                }
                Array.Sort(_items, _comparer);
            }
        }

        /// <summary>
        /// Sort the bag use the configured comparer or default.
        /// </summary>
        public void Sort()
        {
            lock (_lock)
            {
                Array.Sort(_items, _comparer);
            }
        }

        /// <summary>
        /// Check if item exists.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            lock (_lock)
            {
                return _items.Any(x => x.Equals(item));
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _items.Length == 0;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _items.Length;
                }
            }
        }

        /// <summary>
        /// Get clone of all items.
        /// </summary>
        /// <returns></returns>
        public T[] ToArray()
        {
            lock (_lock)
            {
                return _items.ToArray();
            }
        }

        public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
    }
}
