using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Holds all statistics for a single player
/// Following SRP: Only responsible for holding player statistics data
/// </summary>
[DebuggerDisplay("U:{Uid};A:{AttackDamage.Total};T:{TakenDamage.Total};H:{Healing.Total};N:{IsNpc}")]
public sealed class PlayerStatistics
{
    public long Uid { get; }

    // Statistics by type
    public StatisticValues AttackDamage { get; set; } = new();
    public StatisticValues TakenDamage { get; set; } = new();
    public StatisticValues Healing { get; set; } = new();

    // ===== ORIGINAL INTERNAL SAMPLE MANAGERS (KEEP) =====
    private readonly ITimeSeriesSampleManager _deltaDpsSamples;
    private readonly ITimeSeriesSampleManager _deltaHpsSamples;
    private readonly ITimeSeriesSampleManager _deltaDtpsSamples;

    /// <summary>
    /// Serializable snapshot mirror of DPS samples.
    /// Used for history persistence / detached snapshot display.
    /// Does NOT replace the original runtime calculation source.
    /// </summary>
    [JsonProperty]
    public List<DpsDataPoint> DeltaDpsSamples { get; private set; } = new();

    /// <summary>
    /// Serializable snapshot mirror of HPS samples.
    /// </summary>
    [JsonProperty]
    public List<DpsDataPoint> DeltaHpsSamples { get; private set; } = new();

    /// <summary>
    /// Serializable snapshot mirror of DTPS samples.
    /// </summary>
    [JsonProperty]
    public List<DpsDataPoint> DeltaDtpsSamples { get; private set; } = new();

    // Timing info
    public long? StartTick { get; set; }
    public long LastTick { get; set; }

    // NPC flag
    public bool IsNpc { get; set; }

    // Previous values for delta calculation
    private DeltaTrackingHistory _previousHistory;

    // Track last recorded tick to prevent duplicate sample recordings
    private long _lastRecordedTick;

    // Flag to control delta tracking
    private bool _isDeltaTrackingEnabled = true;

    /// <summary>
    /// Creates a new PlayerStatistics instance with default capacity-based sampling
    /// </summary>
    /// <param name="uid">Player unique identifier</param>
    /// <param name="timeSeriesCapacity">Maximum samples to store. Set to null for unlimited storage.</param>
    [JsonConstructor]
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
        return LastTick - (StartTick ?? 0);
    }

    /// <summary>
    /// Calculate delta values per second since last update
    /// Should be called periodically (e.g., every second) to update delta metrics
    ///
    /// IMPORTANT:
    /// This logic is intentionally kept EXACTLY aligned with the user's original implementation.
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
    ///
    /// IMPORTANT:
    /// Kept aligned with the original implementation:
    /// _lastRecordedTick is NOT reset here.
    /// </summary>
    public void ResetDeltaTracking()
    {
        _previousHistory = default;
        ClearDeltaValues();
        _isDeltaTrackingEnabled = true;
    }

    /// <summary>
    /// Get delta DPS samples as a read-only list.
    /// Prefer runtime internal manager when it has data (live mode, original source).
    /// Otherwise fall back to the serialized mirror (history / detached snapshot).
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDeltaDpsSamples()
    {
        var runtime = _deltaDpsSamples.GetSamples();
        if (runtime.Count > 0)
        {
            return runtime;
        }

        return DeltaDpsSamples;
    }

    /// <summary>
    /// Get delta HPS samples as a read-only list.
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDeltaHpsSamples()
    {
        var runtime = _deltaHpsSamples.GetSamples();
        if (runtime.Count > 0)
        {
            return runtime;
        }

        return DeltaHpsSamples;
    }

    /// <summary>
    /// Get delta DTPS samples as a read-only list.
    /// </summary>
    public IReadOnlyList<DpsDataPoint> GetDeltaDtpsSamples()
    {
        var runtime = _deltaDtpsSamples.GetSamples();
        if (runtime.Count > 0)
        {
            return runtime;
        }

        return DeltaDtpsSamples;
    }

    /// <summary>
    /// Clear all delta DPS/HPS/DTPS samples
    /// </summary>
    public void ClearSamples()
    {
        _deltaDpsSamples.Clear();
        _deltaHpsSamples.Clear();
        _deltaDtpsSamples.Clear();

        DeltaDpsSamples.Clear();
        DeltaHpsSamples.Clear();
        DeltaDtpsSamples.Clear();

        ResetDeltaTracking();
    }

    #region Delta Calculation Helpers

    private bool IsFirstUpdate() => _previousHistory.Tick == 0;

    private void InitializeDeltaTracking()
    {
        _previousHistory = new DeltaTrackingHistory
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
            // ORIGINAL BEHAVIOR:
            // no initial point is added here, only initialize duplicate guard
            _lastRecordedTick = LastTick;
        }
    }

    private double? CalculateElapsedTime()
    {
        var tickDelta = LastTick - _previousHistory.Tick;
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
            Damage = AttackDamage.Total - _previousHistory.DamageTotal,
            Healing = Healing.Total - _previousHistory.HealingTotal,
            TakenDamage = TakenDamage.Total - _previousHistory.TakenDamageTotal
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
        // ORIGINAL BEHAVIOR:
        // avoid duplicate recording at the same LastTick
        if (LastTick == _lastRecordedTick)
        {
            return;
        }

        // Calculate time from start (assuming LastTick represents current time)
        var currentTime = StartTick.HasValue
            ? TimeSpan.FromTicks(LastTick - StartTick.Value)
            : TimeSpan.Zero;

        // ORIGINAL RUNTIME SAMPLE CALCULATION
        _deltaDpsSamples.AddSample(currentTime, deltas.Damage / seconds);
        _deltaHpsSamples.AddSample(currentTime, deltas.Healing / seconds);
        _deltaDtpsSamples.AddSample(currentTime, deltas.TakenDamage / seconds);

        // Mirror runtime samples into serializable lists for history/snapshot persistence
        SyncSerializableMirrorsFromRuntime();

        // Update last recorded tick to prevent duplicates
        _lastRecordedTick = LastTick;

        // IMPORTANT:
        // DO NOT move _previousHistory forward here.
        // This is intentionally kept identical to the original implementation.
    }

    private void SyncSerializableMirrorsFromRuntime()
    {
        DeltaDpsSamples = _deltaDpsSamples.GetSamples().ToList();
        DeltaHpsSamples = _deltaHpsSamples.GetSamples().ToList();
        DeltaDtpsSamples = _deltaDtpsSamples.GetSamples().ToList();
    }

    private void ClearDeltaValues()
    {
        AttackDamage.DeltaValuePerSecond = 0;
        Healing.DeltaValuePerSecond = 0;
        TakenDamage.DeltaValuePerSecond = 0;
    }

    /// <summary>
    /// History of previous state for delta calculation
    /// </summary>
    private struct DeltaTrackingHistory
    {
        public long DamageTotal { get; init; }
        public long HealingTotal { get; init; }
        public long TakenDamageTotal { get; init; }
        public long Tick { get; init; }
    }

    /// <summary>
    /// Delta values between two histories
    /// </summary>
    private struct DeltaValues
    {
        public long Damage { get; init; }
        public long Healing { get; init; }
        public long TakenDamage { get; init; }
    }

    #endregion
}