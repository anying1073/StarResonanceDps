using System.Collections.Concurrent;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Holds all statistics for a single player
/// Following SRP: Only responsible for holding player statistics data
/// </summary>
public sealed class PlayerStatistics
{
    public long Uid { get; }

    // Statistics by type
    public StatisticValues AttackDamage { get; } = new();
    public StatisticValues TakenDamage { get; } = new();
    public StatisticValues Healing { get; } = new();

    // Time series data managers (Dependency Inversion: depend on abstraction)
    private readonly ITimeSeriesSampleManager _dpsSamples;
    private readonly ITimeSeriesSampleManager _hpsSamples;
    private readonly ITimeSeriesSampleManager _dtpsSamples;

    // Timing info
    public long? StartTick { get; set; }
    public long LastTick { get; set; }

    // NPC flag
    public bool IsNpc { get; set; }

    /// <summary>
    /// Creates a new PlayerStatistics instance with default capacity-based sampling
    /// </summary>
    /// <param name="uid">Player unique identifier</param>
    /// <param name="timeSeriesCapacity">Maximum samples to store. Set to null for unlimited storage.</param>
    public PlayerStatistics(long uid, int? timeSeriesCapacity = 300)
    {
        Uid = uid;
        _dpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
        _hpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
        _dtpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
    }

    /// <summary>
    /// Creates a new PlayerStatistics instance with custom sample managers
    /// Use this for adaptive sampling or time-window retention
    /// </summary>
    /// <param name="uid">Player unique identifier</param>
    /// <param name="sampleManagerFactory">Factory to create sample managers</param>
    public PlayerStatistics(long uid, Func<ITimeSeriesSampleManager> sampleManagerFactory)
    {
        Uid = uid;
        _dpsSamples = sampleManagerFactory();
        _hpsSamples = sampleManagerFactory();
        _dtpsSamples = sampleManagerFactory();
    }

    /// <summary>
    /// Get or create skill statistics (for damage skills)
    /// </summary>
    public SkillStatistics GetOrCreateSkill(long skillId)
    {
        return AttackDamage.Skills.GetOrAdd(skillId, static id => new SkillStatistics(id));
    }
    
    /// <summary>
    /// Get or create healing skill statistics
    /// </summary>
    public SkillStatistics GetOrCreateHealingSkill(long skillId)
    {
        return Healing.Skills.GetOrAdd(skillId, static id => new SkillStatistics(id));
    }
    
    /// <summary>
    /// Get or create taken damage skill statistics
    /// </summary>
    public SkillStatistics GetOrCreateTakenSkill(long skillId)
    {
        return TakenDamage.Skills.GetOrAdd(skillId, static id => new SkillStatistics(id));
    }

    public long ElapsedTicks()
    {
        return LastTick - StartTick ?? 0;
    }

    /// <summary>
    /// Add a DPS data point for time series tracking
    /// </summary>
    public void AddDpsSample(TimeSpan time, double dps)
    {
        _dpsSamples.AddSample(time, dps);
    }

    /// <summary>
    /// Add an HPS data point for time series tracking
    /// </summary>
    public void AddHpsSample(TimeSpan time, double hps)
    {
        _hpsSamples.AddSample(time, hps);
    }

    /// <summary>
    /// Add a DTPS (Damage Taken Per Second) data point for time series tracking
    /// </summary>
    public void AddDtpsSample(TimeSpan time, double dtps)
    {
        _dtpsSamples.AddSample(time, dtps);
    }

    /// <summary>
    /// Get DPS samples as a read-only list
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDpsSamples()
    {
        return _dpsSamples.GetSamples();
    }

    /// <summary>
    /// Get HPS samples as a read-only list
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetHpsSamples()
    {
        return _hpsSamples.GetSamples();
    }

    /// <summary>
    /// Get DTPS samples as a read-only list
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDtpsSamples()
    {
        return _dtpsSamples.GetSamples();
    }

    /// <summary>
    /// Clear all DPS/HPS/DTPS samples
    /// </summary>
    public void ClearSamples()
    {
        _dpsSamples.Clear();
        _hpsSamples.Clear();
        _dtpsSamples.Clear();
    }
}

/// <summary>
/// Interface for managing time series samples
/// ISP: Interface Segregation - small, focused interface
/// </summary>
public interface ITimeSeriesSampleManager
{
    void AddSample(TimeSpan time, double value);
    IReadOnlyList<DpsDataPoint> GetSamples();
    void Clear();
}

/// <summary>
/// Manages time series samples with automatic capacity management
/// SRP: Single Responsibility - only manages sample collection
/// OCP: Open/Closed - can be extended without modification
/// </summary>
public sealed class TimeSeriesSampleManager : ITimeSeriesSampleManager
{
    private readonly ConcurrentQueue<DpsDataPoint> _samples = new();
    private readonly int? _maxCapacity; // Nullable to support unlimited storage
    private int _count;

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
        
        // Only trim if capacity limit is set
        if (_maxCapacity.HasValue)
        {
            TrimToCapacity();
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

    private void TrimToCapacity()
    {
        if (!_maxCapacity.HasValue) return;
        
        while (_count > _maxCapacity.Value)
        {
            if (_samples.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _count);
            }
            else
            {
                break;
            }
        }
    }
}

/// <summary>
/// Statistics values for a specific metric (damage, healing, etc.)
/// </summary>
public sealed class StatisticValues
{
    public long Total { get; set; }
    public int HitCount { get; set; }
    public int CritCount { get; set; }
    public int LuckyCount { get; set; }
    public long NormalValue { get; set; }
    public long CritValue { get; set; }
    public long LuckyValue { get; set; }
    public double ValuePerSecond { get; set; }
    public ConcurrentDictionary<long, SkillStatistics> Skills { get; } = new();
}

/// <summary>
/// Statistics for a specific skill
/// </summary>
public sealed class SkillStatistics(long skillId)
{
    public long SkillId { get; } = skillId;
    public long TotalValue { get; set; }
    public int UseTimes { get; set; }
    public int CritTimes { get; set; }
    public int LuckyTimes { get; set; }
}

/// <summary>
/// Represents a single DPS/HPS/DTPS data point in time series
/// Immutable value object following DDD principles
/// </summary>
public readonly record struct DpsDataPoint(TimeSpan Time, double Value);