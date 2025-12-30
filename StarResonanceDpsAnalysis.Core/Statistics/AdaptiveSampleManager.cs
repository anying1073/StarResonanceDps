using System.Collections.Concurrent;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Adaptive time series manager that automatically downsamples old data
/// Keeps high resolution for recent data, lower resolution for historical data
/// </summary>
public sealed class AdaptiveSampleManager : ITimeSeriesSampleManager
{
    private readonly ConcurrentQueue<DpsDataPoint> _highResBuffer = new(); // Last 5 minutes: every sample
    private readonly ConcurrentQueue<DpsDataPoint> _mediumResBuffer = new(); // 5-30 minutes: every 5th sample
    private readonly ConcurrentQueue<DpsDataPoint> _lowResBuffer = new(); // 30+ minutes: every 30th sample
    
    private readonly TimeSpan _highResWindow = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _mediumResWindow = TimeSpan.FromMinutes(30);
    
    private int _sampleCounter = 0;
    private TimeSpan _lastHighResTime = TimeSpan.Zero;
    private TimeSpan _lastMediumResTime = TimeSpan.Zero;

    public void AddSample(TimeSpan time, double value)
    {
        var sample = new DpsDataPoint(time, value);
        _sampleCounter++;
        
        // Always add to high-res buffer
        _highResBuffer.Enqueue(sample);
        
        // Downsample for medium-res (every 5th sample)
        if (_sampleCounter % 5 == 0)
        {
            _mediumResBuffer.Enqueue(sample);
        }
        
        // Downsample for low-res (every 30th sample)
        if (_sampleCounter % 30 == 0)
        {
            _lowResBuffer.Enqueue(sample);
        }
        
        // Age out old samples
        PromoteAndCleanup(time);
    }

    public IReadOnlyList<DpsDataPoint> GetSamples()
    {
        // Combine all buffers (low-res oldest, high-res newest)
        var result = new List<DpsDataPoint>(
            _lowResBuffer.Count + _mediumResBuffer.Count + _highResBuffer.Count);
        
        result.AddRange(_lowResBuffer);
        result.AddRange(_mediumResBuffer);
        result.AddRange(_highResBuffer);
        
        return result;
    }

    public void Clear()
    {
        _highResBuffer.Clear();
        _mediumResBuffer.Clear();
        _lowResBuffer.Clear();
        _sampleCounter = 0;
    }

    private void PromoteAndCleanup(TimeSpan currentTime)
    {
        // Move samples from high-res to medium-res if older than 5 minutes
        var highResCutoff = currentTime - _highResWindow;
        while (_highResBuffer.TryPeek(out var oldest) && oldest.Time < highResCutoff)
        {
            _highResBuffer.TryDequeue(out _);
        }
        
        // Move samples from medium-res to low-res if older than 30 minutes
        var mediumResCutoff = currentTime - _mediumResWindow;
        while (_mediumResBuffer.TryPeek(out var oldest) && oldest.Time < mediumResCutoff)
        {
            _mediumResBuffer.TryDequeue(out _);
        }
        
        // Keep only last 3 hours in low-res buffer (360 samples @ 30-second intervals)
        while (_lowResBuffer.Count > 360)
        {
            _lowResBuffer.TryDequeue(out _);
        }
    }
}

/// <summary>
/// Memory-efficient time series manager with time-based retention
/// </summary>
public sealed class TimeWindowSampleManager : ITimeSeriesSampleManager
{
    private readonly ConcurrentQueue<DpsDataPoint> _samples = new();
    private readonly TimeSpan _retentionWindow;
    private int _count;

    public TimeWindowSampleManager(TimeSpan retentionWindow)
    {
        _retentionWindow = retentionWindow;
    }

    public void AddSample(TimeSpan time, double value)
    {
        _samples.Enqueue(new DpsDataPoint(time, value));
        Interlocked.Increment(ref _count);
        
        // Remove samples older than retention window
        var cutoffTime = time - _retentionWindow;
        while (_samples.TryPeek(out var oldest) && oldest.Time < cutoffTime)
        {
            if (_samples.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }
    }

    public IReadOnlyList<DpsDataPoint> GetSamples()
    {
        return _samples.ToArray();
    }

    public void Clear()
    {
        _samples.Clear();
        Interlocked.Exchange(ref _count, 0);
    }
}
