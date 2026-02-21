using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.Models;

/// <summary>
///     Thread-safe observable dictionary with support for batch updates via BeginUpdate/EndUpdate.
///     - Uses <see cref="ReaderWriterLockSlim" /> for synchronization.
///     - Suppresses notifications while in a batch and emits a single Reset when the outermost batch ends (if changes
///     occurred).
///     - Raises events on the captured SynchronizationContext (typically the UI thread) when available.
/// </summary>
public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyCollectionChanged,
    INotifyPropertyChanged where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dict;
    private readonly ReaderWriterLockSlim _rw = new(LockRecursionPolicy.NoRecursion);

    // captured sync context for marshaling events (maybe null)
    private readonly SynchronizationContext? _syncContext = SynchronizationContext.Current;

    // batch update state (protected by _rw write lock)
    private int _deferLevel;
    private bool _hasDeferredChanges;

    public ObservableDictionary() : this(null)
    {
    }

    public ObservableDictionary(IEqualityComparer<TKey>? comparer)
    {
        _dict = new Dictionary<TKey, TValue>(comparer);
    }

    public ObservableDictionary(int capacity)
    {
        _dict = new Dictionary<TKey, TValue>(capacity);
    }

    public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> items, IEqualityComparer<TKey>? comparer = null)
    {
        _dict = new Dictionary<TKey, TValue>(comparer);
        foreach (var kv in items) _dict[kv.Key] = kv.Value;
    }

    #region Batch update control

    /// <summary>
    ///     Begin a batch update. Notifications are suppressed until matching EndUpdate.
    ///     Supports nested calls.
    /// </summary>
    public void BeginUpdate()
    {
        _rw.EnterWriteLock();
        try
        {
            _deferLevel++;
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    /// <summary>
    ///     End a batch update. When the outermost EndUpdate is called and changes occurred during the batch,
    ///     a single Reset notification is raised (and relevant property changed notifications).
    /// </summary>
    public void EndUpdate()
    {
        var shouldRaise = false;
        _rw.EnterWriteLock();
        try
        {
            if (_deferLevel <= 0) return;
            _deferLevel--;
            if (_deferLevel == 0 && _hasDeferredChanges)
            {
                _hasDeferredChanges = false;
                shouldRaise = true;
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }

        if (shouldRaise)
        {
            // notify once (Reset) on captured context
            RaisePropertyChanged(nameof(Count));
            RaisePropertyChanged(nameof(Keys));
            RaisePropertyChanged(nameof(Values));
            RaisePropertyChanged("Item[]");
            RaiseCollectionReset();
        }
    }

    private bool InBatch
    {
        get
        {
            _rw.EnterReadLock();
            try
            {
                return _deferLevel > 0;
            }
            finally
            {
                _rw.ExitReadLock();
            }
        }
    }

    private void MarkDeferredChange()
    {
        _rw.EnterWriteLock();
        try
        {
            _hasDeferredChanges = true;
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    #endregion

    #region IDictionary<TKey,TValue>

    public TValue this[TKey key]
    {
        get
        {
            _rw.EnterReadLock();
            try
            {
                return _dict[key];
            }
            finally
            {
                _rw.ExitReadLock();
            }
        }
        set
        {
            KeyValuePair<TKey, TValue> newItem = default!;
            KeyValuePair<TKey, TValue> oldItem = default!;
            var toRaise = NotifyAction.None;

            _rw.EnterWriteLock();
            try
            {
                if (_dict.TryGetValue(key, out var old))
                {
                    if (EqualityComparer<TValue>.Default.Equals(old, value))
                        return;

                    _dict[key] = value;
                    if (_deferLevel > 0)
                    {
                        _hasDeferredChanges = true;
                        return;
                    }

                    newItem = new KeyValuePair<TKey, TValue>(key, value);
                    oldItem = new KeyValuePair<TKey, TValue>(key, old);
                    toRaise = NotifyAction.Replace;
                }
                else
                {
                    _dict[key] = value;
                    if (_deferLevel > 0)
                    {
                        _hasDeferredChanges = true;
                        return;
                    }

                    newItem = new KeyValuePair<TKey, TValue>(key, value);
                    toRaise = NotifyAction.Add;
                }
            }
            finally
            {
                _rw.ExitWriteLock();
            }

            if (toRaise == NotifyAction.Replace)
            {
                RaiseCollectionReplaced(newItem, oldItem);
                RaisePropertyChanged(nameof(Values));
                RaisePropertyChanged("Item[]");
            }
            else if (toRaise == NotifyAction.Add)
            {
                RaiseCollectionAdd(newItem);
                RaisePropertyChanged(nameof(Count));
                RaisePropertyChanged(nameof(Keys));
                RaisePropertyChanged(nameof(Values));
                RaisePropertyChanged("Item[]");
            }
        }
    }

    public ICollection<TKey> Keys
    {
        get
        {
            _rw.EnterReadLock();
            try
            {
                return _dict.Keys.ToList();
            } // History
            finally
            {
                _rw.ExitReadLock();
            }
        }
    }

    public ICollection<TValue> Values
    {
        get
        {
            _rw.EnterReadLock();
            try
            {
                return _dict.Values.ToList();
            } // History
            finally
            {
                _rw.ExitReadLock();
            }
        }
    }

    public int Count
    {
        get
        {
            _rw.EnterReadLock();
            try
            {
                return _dict.Count;
            }
            finally
            {
                _rw.ExitReadLock();
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        _rw.EnterWriteLock();
        try
        {
            _dict.Add(key, value);
            if (_deferLevel > 0)
            {
                _hasDeferredChanges = true;
                return;
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }

        var item = new KeyValuePair<TKey, TValue>(key, value);
        RaiseCollectionAdd(item);
        RaisePropertyChanged(nameof(Count));
        RaisePropertyChanged(nameof(Keys));
        RaisePropertyChanged(nameof(Values));
        RaisePropertyChanged("Item[]");
    }

    public bool ContainsKey(TKey key)
    {
        _rw.EnterReadLock();
        try
        {
            return _dict.ContainsKey(key);
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    public bool Remove(TKey key)
    {
        KeyValuePair<TKey, TValue> old = default;
        var removed = false;
        _rw.EnterWriteLock();
        try
        {
            if (_dict.TryGetValue(key, out var val) && _dict.Remove(key))
            {
                old = new KeyValuePair<TKey, TValue>(key, val);
                removed = true;
                if (_deferLevel > 0)
                {
                    _hasDeferredChanges = true;
                    return true;
                }
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }

        if (removed)
        {
            RaiseCollectionRemove(old);
            RaisePropertyChanged(nameof(Count));
            RaisePropertyChanged(nameof(Keys));
            RaisePropertyChanged(nameof(Values));
            RaisePropertyChanged("Item[]");
            return true;
        }

        return false;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        _rw.EnterReadLock();
        try
        {
            return _dict.TryGetValue(key, out value!);
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    #endregion

    #region ICollection<KeyValuePair<TKey,TValue>>

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    public void Clear()
    {
        var had = false;
        _rw.EnterWriteLock();
        try
        {
            if (_dict.Count == 0) return;
            _dict.Clear();
            had = true;
            if (_deferLevel > 0)
            {
                _hasDeferredChanges = true;
                return;
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }

        if (had)
        {
            RaisePropertyChanged(nameof(Count));
            RaisePropertyChanged(nameof(Keys));
            RaisePropertyChanged(nameof(Values));
            RaisePropertyChanged("Item[]");
            RaiseCollectionReset();
        }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        _rw.EnterReadLock();
        try
        {
            return _dict.TryGetValue(item.Key, out var v) && EqualityComparer<TValue>.Default.Equals(v, item.Value);
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        _rw.EnterReadLock();
        try
        {
            if (array.Length - arrayIndex < _dict.Count)
                throw new ArgumentException("Insufficient space in target array.");
            var i = arrayIndex;
            foreach (var kv in _dict) array[i++] = kv;
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        _rw.EnterWriteLock();
        try
        {
            if (_dict.TryGetValue(item.Key, out var v) && EqualityComparer<TValue>.Default.Equals(v, item.Value))
            {
                _dict.Remove(item.Key);
                if (_deferLevel > 0)
                {
                    _hasDeferredChanges = true;
                    return true;
                }
            }
            else
            {
                return false;
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }

        RaiseCollectionRemove(item);
        RaisePropertyChanged(nameof(Count));
        RaisePropertyChanged(nameof(Keys));
        RaisePropertyChanged(nameof(Values));
        RaisePropertyChanged("Item[]");
        return true;
    }

    #endregion

    #region Enumeration

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        _rw.EnterReadLock();
        try
        {
            // return a History enumerator to avoid locking during enumeration
            var History = _dict.ToList();
            return History.GetEnumerator();
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion

    #region Events & helpers

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private enum NotifyAction
    {
        None,
        Add,
        Remove,
        Replace,
        Reset
    }

    private void RaiseOnContext(Action invoke)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => invoke(), null);
        }
        else
        {
            invoke();
        }
    }

    private void RaiseCollectionAdd(KeyValuePair<TKey, TValue> item)
    {
        var handler = CollectionChanged;
        if (handler == null) return;
        RaiseOnContext(() =>
            handler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item)));
    }

    private void RaiseCollectionRemove(object item)
    {
        var handler = CollectionChanged;
        if (handler == null) return;
        RaiseOnContext(() =>
            handler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item)));
    }

    private void RaiseCollectionReplaced(object newItem, object oldItem)
    {
        var handler = CollectionChanged;
        if (handler == null) return;
        RaiseOnContext(() => handler(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem)));
    }

    private void RaiseCollectionReset()
    {
        var handler = CollectionChanged;
        if (handler == null) return;
        RaiseOnContext(() => handler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
    }

    private void RaisePropertyChanged(string name)
    {
        var handler = PropertyChanged;
        if (handler == null) return;
        RaiseOnContext(() => handler(this, new PropertyChangedEventArgs(name)));
    }

    #endregion
}