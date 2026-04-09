using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.Core.Statistics.Calculators;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

/// <summary>
/// Dummy train mode data source that calculates damage to training dummies based on battle logs.
/// Listen to battle log creation and section end events to update the damage data accordingly.
/// Time limit: n min(current 3 min). When new Section is created, the data will be reset. When battle log is created, if the target is a dummy, the damage will be calculated and updated until timed up and then all data should be frozen until resetting.
/// </summary>
public class DummyTrainingModeDataSource(
    DataSourceEngine dataSourceEngine,
    IDataStorage dataStorage,
    ILogger logger)
    : IDpsDataSource
{
    private static readonly Dictionary<DummyTargetType, HashSet<long>> DummyTargetDictionary = new()
    {
        { DummyTargetType.Center, [75] },
        { DummyTargetType.TDummy, [179] },
        { DummyTargetType.AoeDummy, [70, 71, 72, 73, 76] }
    };

    private readonly StatisticDictionary _cache = new();
    private readonly StatisticsContext _context = new();
    private readonly TrainingDamageCalculator _damageCalculator = new();
    private readonly Dictionary<long, PlayerStatistics> _rawCache = new();
    private readonly object _syncRoot = new();
    private bool _enable;
    private bool _isTimedOut;
    private DateTime? _startedAt;

    public bool Enable
    {
        get => _enable;
        set => SetEnable(value);
    }

    public DummyTargetType DummyTarget
    {
        get;
        set
        {
            field = value;
            _damageCalculator.TargetDummyUids = GetDummyUidHashSetByTarget(value);
        }
    } = DummyTargetType.Center;

    public long PlayerUid { get; set; } = -1;
    public TimeSpan TimeLimit { get; set; } = TimeSpan.FromMinutes(3);
    public TimeSpan BattleDuration => GetElapsedTime();

    public DataSourceMode Mode => DataSourceMode.DummyTraining;
    public ScopeTime Scope { get; set; } = ScopeTime.Current;

    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid)
    {
        return dataStorage.GetBattleLogsForPlayer(uid, Scope == ScopeTime.Total);
    }

    public Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> GetData()
    {
        lock (_syncRoot)
        {
            return _cache;
        }
    }

    public IReadOnlyDictionary<long, PlayerInfo> GetPlayerInfoDictionary()
    {
        return dataStorage.ReadOnlyPlayerInfoDatas;
    }

    public RawDict GetRawData()
    {
        lock (_syncRoot)
        {
            return _rawCache;
        }
    }

    public void Refresh()
    {
        if (!_enable) return;
        dataSourceEngine.DeliverProcessedData();
    }

    public void Reset()
    {
        ClearCache();
        dataSourceEngine.DeliverProcessedData();
    }

    public void SetEnable(bool enable)
    {
        lock (_syncRoot)
        {
            if (_enable == enable) return;
            _enable = enable;
            if (enable)
            {
                dataStorage.BattleLogCreated += OnBattleLogCreated;
                dataStorage.NewSectionCreated += DataStorageOnNewSectionCreated;
            }
            else
            {
                dataStorage.BattleLogCreated -= OnBattleLogCreated;
                dataStorage.NewSectionCreated -= DataStorageOnNewSectionCreated;
            }

            if (enable)
            {
                _damageCalculator.TargetDummyUids = GetDummyUidHashSetByTarget(DummyTarget);
            }
        }
    }

    private void Calculate(BattleLog log)
    {
        var damage = (ulong)Math.Max(0, log.Value);
        if (damage == 0) return;

        lock (_syncRoot)
        {
            // check player uid
            if (PlayerUid != -1 && log.AttackerUuid != PlayerUid) return;
            _damageCalculator.Calculate(log, _context);
            // this data source only support section scope
            var stat = _context.GetOrCreateSectionStats(log.AttackerUuid);
            var ticks = stat.ElapsedTicks();
            _rawCache[log.AttackerUuid] = stat;

            _cache[StatisticType.Damage][log.AttackerUuid] = new DpsDataProcessed(
                stat,
                stat.AttackDamage.Total.ConvertToUnsigned(),
                ticks,
                log.AttackerUuid, stat.AttackDamage.ValuePerSecond);
        }
    }

    private void ClearCache()
    {
        lock (_syncRoot)
        {
            foreach (var d in _cache.Values)
            {
                d.Clear();
            }

            _rawCache.Clear();
            _context.ClearAll();
            _startedAt = null;
            _isTimedOut = false;
        }
    }

    private TimeSpan GetElapsedTime()
    {
        if (_startedAt != null)
        {
            return DateTime.Now - (DateTime)_startedAt;
        }

        return TimeSpan.Zero;
    }

    private void DataStorageOnNewSectionCreated()
    {
        if (!_enable) return;
        Reset();
    }

    private void OnBattleLogCreated(BattleLog log)
    {
        if (!_enable) return;
        if (_isTimedOut) return;
        if (log.IsHeal || log.IsTargetPlayer) return;
        if (!ShouldCountNpcDamage(log.TargetUuid)) return;

        _startedAt ??= DateTime.Now;
        if (DateTime.Now - _startedAt >= TimeLimit)
        {
            _isTimedOut = true;
            logger.LogInformation("Dummy training mode timed out after {Minutes} minutes", TimeLimit.TotalMinutes);
            return;
        }

        Calculate(log);
        Refresh();
    }

    private bool ShouldCountNpcDamage(long targetNpcId)
    {
        var set = GetDummyUidHashSetByTarget(DummyTarget);
        return set.Contains(targetNpcId);
    }

    private static HashSet<long> GetDummyUidHashSetByTarget(DummyTargetType target)
    {
        return DummyTargetDictionary.TryGetValue(target, out var set)
            ? set
            : [];
    }
}