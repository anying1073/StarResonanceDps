using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public sealed class HistoryDpsDataSource(
    DataSourceEngine engine,
    BattleHistoryService service,
    ILogger logger,
    IDpsDataProcessor processor) : IDpsDataSource
{
    private readonly object _syncRoot = new();
    private StatisticDictionary _cache = new();

    private bool _enable;
    private string _filePath = string.Empty;
    private RawDict _rawDict = new Dictionary<long, PlayerStatistics>();
    private BattleHistoryData? _historyData;

    public DataSourceMode Mode => DataSourceMode.History;
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

    public TimeSpan BattleDuration => _historyData?.Duration ?? TimeSpan.Zero;

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
        return _historyData?.Players ?? new Dictionary<long, PlayerInfo>();
    }

    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid)
    {
        if (uid == 0 || _historyData == null || _historyData.BattleLogs.Count == 0)
        {
            return Array.Empty<BattleLog>();
        }

        return _historyData.BattleLogs
            .Where(log => log.AttackerUuid == uid || log.TargetUuid == uid)
            .ToList();
    }

    public void SetHistoryFilePath(string filePath)
    {
        lock (_syncRoot)
        {
            _filePath = filePath;
        }
    }

    private (StatisticDictionary data, RawDict raw) FetchData()
    {
        var includeNpc = engine.IncludeNpcData;
        var history = service.LoadHistory(_filePath);

        logger.LogInformation("Load history: {filePath}", _filePath);

        if (history == null)
        {
            logger.LogWarning("History file not found");
            return (new StatisticDictionary(), new Dictionary<long, PlayerStatistics>());
        }

        _historyData = history;

        var processed = processor.PreProcessData(history.Statistics, includeNpc);
        return (processed, history.Statistics);
    }
}