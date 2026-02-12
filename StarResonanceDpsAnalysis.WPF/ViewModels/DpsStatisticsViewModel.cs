using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Main ViewModel for DPS Statistics View
/// This is the core file containing field definitions, constructor, and essential methods
/// Business logic is distributed across partial class files:
/// - DpsStatisticsViewModel.Commands.cs: UI command methods
/// - DpsStatisticsViewModel.Snapshot.cs: Snapshot viewing functionality
/// - DpsStatisticsViewModel.StorageHandlers.cs: Data storage event handlers
/// - DpsStatisticsViewModel.DataProcessing.cs: Data update and processing
/// - DpsStatisticsViewModel.Configuration.cs: Configuration and settings
/// - DpsStatisticsViewModel.Definitions.cs: Type definitions and records
/// </summary>
public partial class DpsStatisticsViewModel : BaseViewModel, IDisposable
{
    // ===== Services =====
    private readonly IApplicationControlService _appControlService;
    private readonly IConfigManager _configManager;
    private readonly IDpsDataProcessor _dataProcessor;
    private readonly Dispatcher _dispatcher;
    private readonly LocalizationManager _localizationManager;
    private readonly ILogger<DpsStatisticsViewModel> _logger;
    private readonly IMessageDialogService _messageDialogService;
    private readonly IResetCoordinator _resetCoordinator;
    private readonly IDataStorage _storage;
    private readonly ITeamStatsUIManager _teamStatsManager;
    private readonly IDpsTimerService _timerService;
    private readonly IWindowManagementService _windowManagement;

    // ===== Observable Properties =====
    [ObservableProperty] private AppConfig _appConfig = new();
    [ObservableProperty] private TimeSpan _battleDuration;
    [ObservableProperty] private BattleSnapshotData? _currentSnapshot;
    [ObservableProperty] private int _debugUpdateCount;
    [ObservableProperty] private bool _isIncludeNpcData;
    [ObservableProperty] private bool _isServerConnected;
    [ObservableProperty] private bool _isViewingSnapshot;
    [ObservableProperty] private ScopeTime _scopeTime = ScopeTime.Current;
    [ObservableProperty] private bool _showContextMenu;
    [ObservableProperty] private bool _showTeamTotalDamage;
    [ObservableProperty] private SortDirectionEnum _sortDirection = SortDirectionEnum.Descending;
    [ObservableProperty] private string _sortMemberPath = "Value";
    [ObservableProperty] private StatisticType _statisticIndex;
    [ObservableProperty] private ulong _teamTotalDamage;
    [ObservableProperty] private double _teamTotalDps;
    [ObservableProperty] private string _teamTotalLabel = string.Empty;
    [ObservableProperty] private bool _temporaryMaskPlayerName;

    // ===== Private State Fields =====
    private int _indicatorHoverCount;
    private bool _isInitialized;

    // ===== Public Properties =====
    public DpsStatisticsSubViewModel CurrentStatisticData => StatisticData[StatisticIndex];
    public DebugFunctions DebugFunctions { get; }
    public DpsStatisticsOptions Options { get; } = new();
    public BattleSnapshotService SnapshotService { get; }
    public Dictionary<StatisticType, DpsStatisticsSubViewModel> StatisticData { get; }

    // Engine instance (initialized in constructor)
    private readonly DataSourceEngine _dataSourceEngine;

    // ===== Constructor =====

    public DpsStatisticsViewModel(ILogger<DpsStatisticsViewModel> logger,
        IDataStorage storage,
        IConfigManager configManager,
        IWindowManagementService windowManagement,
        IApplicationControlService appControlService,
        Dispatcher dispatcher,
        DebugFunctions debugFunctions,
        BattleSnapshotService snapshotService,
        LocalizationManager localizationManager,
        IMessageDialogService messageDialogService,
        IDpsTimerService timerService,
        IDpsDataProcessor dataProcessor,
        ITeamStatsUIManager teamStatsManager,
        DataSourceEngine dataSourceEngine,
        IResetCoordinator resetCoordinator)
    {
        _logger = logger;
        _storage = storage;
        _configManager = configManager;
        _windowManagement = windowManagement;
        _appControlService = appControlService;
        _dispatcher = dispatcher;
        _localizationManager = localizationManager;
        _messageDialogService = messageDialogService;
        DebugFunctions = debugFunctions;
        SnapshotService = snapshotService;
        _timerService = timerService;
        _dataProcessor = dataProcessor;
        _teamStatsManager = teamStatsManager;
        _resetCoordinator = resetCoordinator;

        // Subscribe to engine processed data ready event
        _dataSourceEngine = dataSourceEngine;
        _dataSourceEngine.ProcessedDataReady += DataSourceEngineOnProcessedDataReady;
        void DataSourceEngineOnProcessedDataReady(Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> processed)
        {
            InvokeOnDispatcher(() => ApplyProcessedData(processed));
        }


        // Configure engine mode according to config
        _dataSourceEngine.ChangeMode(_configManager.CurrentConfig.DpsUpdateMode.ToDataSourceMode());

        StatisticData = new Dictionary<StatisticType, DpsStatisticsSubViewModel>
        {
            [StatisticType.Damage] = new(logger, dispatcher, StatisticType.Damage, debugFunctions, this, localizationManager, _dataSourceEngine),
            [StatisticType.Healing] = new(logger, dispatcher, StatisticType.Healing, debugFunctions, this, localizationManager, _dataSourceEngine),
            [StatisticType.TakenDamage] = new(logger, dispatcher, StatisticType.TakenDamage, debugFunctions, this, localizationManager, _dataSourceEngine),
            [StatisticType.NpcTakenDamage] = new(logger, dispatcher, StatisticType.NpcTakenDamage, debugFunctions, this, localizationManager, _dataSourceEngine)
        };


        _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;

        _storage.BeforeSectionCleared += StorageOnBeforeSectionCleared;
        _storage.ServerConnectionStateChanged += StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated += StorageOnPlayerInfoUpdated;
        _storage.ServerChanged += StorageOnServerChanged;
        _storage.SectionEnded += SectionEnded;
        _storage.NewSectionCreated += StorageOnNewSectionCreated;
        DebugFunctions.SampleDataRequested += OnSampleDataRequested;

        AppConfig = _configManager.CurrentConfig;
        LoadDpsStatisticsSettings();

        // Bind team stats manager to show team total setting
        _teamStatsManager.ShowTeamTotal = ShowTeamTotalDamage;
        _teamStatsManager.TeamStatsUpdated += OnTeamStatsUpdated;
        TeamTotalLabel = GetTeamTotalLabel(StatisticType.Damage);

        _logger.LogDebug("DpsStatisticsViewModel constructor completed");
    }

