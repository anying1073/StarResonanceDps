using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace StarResonanceDpsAnalysis.Core.Collections;

/// <summary>
/// Thread-safe bounded cache with LRU-like eviction policy
/// Automatically evicts oldest entries when capacity is exceeded
/// </summary>
public sealed class BoundedConcurrentCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheEntry> _cache;
    private readonly ConcurrentDictionary<TKey, DateTime> _timestamps;
    private readonly int _maxCapacity;
    private readonly TimeSpan _entryLifetime;
    private readonly object _evictionLock = new();
    private DateTime _lastEviction = DateTime.MinValue;
    private readonly TimeSpan _evictionInterval = TimeSpan.FromSeconds(5);

    private struct CacheEntry
    {
        public TValue Value;
        public DateTime AccessTime;
    }

    public BoundedConcurrentCache(int maxCapacity, TimeSpan? entryLifetime = null)
    {
        if (maxCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Must be positive");

        _maxCapacity = maxCapacity;
        _entryLifetime = entryLifetime ?? TimeSpan.FromSeconds(30);
        _cache = new ConcurrentDictionary<TKey, CacheEntry>();
        _timestamps = new ConcurrentDictionary<TKey, DateTime>();
    }

    /// <summary>
    /// Add or update an entry in the cache
    /// </summary>
    public bool TryAdd(TKey key, TValue value)
    {
        var now = DateTime.UtcNow;
        var entry = new CacheEntry { Value = value, AccessTime = now };

        var added = _cache.TryAdd(key, entry);
        if (added)
        {
            _timestamps[key] = now;
            CheckCapacityAndEvict(now);
        }

        return added;
    }

    /// <summary>
    /// Try to get a value from the cache
    /// </summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            // Update access time (best effort, don't fail if concurrent update happens)
            var now = DateTime.UtcNow;
            _cache.TryUpdate(key, new CacheEntry { Value = entry.Value, AccessTime = now }, entry);
            
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Remove an entry from the cache
    /// </summary>
    public bool TryRemove(TKey key, out TValue value)
    {
        if (_cache.TryRemove(key, out var entry))
        {
            _timestamps.TryRemove(key, out _);
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Check if capacity is exceeded and evict old entries if needed
    /// </summary>
    private void CheckCapacityAndEvict(DateTime now)
    {
        // Rate-limit eviction checks to avoid overhead
        if ((now - _lastEviction) < _evictionInterval)
            return;

        // Only one thread should perform eviction at a time
        if (!Monitor.TryEnter(_evictionLock))
            return;

        try
        {
            _lastEviction = now;

            // First, remove expired entries
            var expiredKeys = _timestamps
                .Where(kvp => (now - kvp.Value) > _entryLifetime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
                _timestamps.TryRemove(key, out _);
            }

            // If still over capacity, remove oldest entries
            if (_cache.Count > _maxCapacity)
            {
                var toRemove = _cache.Count - _maxCapacity + (_maxCapacity / 10); // Remove 10% extra
                var oldestKeys = _timestamps
                    .OrderBy(kvp => kvp.Value)
                    .Take(toRemove)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestKeys)
                {
                    _cache.TryRemove(key, out _);
                    _timestamps.TryRemove(key, out _);
                }

                Debug.WriteLine($"[BoundedCache] Evicted {oldestKeys.Count} entries. Current size: {_cache.Count}");
            }
        }
        finally
        {
            Monitor.Exit(_evictionLock);
        }
    }

    /// <summary>
    /// Clear all entries
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _timestamps.Clear();
    }

    /// <summary>
    /// Get current cache size
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Force eviction of old entries
    /// </summary>
    public void ForceEviction()
    {
        lock (_evictionLock)
        {
            CheckCapacityAndEvict(DateTime.UtcNow);
        }
    }
}
