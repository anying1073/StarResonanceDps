using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public abstract class RealTimeDataSource : IDpsDataSource, IDisposable
{
    protected readonly DataSourceEngine DataSourceEngine;
    protected readonly IDataStorage DataStorage;
    protected readonly object SyncRoot = new();

    protected StatisticDictionary Cache = new();

    protected bool Enable;
    protected RawDict RawCache = new Dictionary<long, PlayerStatistics>();
    protected bool Updating;

    private readonly IDpsDataProcessor _processor;
    private readonly IDpsTimerService _timerService;

    // Snapshot of the last cleared CURRENT-section logs only.
    // Much lighter than cloning all PlayerStatistics for all players.
    private List<BattleLog> _lastClearedSectionLogs = new();

    protected RealTimeDataSource(
        DataSourceEngine dataSourceEngine,
        IDataStorage dataStorage,
        DataSourceMode mode,
        IDpsDataProcessor processor,
        IDpsTimerService timerService)
    {
        _processor = processor;
        _timerService = timerService;
        DataSourceEngine = dataSourceEngine;
        DataStorage = dataStorage;
        Mode = mode;

        DataStorage.NewSectionCreated += OnNewSection;
        DataStorage.BeforeSectionCleared += OnBeforeSectionCleared;
    }

    ~RealTimeDataSource()
    {
        DataStorage.NewSectionCreated -= OnNewSection;
        DataStorage.BeforeSectionCleared -= OnBeforeSectionCleared;
    }

    public DataSourceMode Mode { get; }
    public ScopeTime Scope { get; set; } = ScopeTime.Current;

    public virtual void SetEnable(bool enable)
    {
        lock (SyncRoot)
        {
            Enable = enable;
        }
    }

    public void Reset()
    {
        ClearCache();
        DataSourceEngine.DeliverProcessedData();
    }

    protected void ClearCache()
    {
        lock (SyncRoot)
        {
            Updating = true;
            foreach (var d in Cache.Values)
            {
                d.Clear();
            }

            Updating = false;
        }
    }

    public Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> GetData()
    {
        lock (SyncRoot)
        {
            return Cache;
        }
    }

    public RawDict GetRawData()
    {
        // Keep this cheap: no all-player clone
        return RawCache;
    }

    public TimeSpan BattleDuration
    {
        get
        {
            return Scope switch
            {
                ScopeTime.Current => _timerService.SectionDuration,
                ScopeTime.Total => _timerService.TotalDuration,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public void Refresh()
    {
        if (!Enable) return;

        var (newCache, raw) = FetchData();

        lock (SyncRoot)
        {
            Cache = newCache;
            RawCache = raw;
        }

        DataSourceEngine.DeliverProcessedData();
    }

    public IReadOnlyDictionary<long, PlayerInfo> GetPlayerInfoDictionary()
    {
        return DataStorage.ReadOnlyPlayerInfoDatas;
    }

    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid)
    {
        if (uid == 0)
        {
            return Array.Empty<BattleLog>();
        }

        // 1) Live logs first
        var live = DataStorage.GetBattleLogsForPlayer(uid, Scope == ScopeTime.Total);
        if (live.Count > 0)
        {
            return live;
        }

        // 2) If current scope was just cleared, use cached pre-clear section logs
        if (Scope == ScopeTime.Current && _lastClearedSectionLogs.Count > 0)
        {
            return _lastClearedSectionLogs
                .Where(log => log.AttackerUuid == uid || log.TargetUuid == uid)
                .ToList();
        }

        return Array.Empty<BattleLog>();
    }

    protected (StatisticDictionary processed, RawDict raw) FetchData()
    {
        var scope = Scope;
        var includeNpc = DataSourceEngine.IncludeNpcData;

        lock (SyncRoot)
        {
            Updating = true;
        }

        try
        {
            var stat = DataStorage.GetStatistics(scope == ScopeTime.Total);
            var processed = _processor.PreProcessData(stat, includeNpc);

            // No deep clone here: this avoids the huge "all players snapshot" freeze.
            return (processed, stat);
        }
        finally
        {
            lock (SyncRoot)
            {
                Updating = false;
            }
        }
    }

    private void OnBeforeSectionCleared()
    {
        try
        {
            var snapshot = DataStorage.GetBattleLogs(false).ToList();

            // Do not overwrite a valid previous snapshot with an empty one.
            if (snapshot.Count > 0)
            {
                _lastClearedSectionLogs = snapshot;
            }
        }
        catch
        {
            // Keep the old snapshot if capture fails.
        }
    }

    protected void OnNewSection()
    {
        if (!Enable) return;

        lock (SyncRoot)
        {
            Reset();
        }
    }

    public void Dispose()
    {
        DataStorage.NewSectionCreated -= OnNewSection;
        DataStorage.BeforeSectionCleared -= OnBeforeSectionCleared;
        GC.SuppressFinalize(this);
    }
}