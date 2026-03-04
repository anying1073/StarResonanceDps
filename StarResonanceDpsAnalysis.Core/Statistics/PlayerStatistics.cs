using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Holds all statistics for a single player
/// </summary>
[DebuggerDisplay("U:{Uid};A:{AttackDamage.Total};T:{TakenDamage.Total};H:{Healing.Total};N:{IsNpc}")]
public sealed class PlayerStatistics
{
    public long Uid { get; }

    public StatisticValues AttackDamage { get; set; } = new();
    public StatisticValues TakenDamage { get; set; } = new();
    public StatisticValues Healing { get; set; } = new();

    // Runtime sample managers (live path)
    private readonly ITimeSeriesSampleManager _deltaDpsSamples;
    private readonly ITimeSeriesSampleManager _deltaHpsSamples;
    private readonly ITimeSeriesSampleManager _deltaDtpsSamples;

    // Detached replay/history samples (NOT updated every runtime tick)
    [JsonProperty]
    public List<DpsDataPoint> DeltaDpsSamples { get; private set; } = new();

    [JsonProperty]
    public List<DpsDataPoint> DeltaHpsSamples { get; private set; } = new();

    [JsonProperty]
    public List<DpsDataPoint> DeltaDtpsSamples { get; private set; } = new();

    public long? StartTick { get; set; }
    public long LastTick { get; set; }
    public bool IsNpc { get; set; }

    private DeltaTrackingHistory _previousHistory;
    private long _lastRecordedTick;
    private bool _isDeltaTrackingEnabled = true;

    [JsonConstructor]
    public PlayerStatistics(long uid, int? timeSeriesCapacity = 300)
    {
        Uid = uid;
        _deltaDpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
        _deltaHpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
        _deltaDtpsSamples = new TimeSeriesSampleManager(timeSeriesCapacity);
    }

    public PlayerStatistics(long uid, Func<ITimeSeriesSampleManager> sampleManagerFactory)
    {
        Uid = uid;
        _deltaDpsSamples = sampleManagerFactory();
        _deltaHpsSamples = sampleManagerFactory();
        _deltaDtpsSamples = sampleManagerFactory();
    }

    public SkillStatistics GetOrCreateSkill(long skillId)
    {
        return AttackDamage.Skills.GetOrAdd(skillId, static id => new SkillStatistics(id));
    }

    public SkillStatistics GetOrCreateHealingSkill(long skillId)
    {
        return Healing.Skills.GetOrAdd(skillId, static id => new SkillStatistics(id));
    }

    public SkillStatistics GetOrCreateTakenSkill(long skillId)
    {
        return TakenDamage.Skills.GetOrAdd(skillId, static id => new SkillStatistics(id));
    }

    public long ElapsedTicks()
    {
        return LastTick - (StartTick ?? 0);
    }

    public void UpdateDeltaValues()
    {
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
            return;
        }

        var deltas = CalculateDeltas();
        ApplyDeltaValues(deltas, elapsed.Value);
        RecordDeltaSamples(deltas, elapsed.Value);
    }

    public void StopDeltaTracking()
    {
        _isDeltaTrackingEnabled = false;
    }

    public void ResumeDeltaTracking()
    {
        _isDeltaTrackingEnabled = true;
    }

    public void ResetDeltaTracking()
    {
        _previousHistory = default;
        ClearDeltaValues();
        _isDeltaTrackingEnabled = true;
    }

    public IReadOnlyList<DpsDataPoint> GetDeltaDpsSamples()
    {
        var runtime = _deltaDpsSamples.GetSamples();
        return runtime.Count > 0 ? runtime : DeltaDpsSamples;
    }

    public IReadOnlyList<DpsDataPoint> GetDeltaHpsSamples()
    {
        var runtime = _deltaHpsSamples.GetSamples();
        return runtime.Count > 0 ? runtime : DeltaHpsSamples;
    }

    public IReadOnlyList<DpsDataPoint> GetDeltaDtpsSamples()
    {
        var runtime = _deltaDtpsSamples.GetSamples();
        return runtime.Count > 0 ? runtime : DeltaDtpsSamples;
    }

    /// <summary>
    /// Used by replay/history reconstruction only.
    /// Avoids the old expensive "every runtime sample => ToList copy" path.
    /// </summary>
    public void SetProjectedSamples(
        IReadOnlyList<DpsDataPoint> dps,
        IReadOnlyList<DpsDataPoint> hps,
        IReadOnlyList<DpsDataPoint> dtps)
    {
        DeltaDpsSamples = dps?.ToList() ?? new List<DpsDataPoint>();
        DeltaHpsSamples = hps?.ToList() ?? new List<DpsDataPoint>();
        DeltaDtpsSamples = dtps?.ToList() ?? new List<DpsDataPoint>();
    }

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

        var elapsedSeconds = StartTick.HasValue
            ? (LastTick - StartTick.Value) / (double)TimeSpan.TicksPerSecond
            : 1.0;

        if (elapsedSeconds > 0)
        {
            _lastRecordedTick = LastTick;
        }
    }

    private double? CalculateElapsedTime()
    {
        var tickDelta = LastTick - _previousHistory.Tick;
        if (tickDelta <= 0)
        {
            return null;
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
        if (LastTick == _lastRecordedTick)
        {
            return;
        }

        var currentTime = StartTick.HasValue
            ? TimeSpan.FromTicks(LastTick - StartTick.Value)
            : TimeSpan.Zero;

        _deltaDpsSamples.AddSample(currentTime, deltas.Damage / seconds);
        _deltaHpsSamples.AddSample(currentTime, deltas.Healing / seconds);
        _deltaDtpsSamples.AddSample(currentTime, deltas.TakenDamage / seconds);

        _lastRecordedTick = LastTick;

        // IMPORTANT:
        // Keep the old baseline behavior as-is.
        // Do not move _previousHistory forward here.
    }

    private void ClearDeltaValues()
    {
        AttackDamage.DeltaValuePerSecond = 0;
        Healing.DeltaValuePerSecond = 0;
        TakenDamage.DeltaValuePerSecond = 0;
    }

    private struct DeltaTrackingHistory
    {
        public long DamageTotal { get; init; }
        public long HealingTotal { get; init; }
        public long TakenDamageTotal { get; init; }
        public long Tick { get; init; }
    }

    private struct DeltaValues
    {
        public long Damage { get; init; }
        public long Healing { get; init; }
        public long TakenDamage { get; init; }
    }
}