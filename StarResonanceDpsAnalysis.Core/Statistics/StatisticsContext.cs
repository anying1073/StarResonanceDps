using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Context object containing all data needed for statistics calculation
/// Thread-safe implementation
/// </summary>
public sealed class StatisticsContext
{
    private readonly Dictionary<long, PlayerStatistics> _fullStats = new();
    private readonly Dictionary<long, PlayerStatistics> _sectionStats = new();
    private readonly List<BattleLog> _fullBattleLogs = new();
    private readonly List<BattleLog> _sectionBattleLogs = new();
    
    // ? Time series sample capacity configuration
    private readonly int _timeSeriesSampleCapacity;
    
    // Locks for thread safety
    private readonly object _statsLock = new();
    private readonly object _logsLock = new();
    
    /// <summary>
    /// Constructor with configurable time series capacity
    /// </summary>
    /// <param name="timeSeriesSampleCapacity">Maximum samples to store for time series data. If null, uses global configuration.</param>
    public StatisticsContext(int? timeSeriesSampleCapacity = null)
    {
        _timeSeriesSampleCapacity = timeSeriesSampleCapacity ?? StatisticsConfiguration.TimeSeriesSampleCapacity;
    }
    
    /// <summary>
    /// Get or create full-session statistics for a player
    /// </summary>
    public PlayerStatistics GetOrCreateFullStats(long uid)
    {
        lock (_statsLock)
        {
            if (!_fullStats.TryGetValue(uid, out var stats))
            {
                stats = new PlayerStatistics(uid, _timeSeriesSampleCapacity);
                _fullStats[uid] = stats;
            }
            return stats;
        }
    }
    
    /// <summary>
    /// Get or create section statistics for a player
    /// </summary>
    public PlayerStatistics GetOrCreateSectionStats(long uid)
    {
        lock (_statsLock)
        {
            if (!_sectionStats.TryGetValue(uid, out var stats))
            {
                stats = new PlayerStatistics(uid, _timeSeriesSampleCapacity);
                _sectionStats[uid] = stats;
            }
            return stats;
        }
    }
    
    /// <summary>
    /// Add battle log to both full and section collections
    /// </summary>
    public void AddBattleLog(BattleLog log)
    {
        lock (_logsLock)
        {
            _fullBattleLogs.Add(log);
            _sectionBattleLogs.Add(log);
        }
    }
    
    /// <summary>
    /// Get all full battle logs (returns snapshot)
    /// </summary>
    public IReadOnlyList<BattleLog> FullBattleLogs
    {
        get
        {
            lock (_logsLock)
            {
                return _fullBattleLogs.ToList();
            }
        }
    }
    
    /// <summary>
    /// Get all section battle logs (returns snapshot)
    /// </summary>
    public IReadOnlyList<BattleLog> SectionBattleLogs
    {
        get
        {
            lock (_logsLock)
            {
                return _sectionBattleLogs.ToList();
            }
        }
    }
    
    /// <summary>
    /// Clear section statistics and battle logs
    /// </summary>
    public void ClearSection()
    {
        lock (_statsLock)
        {
            _sectionStats.Clear();
        }
        
        lock (_logsLock)
        {
            _sectionBattleLogs.Clear();
        }
    }
    
    /// <summary>
    /// Clear all statistics and battle logs (both full and section)
    /// </summary>
    public void ClearAll()
    {
        lock (_statsLock)
        {
            _fullStats.Clear();
            _sectionStats.Clear();
        }
        
        lock (_logsLock)
        {
            _fullBattleLogs.Clear();
            _sectionBattleLogs.Clear();
        }
    }
    
    /// <summary>
    /// Get all full statistics (returns snapshot)
    /// </summary>
    public IReadOnlyDictionary<long, PlayerStatistics> FullStatistics
    {
        get
        {
            lock (_statsLock)
            {
                return new Dictionary<long, PlayerStatistics>(_fullStats);
            }
        }
    }
    
    /// <summary>
    /// Get all section statistics (returns snapshot)
    /// </summary>
    public IReadOnlyDictionary<long, PlayerStatistics> SectionStatistics
    {
        get
        {
            lock (_statsLock)
            {
                return new Dictionary<long, PlayerStatistics>(_sectionStats);
            }
        }
    }
}
