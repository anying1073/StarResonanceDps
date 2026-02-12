using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public abstract class RealTimeDataSource(
    DataSourceEngine dataSourceEngine,
    IDataStorage dataStorage,
    DataSourceMode mode,
    IDpsDataProcessor processor)
    : IDpsDataSource
{
    protected readonly DataSourceEngine DataSourceEngine = dataSourceEngine;
    protected readonly IDataStorage DataStorage = dataStorage;
    protected readonly object SyncRoot = new();

    protected StatisticDictionary Cache = new();

    protected bool Enable;
    protected RawDict RawCache = new Dictionary<long, PlayerStatistics>();
    protected bool Updating;

    public DataSourceMode Mode { get; } = mode;
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
        lock (SyncRoot)
        {
            Updating = true;
            foreach (var d in Cache.Values)
            {
                d.Clear();
            }

            Updating = false;
        }

        DataSourceEngine.DeliverProcessedData();
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

    public void Refresh()
    {
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
            var processed = processor.PreProcessData(stat, includeNpc);
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
}