    // ===== Dispose =====
    public void Dispose()
    {
        DebugFunctions.SampleDataRequested -= OnSampleDataRequested;
        _configManager.ConfigurationUpdated -= ConfigManagerOnConfigurationUpdated;
        _timerService.Stop();

        _storage.ServerConnectionStateChanged -= StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated -= StorageOnPlayerInfoUpdated;
        _storage.Dispose();

        foreach (var dpsStatisticsSubViewModel in StatisticData.Values)
        {
            dpsStatisticsSubViewModel.Initialized = false;
        }

        _isInitialized = false;

        _storage.BeforeSectionCleared -= StorageOnBeforeSectionCleared;
    }

    // ===== Core Public Methods =====
    [RelayCommand]
    public void ResetAll()
    {
        _logger.LogInformation("=== ResetAll START === ScopeTime={ScopeTime}", ScopeTime);

        if (_timerService.IsRunning)
        {
            _timerService.Stop();
            _logger.LogInformation("已停止战斗计时器 (using DpsTimerService)");
        }

        // Use ResetCoordinator to handle snapshot save + reset
        _resetCoordinator.ResetWithSnapshot(
            ScopeTime,
            saveSnapshot: true,
            BattleDuration,
            Options.MinimalDurationInSeconds);

        // Clear UI data
        foreach (var subVm in StatisticData.Values)
        {
            subVm.Reset();
        }

        TeamTotalDamage = 0;
        TeamTotalDps = 0;
        BattleDuration = TimeSpan.Zero;

        if (!_isInitialized)
        {
            _logger.LogWarning("ResetAll called but ViewModel not initialized!");
            return;
        }

        try
        {
            _logger.LogInformation("ResetAll: Mode={Mode}, Interval={Interval}ms",
                AppConfig.DpsUpdateMode, AppConfig.DpsUpdateInterval);

            _logger.LogInformation("ResetAll: Stopped all existing update mechanisms (using DpsUpdateCoordinator)");

            UpdateBattleDuration();

            _logger.LogInformation(
                "=== ResetAll COMPLETE === ScopeTime={ScopeTime}, Mode={Mode}, Event subscribed={Event}",
                ScopeTime, AppConfig.DpsUpdateMode, AppConfig.DpsUpdateMode == DpsUpdateMode.Passive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ResetAll");
        }
    }

    [RelayCommand]
    public void ResetSection()
    {
        _logger.LogInformation("=== ResetSection START ===");

        // Delegate to ResetCoordinator
        _resetCoordinator.ResetCurrentSection();

        _logger.LogInformation("=== ResetSection COMPLETE ===");
    }

    public void SetIndicatorHover(bool isHovering)
    {
        _indicatorHoverCount = Math.Max(0, _indicatorHoverCount + (isHovering ? 1 : -1));
        var suppress = _indicatorHoverCount > 0;

        foreach (var vm in StatisticData.Values)
        {
            vm.SuppressSorting = suppress;
        }

        if (!suppress)
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SortSlotsInPlace(true);
            }
        }
    }

    // ===== Private Helper Methods =====

    private void OnSampleDataRequested(object? sender, EventArgs e)
    {
        AddRandomData();
    }

    private void OnTeamStatsUpdated(object? sender, TeamStatsUpdatedEventArgs e)
    {
        // Update observable properties when team stats change
        InvokeOnDispatcher(() =>
        {
            TeamTotalDamage = e.TotalDamage;
            TeamTotalDps = e.TotalDps;
            TeamTotalLabel = GetTeamTotalLabel(e.StatisticType);
        });
    }

    private string GetTeamTotalLabel(StatisticType statisticType)
    {
        return statisticType switch
        {
            StatisticType.Damage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamTotal_Damage,
                defaultValue: "Team DPS"),
            StatisticType.Healing => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamTotal_Healing,
                defaultValue: "Team Healing"),
            StatisticType.TakenDamage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamTotal_TakenDamage,
                defaultValue: "Team Damage Taken"),
            StatisticType.NpcTakenDamage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamTotal_NpcTakenDamage,
                defaultValue: "NPC Damage Taken"),
            _ => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamTotal_Damage,
                defaultValue: "Team DPS")
        };
    }

    private void InvokeOnDispatcher(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }
}
