using System.Diagnostics;
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
    private readonly ICombatSectionStateManager _combatState;
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
    private readonly IDpsUpdateCoordinator _updateCoordinator;
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
    private DispatcherTimer? _dpsUpdateTimer;
    private DispatcherTimer? _durationTimer;
    private int _indicatorHoverCount;
    private bool _isInitialized;
    private DpsDataUpdatedEventHandler? _resumeActiveTimerHandler;
    private bool _wasPassiveMode;
    private bool _wasTimerRunning;

    // ===== Public Properties =====
    public DpsStatisticsSubViewModel CurrentStatisticData => StatisticData[StatisticIndex];
    public DebugFunctions DebugFunctions { get; }
    public DpsStatisticsOptions Options { get; } = new();
    public BattleSnapshotService SnapshotService { get; }
    public Dictionary<StatisticType, DpsStatisticsSubViewModel> StatisticData { get; }

    // ===== Constructor =====
    public DpsStatisticsViewModel(
        ILogger<DpsStatisticsViewModel> logger,
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
        IDpsUpdateCoordinator updateCoordinator,
        ICombatSectionStateManager combatSectionState,
        ITeamStatsUIManager teamStatsManager,
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
        _updateCoordinator = updateCoordinator;
        _combatState = combatSectionState;
        _teamStatsManager = teamStatsManager;
        _resetCoordinator = resetCoordinator;

        StatisticData = new Dictionary<StatisticType, DpsStatisticsSubViewModel>
        {
            [StatisticType.Damage] = new(logger, dispatcher, StatisticType.Damage, storage, debugFunctions, this, localizationManager),
            [StatisticType.Healing] = new(logger, dispatcher, StatisticType.Healing, storage, debugFunctions, this, localizationManager),
            [StatisticType.TakenDamage] = new(logger, dispatcher, StatisticType.TakenDamage, storage, debugFunctions, this, localizationManager),
            [StatisticType.NpcTakenDamage] = new(logger, dispatcher, StatisticType.NpcTakenDamage, storage, debugFunctions, this, localizationManager)
        };

        _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;
        _storage.BeforeSectionCleared += StorageOnBeforeSectionCleared;
        _storage.DpsDataUpdated += UpdateData;
        _storage.NewSectionCreated += StorageOnNewSectionCreated;
        _storage.ServerConnectionStateChanged += StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated += StorageOnPlayerInfoUpdated;
        _storage.ServerChanged += StorageOnServerChanged;
        _storage.SectionEnded += SectionEnded;
        DebugFunctions.SampleDataRequested += OnSampleDataRequested;

        AppConfig = _configManager.CurrentConfig;
        LoadDpsStatisticsSettings();

        _timerService.DurationChanged += OnTimerDurationChanged;
        _updateCoordinator.UpdateRequested += OnUpdateRequested;
        
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
        _timerService.DurationChanged -= OnTimerDurationChanged;
        _updateCoordinator.UpdateRequested -= OnUpdateRequested;
        _timerService.Stop();
        _updateCoordinator.Stop();

        if (_durationTimer != null)
        {
            _durationTimer.Stop();
            _durationTimer.Tick -= DurationTimerOnTick;
        }

        if (_dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Stop();
            _dpsUpdateTimer.Tick -= DpsUpdateTimerOnTick;
            _dpsUpdateTimer = null;
        }

        _storage.DpsDataUpdated -= UpdateData;
        _storage.NewSectionCreated -= StorageOnNewSectionCreated;
        _storage.ServerConnectionStateChanged -= StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated -= StorageOnPlayerInfoUpdated;
        _storage.Dispose();

        foreach (var dpsStatisticsSubViewModel in StatisticData.Values)
        {
            dpsStatisticsSubViewModel.Initialized = false;
        }

        _isInitialized = false;

        if (_resumeActiveTimerHandler != null)
        {
            _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
            _resumeActiveTimerHandler = null;
        }

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

            if (_resumeActiveTimerHandler != null)
            {
                _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
                _resumeActiveTimerHandler = null;
            }

            _updateCoordinator.Stop();
            _storage.DpsDataUpdated -= UpdateData;
            _storage.NewSectionCreated -= StorageOnNewSectionCreated;

            _logger.LogInformation("ResetAll: Stopped all existing update mechanisms (using DpsUpdateCoordinator)");

            if (AppConfig.DpsUpdateMode == DpsUpdateMode.Active)
            {
                _updateCoordinator.Configure(AppConfig.DpsUpdateMode, AppConfig.DpsUpdateInterval);
                _updateCoordinator.Start();
                _logger.LogInformation("ResetAll: Started DpsUpdateCoordinator. Interval={Interval}ms", AppConfig.DpsUpdateInterval);
            }
            else
            {
                _storage.DpsDataUpdated += UpdateData;
                _logger.LogInformation("ResetAll: Subscribed to DpsDataUpdated event");
            }

            _storage.NewSectionCreated += StorageOnNewSectionCreated;

            RefreshData();
            UpdateBattleDuration();

            _logger.LogInformation(
                "=== ResetAll COMPLETE === ScopeTime={ScopeTime}, Mode={Mode}, Coordinator enabled={Enabled}, Event subscribed={Event}",
                ScopeTime, AppConfig.DpsUpdateMode, _updateCoordinator.IsUpdateEnabled, AppConfig.DpsUpdateMode == DpsUpdateMode.Passive);
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
    private void OnTimerDurationChanged(object? sender, TimeSpan duration)
    {
        _dispatcher.Invoke(() =>
        {
            BattleDuration = duration;
        });
    }

    private void OnUpdateRequested(object? sender, EventArgs e)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(OnUpdateRequested, sender, e);
            return;
        }

        UpdateData();
    }

    private void OnSampleDataRequested(object? sender, EventArgs e)
    {
        AddRandomData();
    }
    
    private void OnTeamStatsUpdated(object? sender, TeamStatsUpdatedEventArgs e)
    {
        // Update observable properties when team stats change
        _dispatcher.Invoke(() =>
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

    private void DpsUpdateTimerOnTick(object? sender, EventArgs e)
    {
        // 每10次tick输出一次日志,避免日志爆炸
        if (_dpsUpdateTimer != null && _dpsUpdateTimer.IsEnabled)
        {
            var currentSecond = DateTime.Now.Second;
            if (currentSecond % 10 == 0) // 每10秒输出一次
            {
                _logger.LogDebug("Timer tick triggered - calling DataStorage_DpsDataUpdated");
            }
        }

        // Call the same update logic as event-based mode
        UpdateData();
    }

    private void DurationTimerOnTick(object? sender, EventArgs e)
    {
        UpdateBattleDuration();
    }

    private void EnsureDurationTimerStarted()
    {
        if (_durationTimer != null) return;

        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += DurationTimerOnTick;
        _durationTimer.Start();
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
