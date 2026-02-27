namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Interface for recording DPS/HPS/DTPS samples
/// ISP: Interface Segregation - focused interface for sample recording
/// </summary>
public interface ISampleRecorder
{
    void Add(PlayerStatistics stat);

    void Start(
        Func<IReadOnlyDictionary<long, PlayerStatistics>> sectionStatisticsProvider,
        Func<IReadOnlyDictionary<long, PlayerStatistics>> totalStatisticsProvider,
        TimeSpan interval);

    void Stop();
}

/// <summary>
/// Records samples periodically for all players
/// SRP: Single Responsibility - only handles periodic sample recording
/// Note: Delta values are automatically recorded in UpdateDeltaValues(), 
/// so this recorder only needs to trigger the update.
/// </summary>
public sealed class PeriodicSampleRecorder : ISampleRecorder
{
    private readonly object _syncRoot = new();
    private Timer? _timer;
    private Func<IReadOnlyDictionary<long, PlayerStatistics>>? _sectionProvider;
    private Func<IReadOnlyDictionary<long, PlayerStatistics>>? _totalProvider;
    private TimeSpan _interval = TimeSpan.FromSeconds(1);
    private int _isRecording;

    public PeriodicSampleRecorder(int intervalMilliseconds = 1000)
    {
        _interval = TimeSpan.FromMilliseconds(Math.Max(1, intervalMilliseconds));
    }

    public void Add(PlayerStatistics stat)
    {
        _ = stat;
    }

    public void Start(
        Func<IReadOnlyDictionary<long, PlayerStatistics>> sectionStatisticsProvider,
        Func<IReadOnlyDictionary<long, PlayerStatistics>> totalStatisticsProvider,
        TimeSpan interval)
    {
        if (sectionStatisticsProvider == null) throw new ArgumentNullException(nameof(sectionStatisticsProvider));
        if (totalStatisticsProvider == null) throw new ArgumentNullException(nameof(totalStatisticsProvider));

        lock (_syncRoot)
        {
            _sectionProvider = sectionStatisticsProvider;
            _totalProvider = totalStatisticsProvider;
            _interval = interval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : interval;
            _timer ??= new Timer(RecordSamples, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(_interval, _interval);
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void RecordSamples(object? state)
    {
        if (Interlocked.Exchange(ref _isRecording, 1) == 1)
        {
            return;
        }

        try
        {
            var sectionStats = _sectionProvider?.Invoke();
            if (sectionStats != null)
            {
                foreach (var playerStats in sectionStats.Values)
                {
                    playerStats.UpdateDeltaValues();
                }
            }

            var totalStats = _totalProvider?.Invoke();
            if (totalStats != null)
            {
                foreach (var playerStats in totalStats.Values)
                {
                    playerStats.UpdateDeltaValues();
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isRecording, 0);
        }
    }
}
