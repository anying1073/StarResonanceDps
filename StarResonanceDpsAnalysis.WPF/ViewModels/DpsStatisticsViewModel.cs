using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;
using System.Globalization;
using System.IO;
using System.Windows.Threading;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Main ViewModel for DPS Statistics View
/// This is the core file containing field definitions, constructor, and essential methods
/// Business logic is distributed across partial class files:
/// - DpsStatisticsViewModel.Commands.cs: UI command methods
/// - DpsStatisticsViewModel.History.cs: History viewing functionality
/// - DpsStatisticsViewModel.StorageHandlers.cs: Data storage event handlers
/// - DpsStatisticsViewModel.DataProcessing.cs: Data update and processing
/// - DpsStatisticsViewModel.Configuration.cs: Configuration and settings
/// - DpsStatisticsViewModel.Definitions.cs: Type definitions and records
/// </summary>
public partial class DpsStatisticsViewModel : BaseDispatcherSupportViewModel, IDisposable
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
    private readonly JsonLocalizationProvider _jsonLocalizationProvider;

    // ===== Observable Properties =====
    [ObservableProperty] private AppConfig _appConfig = new();
    [ObservableProperty] private TimeSpan _battleDuration;
    [ObservableProperty] private int _debugUpdateCount;
    [ObservableProperty] private bool _isIncludeNpcData;
    [ObservableProperty] private bool _isServerConnected;
    [ObservableProperty] private bool _isViewingHistory;
    [ObservableProperty] private ScopeTime _scopeTime = ScopeTime.Current;
    [ObservableProperty] private bool _showContextMenu;
    [ObservableProperty] private bool _showTeamTotalDamage;
    [ObservableProperty] private SortDirectionEnum _sortDirection = SortDirectionEnum.Descending;
    [ObservableProperty] private string _sortMemberPath = "Value";
    [ObservableProperty] private StatisticType _statisticIndex = StatisticType.Damage;
    [ObservableProperty] private ulong _teamTotalDamage;
    [ObservableProperty] private double _teamTotalDps;
    [ObservableProperty] private string _teamLabel = string.Empty;
    [ObservableProperty] private string _currentPlayerLabel = string.Empty;
    [ObservableProperty] private string _teamTotalLabel = string.Empty;

    // ===== Private State Fields =====
    private int _indicatorHoverCount;
    private bool _isInitialized;
    private readonly DispatcherTimer _battleDurationUpdateTimer;

    private bool _maskWarningReentry;

    // ===== Public Properties =====
    public DpsStatisticsSubViewModel CurrentStatisticData => StatisticData[StatisticIndex];
    public DebugFunctions DebugFunctions { get; }
    public DpsStatisticsOptions Options { get; } = new();
    public BattleHistoryService HistoryService { get; }
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
        BattleHistoryService historyService,
        LocalizationManager localizationManager,
        IMessageDialogService messageDialogService,
        IDpsTimerService timerService,
        IDpsDataProcessor dataProcessor,
        ITeamStatsUIManager teamStatsManager,
        DataSourceEngine dataSourceEngine,
        IResetCoordinator resetCoordinator) : base(dispatcher)
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
        HistoryService = historyService;
        _timerService = timerService;
        _dataProcessor = dataProcessor;
        _teamStatsManager = teamStatsManager;
        _resetCoordinator = resetCoordinator;

        _jsonLocalizationProvider = new JsonLocalizationProvider(
            Path.Combine(AppContext.BaseDirectory, "Localization"));

        _battleDurationUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _battleDurationUpdateTimer.Tick += (_, _) => { UpdateBattleDuration(); };

        StatisticData = new Dictionary<StatisticType, DpsStatisticsSubViewModel>
        {
            [StatisticType.Damage] = new(logger, dispatcher, StatisticType.Damage, debugFunctions, this, localizationManager, dataSourceEngine),
            [StatisticType.Healing] = new(logger, dispatcher, StatisticType.Healing, debugFunctions, this, localizationManager, dataSourceEngine),
            [StatisticType.TakenDamage] = new(logger, dispatcher, StatisticType.TakenDamage, debugFunctions, this, localizationManager, dataSourceEngine),
            [StatisticType.NpcTakenDamage] = new(logger, dispatcher, StatisticType.NpcTakenDamage, debugFunctions, this, localizationManager, dataSourceEngine)
        };

        // Subscribe to engine processed data ready event
        _dataSourceEngine = dataSourceEngine;
        _dataSourceEngine.ProcessedDataReady += ApplyProcessedData;

        // Configure engine mode according to config
        _dataSourceEngine.ChangeMode(_configManager.CurrentConfig.DpsUpdateMode.ToDataSourceMode());

        _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;

        _storage.BeforeSectionCleared += StorageOnBeforeSectionCleared;
        _storage.ServerConnectionStateChanged += StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated += StorageOnPlayerInfoUpdatedWithNpcLocalization;
        _storage.ServerChanged += StorageOnServerChanged;
        _storage.SectionEnded += SectionEnded;
        _storage.NewSectionCreated += StorageOnNewSectionCreated;
        DebugFunctions.SampleDataRequested += OnSampleDataRequested;

        AppConfig = _configManager.CurrentConfig;
        LoadDpsStatisticsSettings();

        // Bind team stats manager to show team total setting
        _teamStatsManager.ShowTeamTotal = ShowTeamTotalDamage;
        _teamStatsManager.TeamStatsUpdated += OnTeamStatsUpdated;

        _localizationManager.CultureChanged += OnLocalizationCultureChanged;

        TeamLabel = GetTeamLabel(StatisticIndex);
        CurrentPlayerLabel = GetCurrentPlayerLabel(StatisticIndex);
        TeamTotalLabel = GetTeamTotalLabel(StatisticIndex);

        _logger.LogDebug("DpsStatisticsViewModel constructor completed");
    }

    // ===== Dispose =====
    public void Dispose()
    {
        DebugFunctions.SampleDataRequested -= OnSampleDataRequested;
        _configManager.ConfigurationUpdated -= ConfigManagerOnConfigurationUpdated;
        _timerService.Stop();

        _storage.ServerConnectionStateChanged -= StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated -= StorageOnPlayerInfoUpdatedWithNpcLocalization;
        _storage.BeforeSectionCleared -= StorageOnBeforeSectionCleared;

        _localizationManager.CultureChanged -= OnLocalizationCultureChanged;

        foreach (var dpsStatisticsSubViewModel in StatisticData.Values)
        {
            dpsStatisticsSubViewModel.Initialized = false;
        }

        _isInitialized = false;
        GC.SuppressFinalize(this);
    }

    // ===== Core Public Methods =====
    [RelayCommand]
    public void ResetAll()
    {
        _logger.LogInformation("=== ResetAll START === ScopeTime={ScopeTime}", ScopeTime);

        if (_timerService.IsRunning)
        {
            _timerService.Stop();
        }

        _resetCoordinator.ResetWithHistory(
            ScopeTime,
            saveHistory: true,
            BattleDuration,
            Options.MinimalDurationInSeconds);

        ResetSubViewModelsIfInCurrentScope();

        TeamTotalDamage = 0;
        TeamTotalDps = 0;
        BattleDuration = TimeSpan.Zero;

        var skillBreakdownVm = _windowManagement.SkillBreakdownView.DataContext as SkillBreakdownViewModel;
        skillBreakdownVm?.ClearFromMainRefresh();

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
            if (IsViewingHistory)
            {
                ExitHistoryViewMode();
            }

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
        _logger.LogInformation("ResetSection START");

        _resetCoordinator.ResetCurrentSection();
        if (IsViewingHistory)
        {
            ExitHistoryViewMode();
        }
        ResetSubViewModelsIfInCurrentScope();
        ResetBattleDurationIfInCurrentScope();

        _logger.LogInformation("ResetSection COMPLETE");
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
        InvokeOnDispatcher(() =>
        {
            TeamTotalDamage = e.TotalDamage;
            TeamTotalDps = e.TotalDps;
            CurrentPlayerLabel = GetCurrentPlayerLabel(StatisticIndex);
            TeamTotalLabel = GetTeamTotalLabel(StatisticIndex);
        });
    }

    private string GetTeamLabel(StatisticType statisticType)
    {
        return statisticType switch
        {
            StatisticType.Damage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_Team_Label,
                defaultValue: "Team"),
            StatisticType.Healing => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_Team_Label,
                defaultValue: "Team"),
            StatisticType.TakenDamage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_Team_Label,
                defaultValue: "Team"),
            StatisticType.NpcTakenDamage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_NPC_Label,
                defaultValue: "All NPC"),
            _ => _localizationManager.GetString(
                ResourcesKeys.SkillBreakdown_Label_TotalDamage,
                defaultValue: "Team")
        };
    }

    private string GetCurrentPlayerLabel(StatisticType statisticType)
    {
        return statisticType switch
        {
            StatisticType.Damage => "DPS",
            StatisticType.Healing => "HPS",
            StatisticType.TakenDamage =>"DTPS",
            _ => "DPS"
        };
    }

    private string GetTeamTotalLabel(StatisticType statisticType)
    {
        return statisticType switch
        {
            StatisticType.Damage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamLabel_Damage,
                defaultValue: "Team DPS"),
            StatisticType.Healing => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamLabel_Healing,
                defaultValue: "Team HPS"),
            StatisticType.TakenDamage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamLabel_TakenDamage,
                defaultValue: "Team DTPS"),
            StatisticType.NpcTakenDamage => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamLabel_NpcTakenDamage,
                defaultValue: "NPC DTPS"),
            _ => _localizationManager.GetString(
                ResourcesKeys.DpsStatistics_TeamLabel_Damage,
                defaultValue: "Team DPS")
        };
    }

    private void OnLocalizationCultureChanged(object? sender, CultureInfo culture)
    {
        InvokeOnDispatcher(() =>
        {
            foreach (var info in _storage.ReadOnlyPlayerInfoDatas.Values)
            {
                ApplyNpcLocalizedName(info, culture);
            }

            TeamLabel = GetTeamLabel(StatisticIndex);
            CurrentPlayerLabel = GetCurrentPlayerLabel(StatisticIndex);
            TeamTotalLabel = GetTeamTotalLabel(StatisticIndex);
        });
    }

    [RelayCommand]
    private void RecordWindowPosition(System.Drawing.Rectangle rect)
    {
        AppConfig.StartUpState = rect;
    }

    private void ResetSubViewModelsIf(Func<bool> condition)
    {
        if (!condition()) return;
        ResetSubViewModels();
    }

    private void ResetSubViewModelsIfInCurrentScope()
    {
        ResetSubViewModelsIf(() => ScopeTime == ScopeTime.Current);
    }

    private void ResetSubViewModels()
    {
        _logger.LogInformation("Reset sub view models");
        foreach (var itm in StatisticData.Values)
        {
            itm.Reset();
        }
    }

    private void StartBattleDurationUpdate()
    {
        _battleDurationUpdateTimer.Start();
    }

    private void StopBattleDurationUpdate()
    {
        _battleDurationUpdateTimer.Stop();
    }

    private void StorageOnPlayerInfoUpdatedWithNpcLocalization(PlayerInfo info)
    {
        ApplyNpcLocalizedName(info);
        StorageOnPlayerInfoUpdated(info);
    }

    private void ApplyNpcLocalizedName(PlayerInfo info, CultureInfo? culture = null)
    {
        var templateId = info.NpcTemplateId;
        if (templateId == 0)
            return;

        var resolved = _jsonLocalizationProvider.GetLocalizedObject(
            $"Monster:{templateId}",
            target: null,
            culture: culture ?? CultureInfo.CurrentUICulture) as string;

        if (string.IsNullOrWhiteSpace(resolved))
            return;

        if (!string.Equals(info.Name, resolved, StringComparison.Ordinal))
        {
            info.Name = resolved;
        }
    }
}