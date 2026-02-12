using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

/// <summary>
/// Central engine that unifies data providers for Passive/Active/Snapshot modes
/// and performs the sub-viewmodel update logic extracted from the main ViewModel.
/// </summary>
public partial class DataSourceEngine
{
    private readonly ActiveUpdateModeDpsDataSource _activeUpdateModeDpsDataSource;
    private readonly ILogger<DataSourceEngine> _logger;
    private readonly Dictionary<DataSourceMode, IDpsDataSource> _providers = new();
    private readonly SnapshotDpsDataSource _snapshotDpsDataSource;

    public DataSourceEngine(
        IDataStorage dataStorage,
        IDpsDataProcessor dataProcessor,
        BattleSnapshotService snapShotService,
        ILogger<DataSourceEngine> logger)
    {
        var dataStorage1 = dataStorage ?? throw new ArgumentNullException(nameof(dataStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register providers
        Register(new PassiveUpdateModeDpsDataSource(this, dataStorage1, dataProcessor));
        _activeUpdateModeDpsDataSource = new ActiveUpdateModeDpsDataSource(this, dataStorage1, _logger, dataProcessor);
        Register(_activeUpdateModeDpsDataSource);
        _snapshotDpsDataSource = new SnapshotDpsDataSource(this, snapShotService, logger, dataProcessor);
        Register(_snapshotDpsDataSource);
    }

    public DataSourceMode CurrentMode
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            LogModeChanged(value);
        }
    }

    public IDpsDataSource CurrentSource => _providers.TryGetValue(CurrentMode, out var p) ? p : null!;

    public ScopeTime Scope
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            WhenScopeChanged(field);
        }
    }

    public bool IncludeNpcData { get; set; }

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Switched to {Mode} mode.")]
    private partial void LogModeChanged(DataSourceMode mode);

    public void Configure(DataSourceEngineParam param)
    {
        if (param.Mode != null) ChangeMode(param.Mode.Value);
        if (param.BattleSnapshotFilePath != null)
            _snapshotDpsDataSource.SetSnapshotFilePath(param.BattleSnapshotFilePath);
        if (param.ActiveUpdateInterval != null)
            _activeUpdateModeDpsDataSource.SetUpdateInterval(param.ActiveUpdateInterval.Value);
    }

    // Event raised when providers deliver preprocessed data ready for UI
    public event
        Action<Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>>>? ProcessedDataReady;

    private void Register(IDpsDataSource source)
    {
        _providers[source.Mode] = source;
    }

    public void ChangeMode(DataSourceMode mode)
    {
        CurrentMode = mode;
        foreach (var p in _providers.Values)
        {
            p.SetEnable(p.Mode == mode);
        }
    }

    private void WhenScopeChanged(ScopeTime scope)
    {
        foreach (var provider in _providers.Values)
        {
            provider.Scope = scope;
            provider.Refresh();
        }
    }

    public Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> GetData()
    {
        return CurrentSource.GetData();
    }

    public IReadOnlyDictionary<long, PlayerInfo> GetPlayerInfoDictionary()
    {
        return CurrentSource.GetPlayerInfoDictionary();
    }

    /// <summary>
    /// Deliver already-preprocessed data to the ViewModel for UI update.
    /// Providers should call this when they prepare processed datasets.
    /// </summary>
    internal void DeliverProcessedData()
    {
        var data = CurrentSource.GetData();
        ProcessedDataReady?.Invoke(data);
    }
}