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

    protected RealTimeDataSource(DataSourceEngine dataSourceEngine,
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
    }

    ~RealTimeDataSource()
    {
        DataStorage.NewSectionCreated -= OnNewSection;
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

        GC.SuppressFinalize(this);
    }
}