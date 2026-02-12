using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public sealed class SnapshotDpsDataSource(
    DataSourceEngine engine,
    BattleSnapshotService service,
    ILogger logger,
    IDpsDataProcessor processor) : IDpsDataSource
{
    private readonly object _syncRoot = new();
    private StatisticDictionary _cache = new();

    private bool _enable;

    private string _filePath = string.Empty;
    private RawDict _rawDict = new Dictionary<long, PlayerStatistics>();
    private BattleSnapshotData? _snapshotData;

    public DataSourceMode Mode => DataSourceMode.Snapshot;
    public ScopeTime Scope { get; set; } = ScopeTime.Current;

    public void SetEnable(bool enable)
    {
        lock (_syncRoot)
        {
            _enable = enable;
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            foreach (var d in _cache.Values)
            {
                d.Clear();
            }
        }

        engine.DeliverProcessedData();
    }

    public Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> GetData()
    {
        lock (_syncRoot)
        {
            return _cache;
        }
    }

    public RawDict GetRawData()
    {
        lock (_syncRoot)
        {
            return _rawDict;
        }
    }

    public void Refresh()
    {
        lock (_syncRoot)
        {
            if (!_enable) return;
            var (processed, raw) = FetchData();
            _cache = processed;
            _rawDict = raw;
        }

        engine.DeliverProcessedData();
    }

    public IReadOnlyDictionary<long, PlayerInfo> GetPlayerInfoDictionary()
    {
        return _snapshotData?.Players ?? new Dictionary<long, PlayerInfo>();
    }

    public void SetSnapshotFilePath(string filePath)
    {
        lock (_syncRoot)
        {
            _filePath = filePath;
        }

        Refresh();
    }

    private (StatisticDictionary data, RawDict raw) FetchData()
    {
        var includeNpc = engine.IncludeNpcData;
        var snapshot = service.LoadSnapshot(_filePath);
        if (snapshot == null)
        {
            logger.LogWarning("Snapshot file not found");
            return (new StatisticDictionary(), new Dictionary<long, PlayerStatistics>());
        }

        _snapshotData = snapshot;
        var processed = processor.PreProcessData(snapshot.Statistics, includeNpc);
        return (processed, snapshot.Statistics);
    }
}