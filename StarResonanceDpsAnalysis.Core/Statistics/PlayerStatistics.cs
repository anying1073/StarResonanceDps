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
    
    // Delta time series data managers for incremental values (ONLY delta tracking)
    private readonly ITimeSeriesSampleManager _deltaDpsSamples;
    private readonly ITimeSeriesSampleManager _deltaHpsSamples;
    private readonly ITimeSeriesSampleManager _deltaDtpsSamples;

    // Timing info
    public long? StartTick { get; set; }
    public long LastTick { get; set; }

    // NPC flag
    public bool IsNpc { get; set; }

    // Previous values for delta calculation
    private DeltaTrackingSnapshot _previousSnapshot;
    
    // Track last recorded tick to prevent duplicate sample recordings
    private long _lastRecordedTick;
    
    // Flag to control delta tracking
    private bool _isDeltaTrackingEnabled = true;

    /// <summary>
    /// Creates a new PlayerStatistics instance with default capacity-based sampling
    /// </summary>
    /// <param name="uid">Player unique identifier</param>
    /// <param name="timeSeriesCapacity">Maximum samples to store. Set to null for unlimited storage.</param>
    public PlayerStatistics(long uid, int? timeSeriesCapacity = 300)
    {
        Uid = uid;
        _deltaDpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
        _deltaHpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
        _deltaDtpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
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
        _deltaDpsSamples = sampleManagerFactory();
        _deltaHpsSamples = sampleManagerFactory();
        _deltaDtpsSamples = sampleManagerFactory();
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
    /// Calculate delta values per second since last update
    /// Should be called periodically (e.g., every second) to update delta metrics
    /// </summary>
    public void UpdateDeltaValues()
    {
        // Skip delta calculation if tracking is disabled
        if (!_isDeltaTrackingEnabled)
        {
            return;
        }
        
        if (IsFirstUpdate())
        {
            InitializeDeltaTracking();
            return;
        }

        var elapsed = CalculateElapsedTime();
        if (!elapsed.HasValue)
        {
            return; // No time elapsed, skip calculation
        }

        var deltas = CalculateDeltas();
        ApplyDeltaValues(deltas, elapsed.Value);
        
        // Record delta values to time series
        RecordDeltaSamples(deltas, elapsed.Value);
    }

    /// <summary>
    /// Stop delta tracking (called when section ends)
    /// Preserves current delta values but stops calculating new ones
    /// </summary>
    public void StopDeltaTracking()
    {
        _isDeltaTrackingEnabled = false;
    }
    
    /// <summary>
    /// Resume delta tracking (called when new section starts)
    /// </summary>
    public void ResumeDeltaTracking()
    {
        _isDeltaTrackingEnabled = true;
    }

    /// <summary>
    /// Reset delta tracking (useful when clearing or resetting statistics)
    /// Also re-enables tracking if it was stopped
    /// </summary>
    public void ResetDeltaTracking()
    {
        _previousSnapshot = default;
        ClearDeltaValues();
        _isDeltaTrackingEnabled = true; // Re-enable tracking on reset
    }

    /// <summary>
    /// Get delta DPS samples as a read-only list (incremental DPS between measurements)
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDeltaDpsSamples()
    {
        return _deltaDpsSamples.GetSamples();
    }

    /// <summary>
    /// Get delta HPS samples as a read-only list (incremental HPS between measurements)
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDeltaHpsSamples()
    {
        return _deltaHpsSamples.GetSamples();
    }

    /// <summary>
    /// Get delta DTPS samples as a read-only list (incremental DTPS between measurements)
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDeltaDtpsSamples()
    {
        return _deltaDtpsSamples.GetSamples();
    }

    /// <summary>
    /// Clear all delta DPS/HPS/DTPS samples
    /// </summary>
    public void ClearSamples()
    {
        _deltaDpsSamples.Clear();
        _deltaHpsSamples.Clear();
        _deltaDtpsSamples.Clear();
        ResetDeltaTracking();
    }

    #region Delta Calculation Helpers

    private bool IsFirstUpdate() => _previousSnapshot.Tick == 0;

    private void InitializeDeltaTracking()
    {
        _previousSnapshot = new DeltaTrackingSnapshot
        {
            DamageTotal = AttackDamage.Total,
            HealingTotal = Healing.Total,
            TakenDamageTotal = TakenDamage.Total,
            Tick = LastTick
        };
        
        // Record initial sample at the first update
        // Calculate time from start
        var currentTime = StartTick.HasValue 
            ? TimeSpan.FromTicks(LastTick - StartTick.Value) 
            : TimeSpan.Zero;
        
        // Calculate elapsed time for DPS calculation
        var elapsedSeconds = StartTick.HasValue 
            ? (LastTick - StartTick.Value) / (double)TimeSpan.TicksPerSecond 
            : 1.0; // Default to 1 second if no start time
        
        if (elapsedSeconds > 0)
        {
            // Record initial values as first delta samples
            _deltaDpsSamples.AddSample(currentTime, AttackDamage.Total / elapsedSeconds);
            _deltaHpsSamples.AddSample(currentTime, Healing.Total / elapsedSeconds);
            _deltaDtpsSamples.AddSample(currentTime, TakenDamage.Total / elapsedSeconds);
            
            // Update last recorded tick to prevent duplicates
            _lastRecordedTick = LastTick;
        }
    }

    private double? CalculateElapsedTime()
    {
        var tickDelta = LastTick - _previousSnapshot.Tick;
        if (tickDelta <= 0)
        {
            return null; // No time elapsed
        }
        return tickDelta / (double)TimeSpan.TicksPerSecond;
    }

    private DeltaValues CalculateDeltas()
    {
        return new DeltaValues
        {
            Damage = AttackDamage.Total - _previousSnapshot.DamageTotal,
            Healing = Healing.Total - _previousSnapshot.HealingTotal,
            TakenDamage = TakenDamage.Total - _previousSnapshot.TakenDamageTotal
        };
    }

    private void ApplyDeltaValues(DeltaValues deltas, double seconds)
    {
        AttackDamage.DeltaValuePerSecond = deltas.Damage / seconds;
        Healing.DeltaValuePerSecond = deltas.Healing / seconds;
        TakenDamage.DeltaValuePerSecond = deltas.TakenDamage / seconds;
    }

    private void RecordDeltaSamples(DeltaValues deltas, double seconds)
    {
        // Skip if we've already recorded samples at this tick
        // This prevents duplicate recordings when UpdateDeltaValues() is called
        // multiple times before LastTick is updated by new battle logs
        if (LastTick == _lastRecordedTick)
        {
            return; // Already recorded samples at this tick
        }
        
        // Calculate time from start (assuming LastTick represents current time)
        var currentTime = StartTick.HasValue 
            ? TimeSpan.FromTicks(LastTick - StartTick.Value) 
            : TimeSpan.Zero;
        
        // Record delta values per second to time series
        _deltaDpsSamples.AddSample(currentTime, deltas.Damage / seconds);
        _deltaHpsSamples.AddSample(currentTime, deltas.Healing / seconds);
        _deltaDtpsSamples.AddSample(currentTime, deltas.TakenDamage / seconds);
        
        // Update last recorded tick to prevent duplicates
        _lastRecordedTick = LastTick;
    }

    private void ClearDeltaValues()
    {
        AttackDamage.DeltaValuePerSecond = 0;
        Healing.DeltaValuePerSecond = 0;
        TakenDamage.DeltaValuePerSecond = 0;
    }

    /// <summary>
    /// Snapshot of previous state for delta calculation
    /// </summary>
    private struct DeltaTrackingSnapshot
    {
        public long DamageTotal { get; init; }
        public long HealingTotal { get; init; }
        public long TakenDamageTotal { get; init; }
        public long Tick { get; init; }
    }

    /// <summary>
    /// Delta values between two snapshots
    /// </summary>
    private struct DeltaValues
    {
        public long Damage { get; init; }
        public long Healing { get; init; }
        public long TakenDamage { get; init; }
    }

    #endregion
}