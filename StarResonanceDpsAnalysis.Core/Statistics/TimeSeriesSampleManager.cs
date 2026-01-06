using System.Collections.Concurrent;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Manages time series samples with automatic capacity management
/// SRP: Single Responsibility - only manages sample collection
/// OCP: Open/Closed - can be extended without modification
/// Optimized with copy-on-write caching to reduce allocations
/// </summary>
public sealed class TimeSeriesSampleManager : ITimeSeriesSampleManager
{
    private readonly ConcurrentQueue<DpsDataPoint> _samples = new();
    private readonly int? _maxCapacity; // Nullable to support unlimited storage
    private int _count;
    
    private volatile int _version;

    /// <summary>
    /// Creates a time series sample manager
    /// </summary>
    /// <param name="maxCapacity">Maximum capacity. Set to null for unlimited storage.</param>
    public TimeSeriesSampleManager(int? maxCapacity = 300)
    {
        if (maxCapacity.HasValue && maxCapacity.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Capacity must be positive or null for unlimited");
        
        _maxCapacity = maxCapacity;
    }

    public void AddSample(TimeSpan time, double value)
    {
        _samples.Enqueue(new DpsDataPoint(time, value));
        Interlocked.Increment(ref _count);
        Interlocked.Increment(ref _version); // Invalidate cache
        
        // Only trim if capacity limit is set
        if (_maxCapacity.HasValue)
        {
            TrimToCapacity();
        }
    }

    public IReadOnlyList<DpsDataPoint> GetSamples()
    {
        var array = _samples.ToArray();
        return array;
    }

    public void Clear()
    {
        _samples.Clear();
        Interlocked.Exchange(ref _count, 0);
        Interlocked.Increment(ref _version); // Invalidate cache
    }

    private void TrimToCapacity()
    {
        if (!_maxCapacity.HasValue) return;
        
        while (_count > _maxCapacity.Value)
        {
            if (_samples.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
                Interlocked.Increment(ref _version); // Invalidate cache on trim
            }
            else
            {
                break;
            }
        }
    }
}