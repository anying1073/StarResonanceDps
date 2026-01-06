using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Analyze.Exceptions;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DpsStatisticsOptions : BaseViewModel
{
    [ObservableProperty] private int _minimalDurationInSeconds;

    [RelayCommand]
    private void SetMinimalDuration(int duration)
    {
        MinimalDurationInSeconds = duration;
    }

    /// <summary>
    /// 当最小记录时长改变时保存到配置
    /// </summary>
    partial void OnMinimalDurationInSecondsChanged(int value)
    {
        // 这里需要访问外部的ConfigManager,所以需要传递引用或通过事件通知
        // 简单起见,我们通过DpsStatisticsViewModel来处理
    }
}

public partial class DpsStatisticsViewModel : BaseViewModel, IDisposable
{
    private readonly IApplicationControlService _appControlService;
    private readonly IConfigManager _configManager;
    private readonly Dispatcher _dispatcher;
    private readonly IMessageDialogService _messageDialogService;
    private readonly ILogger<DpsStatisticsViewModel> _logger;

    // 快照服务

    private readonly IDataStorage _storage;

    // Use a single stopwatch for both total and section durations
    private readonly Stopwatch _timer = new();
    private readonly ITopmostService _topmostService;
    private readonly IWindowManagementService _windowManagement;
    [ObservableProperty] private AppConfig _appConfig = new();

    // Whether we are waiting for the first datapoint of a new section
    private bool _awaitingSectionStart;

    // ⭐ New flag: indicates section has timed out but not yet cleared
    private bool _sectionTimedOut;

    [ObservableProperty] private TimeSpan _battleDuration;

    [ObservableProperty] private BattleSnapshotData? _currentSnapshot;

    // ===== 调试用: 更新计数器 =====
    [ObservableProperty] private int _debugUpdateCount;

    // Timer for active update mode
    private DispatcherTimer? _dpsUpdateTimer;
    private DispatcherTimer? _durationTimer;
    private int _indicatorHoverCount;

    // 控制是否显示NPC数据
    [ObservableProperty] private bool _isIncludeNpcData;

    private bool _isInitialized;
    [ObservableProperty] private bool _isServerConnected;

    // 快照查看模式相关字段
    [ObservableProperty] private bool _isViewingSnapshot;

    // Captured elapsed of the last section to freeze UI until new data arrives
    private TimeSpan _lastSectionElapsed = TimeSpan.Zero;
    private DpsDataUpdatedEventHandler? _resumeActiveTimerHandler;

    [ObservableProperty] private ScopeTime _scopeTime = ScopeTime.Current;

    // Snapshot of elapsed time at the moment a new section starts
    private TimeSpan _sectionStartElapsed = TimeSpan.Zero;
    [ObservableProperty] private bool _showContextMenu;
    [ObservableProperty] private bool _showTeamTotalDamage;

    private bool _skipNextSnapshotSave;
    [ObservableProperty] private SortDirectionEnum _sortDirection = SortDirectionEnum.Descending;
    [ObservableProperty] private string _sortMemberPath = "Value";
    [ObservableProperty] private StatisticType _statisticIndex;
    [ObservableProperty] private ulong _teamTotalDamage;
    [ObservableProperty] private double _teamTotalDps;
    [ObservableProperty] private string _teamTotalLabel = "团队DPS"; // 动态标签
    [ObservableProperty] private bool _temporaryMaskPlayerName;

    // 全程累计战斗时长（不包括脱战时间）
    private TimeSpan _totalCombatDuration = TimeSpan.Zero;

    // 保存实时数据订阅状态,以便恢复
    private bool _wasPassiveMode;
    private bool _wasTimerRunning;

    // ⭐ 添加构造函数以初始化StatisticData和DebugFunctions
    public DpsStatisticsViewModel(
        ILogger<DpsStatisticsViewModel> logger,
        IDataStorage storage,
        IConfigManager configManager,
        IWindowManagementService windowManagement,
        ITopmostService topmostService,
        IApplicationControlService appControlService,
        Dispatcher dispatcher,
        DebugFunctions debugFunctions,
        BattleSnapshotService snapshotService, LocalizationManager localizationManager,
        IMessageDialogService messageDialogService)
    {
        _logger = logger;
        _storage = storage;
        _configManager = configManager;
        _windowManagement = windowManagement;
        _topmostService = topmostService;
        _appControlService = appControlService;
        _dispatcher = dispatcher;
        _messageDialogService = messageDialogService;
        DebugFunctions = debugFunctions;
        SnapshotService = snapshotService;

        // 初始化StatisticData字典，为每种统计类型创建对应的SubViewModel
        StatisticData = new Dictionary<StatisticType, DpsStatisticsSubViewModel>
        {
            [StatisticType.Damage] = new(logger, dispatcher, StatisticType.Damage, storage, debugFunctions, this,
                localizationManager),
            [StatisticType.Healing] = new(logger, dispatcher, StatisticType.Healing, storage, debugFunctions, this,
                localizationManager),
            [StatisticType.TakenDamage] = new(logger, dispatcher, StatisticType.TakenDamage, storage, debugFunctions,
                this, localizationManager),
            [StatisticType.NpcTakenDamage] =
                new(logger, dispatcher, StatisticType.NpcTakenDamage, storage, debugFunctions, this,
                    localizationManager)
        };
        // ⭐ 订阅分段清空前事件
        if (_storage is DataStorageV2 storageV2)
        {
            storageV2.BeforeSectionCleared += StorageOnBeforeSectionCleared;
        }

        // 订阅配置更新事件
        _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;

        // 订阅存储事件
        _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
        _storage.NewSectionCreated += StorageOnNewSectionCreated;
        _storage.ServerConnectionStateChanged += StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated += StorageOnPlayerInfoUpdated;
        _storage.ServerChanged += StorageOnServerChanged; // ⭐ 订阅服务器切换事件
        _storage.SectionEnded += SectionEnded;

        // 订阅DebugFunctions事件
        DebugFunctions.SampleDataRequested += OnSampleDataRequested;

        // 初始化AppConfig
        AppConfig = _configManager.CurrentConfig;

        // ⭐ 从配置加载DPS统计页面的设置
        LoadDpsStatisticsSettings();

        _logger.LogDebug("DpsStatisticsViewModel constructor completed");
    }

    private void SectionEnded()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(SectionEnded);
            return;
        }

        _logger.LogInformation("=== SectionEnded event received ===");

        // ⭐ Capture the final section duration before it gets cleared
        var finalSectionDuration = _timer.Elapsed - _sectionStartElapsed;
        _lastSectionElapsed = finalSectionDuration;

        // ⭐ Set the timed out flag to freeze duration display
        _sectionTimedOut = true;

        _logger.LogInformation("Section ended, final duration: {Duration:F1}s", finalSectionDuration.TotalSeconds);

        // ⭐ DON'T set _awaitingSectionStart here - let the data clearing logic handle it
        // This allows the duration to display the final frozen value

        // Update duration display immediately
        UpdateBattleDuration();
    }

    /// <summary>
    /// 从配置加载DPS统计页面的设置
    /// </summary>
    private void LoadDpsStatisticsSettings()
    {
        // 加载技能显示数量
        var savedSkillLimit = _configManager.CurrentConfig.SkillDisplayLimit;
        if (savedSkillLimit > 0)
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SkillDisplayLimit = savedSkillLimit;
            }
            _logger.LogInformation("从配置加载技能显示数量: {Limit}", savedSkillLimit);
        }

        // 加载是否统计NPC
        IsIncludeNpcData = _configManager.CurrentConfig.IsIncludeNpcData;
        _logger.LogInformation("从配置加载统计NPC设置: {Value}", IsIncludeNpcData);

        // 加载是否显示团队总伤
        ShowTeamTotalDamage = _configManager.CurrentConfig.ShowTeamTotalDamage;
        _logger.LogInformation("从配置加载显示团队总伤设置: {Value}", ShowTeamTotalDamage);

        // 加载最小记录时长
        Options.MinimalDurationInSeconds = _configManager.CurrentConfig.MinimalDurationInSeconds;
        _logger.LogInformation("从配置加载最小记录时长: {Duration}秒", Options.MinimalDurationInSeconds);

        // ⭐ 订阅Options的属性变更事件，以便保存配置
        Options.PropertyChanged += Options_PropertyChanged;
    }

    /// <summary>
    /// Options属性变更处理
    /// </summary>
    private void Options_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DpsStatisticsOptions.MinimalDurationInSeconds))
        {
            var newValue = Options.MinimalDurationInSeconds;
            _configManager.CurrentConfig.MinimalDurationInSeconds = newValue;
            _ = _configManager.SaveAsync();
            _logger.LogInformation("最小记录时长已保存到配置: {Duration}秒", newValue);
        }
    }

    // ⭐ 新增: 暴露快照服务给View绑定
    public BattleSnapshotService SnapshotService { get; }

    public Dictionary<StatisticType, DpsStatisticsSubViewModel> StatisticData { get; }

    public DpsStatisticsSubViewModel CurrentStatisticData => StatisticData[StatisticIndex];

    public DebugFunctions DebugFunctions { get; }

    public DpsStatisticsOptions Options { get; } = new();

    /// <summary>
    /// ⭐ 新增: 主题颜色（用于Header和Footer）
    /// </summary>
    public string ThemeColor
    {
        get
        {
            var color = AppConfig.ThemeColor;
            _logger.LogDebug("ThemeColor requested: {Color}", color);
            return color;
        }
    }

    /// <summary>
    /// ⭐ 新增: 背景图片路径
    /// </summary>
    public string BackgroundImagePath
    {
        get
        {
            var path = AppConfig.BackgroundImagePath;
            _logger.LogDebug("BackgroundImagePath requested: {Path}", path);
            return path;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Unsubscribe from DebugFunctions events
        DebugFunctions.SampleDataRequested -= OnSampleDataRequested;
        _configManager.ConfigurationUpdated -= ConfigManagerOnConfigurationUpdated;

        if (_durationTimer != null)
        {
            _durationTimer.Stop();
            _durationTimer.Tick -= DurationTimerOnTick;
        }

        // Stop and dispose DPS update timer
        if (_dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Stop();
            _dpsUpdateTimer.Tick -= DpsUpdateTimerOnTick;
            _dpsUpdateTimer = null;
        }

        _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
        _storage.NewSectionCreated -= StorageOnNewSectionCreated;
        _storage.ServerConnectionStateChanged -= StorageOnServerConnectionStateChanged;
        _storage.PlayerInfoUpdated -= StorageOnPlayerInfoUpdated;
        _storage.Dispose();

        foreach (var dpsStatisticsSubViewModel in StatisticData.Values)
        {
            dpsStatisticsSubViewModel.Initialized = false;
        }

        _isInitialized = false;

        // Detach one-shot resume handler if any
        if (_resumeActiveTimerHandler != null)
        {
            _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
            _resumeActiveTimerHandler = null;
        }

        // ⭐ 取消订阅
        if (_storage is DataStorageV2 storageV2)
        {
            storageV2.BeforeSectionCleared -= StorageOnBeforeSectionCleared;
        }
    }

    /// <summary>
    /// When hovering indicator popups, freeze sorting to avoid flicker caused by item reordering.
    /// </summary>
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

    [RelayCommand]
    private void OpenSkillBreakdown(StatisticDataViewModel? slot)
    {
        var target = slot ?? CurrentStatisticData.SelectedSlot;
        if (target is null) return;

        var vm = _windowManagement.SkillBreakdownView.DataContext as SkillBreakdownViewModel;
        Debug.Assert(vm != null, "vm!=null");

        var playerStats = _storage.GetStatistics(fullSession: ScopeTime == ScopeTime.Total);
        if (!playerStats.TryGetValue(target.Player.Uid, out var stats)) return;
        _logger.LogInformation("Using PlayerStatistics for SkillBreakdown (accurate data)");

        var playerInfo = _storage.ReadOnlyPlayerInfoDatas.TryGetValue(target.Player.Uid, out var info)
            ? info
            : null;

        vm.InitializeFrom(stats, playerInfo, StatisticIndex, target);
        _windowManagement.SkillBreakdownView.Show();
        _windowManagement.SkillBreakdownView.Activate();
    }

    private void ConfigManagerOnConfigurationUpdated(object? sender, AppConfig newConfig)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(ConfigManagerOnConfigurationUpdated, sender, newConfig);
            return;
        }

        var oldMode = AppConfig.DpsUpdateMode;
        var oldInterval = AppConfig.DpsUpdateInterval;

        AppConfig = newConfig;

        // ⭐ 强制通知主题颜色和背景图片变更
        OnPropertyChanged(nameof(ThemeColor));
        OnPropertyChanged(nameof(BackgroundImagePath));

        // If update mode or interval changed, reconfigure update mechanism
        if (oldMode != newConfig.DpsUpdateMode || oldInterval != newConfig.DpsUpdateInterval)
        {
            ConfigureDpsUpdateMode();
        }
    }

    private void OnSampleDataRequested(object? sender, EventArgs e)
    {
        // Handle the event from DebugFunctions
        AddRandomData();
    }

    private void StorageOnServerConnectionStateChanged(bool serverConnectionState)
    {
        if (_dispatcher.CheckAccess())
        {
            IsServerConnected = serverConnectionState;
        }
        else
        {
            _dispatcher.Invoke(() => IsServerConnected = serverConnectionState);
        }
    }

    /// <summary>
    /// 切换窗口置顶状态（命令）。
    /// 通过绑定 Window.Topmost 到 AppConfig.TopmostEnabled 实现。
    /// </summary>
    [RelayCommand]
    private async Task ToggleTopmost()
    {
        AppConfig.TopmostEnabled = !AppConfig.TopmostEnabled;
        try
        {
            await _configManager.SaveAsync(AppConfig);
        }
        catch (InvalidOperationException ex)
        {
            // Ignore
            _logger.LogError(ex, "Failed to save AppConfig");
        }
    }

    [RelayCommand]
    private void OpenPersonalDpsView()
    {
        // 检查用户是否设置了UID
        var userUid = _configManager.CurrentConfig.Uid;

        if (userUid <= 0)
        {
            // UID未设置,弹出提示并打开设置页面
            _logger.LogWarning("尝试打开个人打桩模式但UID未设置");

            _messageDialogService.Show(
                "需要设置角色UID",
                "请先在设置中配置您的角色UID，才能使用个人打桩模式。\n\n如何获取UID：进入游戏后，左下角玩家编号就是UID",
                _windowManagement.DpsStatisticsView);

            // 打开设置页面(角色设置区域)
            _windowManagement.SettingsView.Show();
            _windowManagement.SettingsView.Activate(); // 确保窗口激活到前台

            return; // 不打开个人打桩窗口
        }

        // UID已设置,正常打开个人打桩窗口
        _logger.LogInformation("打开个人打桩模式, UID={Uid}", userUid);
        _windowManagement.PersonalDpsView.Show();
        _windowManagement.DpsStatisticsView.Hide();
    }


    [RelayCommand]
    public void ResetAll()
    {
        _logger.LogInformation("=== ResetAll START === ScopeTime={ScopeTime}", ScopeTime);

        // ⭐ 关键修复: 停止计时器(如果在运行)
        if (_timer.IsRunning)
        {
            _timer.Stop();
            _logger.LogInformation("已停止战斗计时器");
        }

        // 保存快照(在清空数据之前)
        if (_storage.HasData())
        {
            try
            {
                if (ScopeTime == ScopeTime.Current)
                {
                    // 当前模式:保存当前战斗快照
                    SnapshotService.SaveCurrentSnapshot(_storage, BattleDuration, Options.MinimalDurationInSeconds);
                    _logger.LogInformation("保存当前战斗快照成功");

                    // ⭐ 设置标志,跳过 StorageOnNewSectionCreated 中的快照保存
                    _skipNextSnapshotSave = true;
                }
                else
                {
                    // 全程模式:保存全程快照
                    SnapshotService.SaveTotalSnapshot(_storage, BattleDuration, Options.MinimalDurationInSeconds);
                    _logger.LogInformation("保存全程快照成功");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存快照失败");
            }
        }

        // ⭐ 新逻辑: 根据当前视图模式决定清空策略
        if (ScopeTime == ScopeTime.Current)
        {
            // 当前模式: 只清空当前(sectioned)数据,保留全程数据
            _logger.LogInformation("Current mode: Clearing only sectioned data, preserving total data");
            _storage.ClearDpsData(); // 只清空当前数据

            // ⭐ 重置计时器相关状态
            _sectionStartElapsed = _timer.Elapsed; // ⬅️ 保留计时器已经过的时间
            _lastSectionElapsed = TimeSpan.Zero;
            _awaitingSectionStart = false;
            _sectionTimedOut = false; // ⭐ Reset timeout flag
        }
        else
        {
            // 全程模式: 清空所有数据(全程+当前)
            _logger.LogInformation("Total mode: Clearing ALL data (total + sectioned)");
            _storage.ClearAllDpsData(); // 清空全程+当前数据

            // ⭐ 完全重置计时器
            _timer.Reset(); // ⬅️ 重置计时器到0
            _sectionStartElapsed = TimeSpan.Zero;
            _lastSectionElapsed = TimeSpan.Zero;
            _awaitingSectionStart = false;
            _sectionTimedOut = false; // ⭐ Reset timeout flag

            // 清空全程累计时长
            _totalCombatDuration = TimeSpan.Zero;
            _logger.LogInformation("已重置全程累计战斗时长");
        }

        // 清空UI数据
        foreach (var subVm in StatisticData.Values)
        {
            subVm.Reset();
        }

        // 重置团队统计
        TeamTotalDamage = 0;
        TeamTotalDps = 0;
        BattleDuration = TimeSpan.Zero; // ⬅️ 重置显示时长

        // 强制重新初始化更新机制
        if (!_isInitialized)
        {
            _logger.LogWarning("ResetAll called but ViewModel not initialized!");
            return;
        }

        try
        {
            _logger.LogInformation("ResetAll: Mode={Mode}, Interval={Interval}ms",
                AppConfig.DpsUpdateMode, AppConfig.DpsUpdateInterval);

            // 清理残留
            if (_resumeActiveTimerHandler != null)
            {
                _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
                _resumeActiveTimerHandler = null;
            }

            // ===== 关键修复: 完全重建更新机制 =====

            // 步骤1: 完全停止现有机制
            if (_dpsUpdateTimer != null)
            {
                _dpsUpdateTimer.Stop();
                _dpsUpdateTimer.Tick -= DpsUpdateTimerOnTick;
                _dpsUpdateTimer = null;
            }

            _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
            _storage.NewSectionCreated -= StorageOnNewSectionCreated;

            _logger.LogInformation("ResetAll: Stopped all existing update mechanisms");

            // 步骤2: 根据模式重新创建
            if (AppConfig.DpsUpdateMode == DpsUpdateMode.Active)
            {
                // Active模式: 创建新定时器
                _dpsUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(Math.Clamp(AppConfig.DpsUpdateInterval, 100, 5000))
                };
                _dpsUpdateTimer.Tick += DpsUpdateTimerOnTick;
                _dpsUpdateTimer.Start();

                _logger.LogInformation(
                    "ResetAll: Created and started NEW timer. Enabled={Enabled}, Interval={Interval}ms",
                    _dpsUpdateTimer.IsEnabled, _dpsUpdateTimer.Interval.TotalMilliseconds);
            }
            else
            {
                // Passive模式: 订阅事件
                _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
                _logger.LogInformation("ResetAll: Subscribed to DpsDataUpdated event");
            }

            // 始终订阅NewSectionCreated
            _storage.NewSectionCreated += StorageOnNewSectionCreated;

            // 步骤3: 立即触发一次更新显示空状态
            RefreshData();
            UpdateBattleDuration();

            _logger.LogInformation(
                "=== ResetAll COMPLETE === ScopeTime={ScopeTime}, Mode={Mode}, Timer={Timer}, Event subscribed={Event}",
                ScopeTime,
                AppConfig.DpsUpdateMode,
                _dpsUpdateTimer?.IsEnabled ?? false,
                AppConfig.DpsUpdateMode == DpsUpdateMode.Passive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ResetAll");
        }
    }

    [RelayCommand]
    public void ResetSection()
    {
        _storage.ClearDpsData();
        // Move section start to current elapsed so section duration becomes zero
        _sectionStartElapsed = _timer.Elapsed;
        _sectionTimedOut = false; // ⭐ Reset timeout flag
        _lastSectionElapsed = TimeSpan.Zero;
    }

    /// <summary>
    /// 读取用户缓存
    /// </summary>
    private void LoadPlayerCache()
    {
        try
        {
            _storage.LoadPlayerInfoFromFile();
        }
        catch (FileNotFoundException)
        {
            // 没有缓存
            // cache not found
        }
        catch (DataTamperedException)
        {
            _storage.ClearAllPlayerInfos();
            _storage.SavePlayerInfoToFile();
        }
    }

    [RelayCommand]
    private void OnLoaded()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        foreach (var vm in StatisticData.Values)
        {
            vm.Initialized = true;
        }

        _logger.LogDebug(WpfLogEvents.VmLoaded, "DpsStatisticsViewModel loaded");
        LoadPlayerCache();

        EnsureDurationTimerStarted();
        UpdateBattleDuration();

        // Configure update mode based on settings
        ConfigureDpsUpdateMode();
    }

    [RelayCommand]
    private void OnUnloaded()
    {
        // ===== 重要: 不要在Unloaded时停止定时器,因为ResetAll可能会触发这个 =====
        // 只记录日志,不实际停止任何东西
        _logger.LogDebug("DpsStatisticsViewModel OnUnloaded called - NOT stopping timers");

        // 注释掉原来的停止逻辑
        // StopDpsUpdateTimer();
        // if (_durationTimer != null)
        // {
        //     _durationTimer.Stop();
        // }
    }

    [RelayCommand]
    private void OnResize()
    {
        _logger.LogDebug("Window Resized");
    }

    /// <summary>
    /// Configure DPS update mode based on AppConfig settings
    /// </summary>
    private void ConfigureDpsUpdateMode()
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("ConfigureDpsUpdateMode called but not initialized");
            return;
        }

        _logger.LogInformation(
            "Configuring DPS update mode: {Mode}, Interval: {Interval}ms",
            AppConfig.DpsUpdateMode,
            AppConfig.DpsUpdateInterval);

        // Ensure any one-shot handler from previous mode is removed
        if (_resumeActiveTimerHandler != null)
        {
            _storage.DpsDataUpdated -= _resumeActiveTimerHandler;
            _resumeActiveTimerHandler = null;
            _logger.LogDebug("Removed resume active timer handler");
        }

        switch (AppConfig.DpsUpdateMode)
        {
            case DpsUpdateMode.Passive:
                // Passive mode: subscribe to event
                StopDpsUpdateTimer();
                _dpsUpdateTimer = null; // ensure timer is released
                _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated; // Unsubscribe first to avoid duplicate
                _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
                _storage.NewSectionCreated -= StorageOnNewSectionCreated;
                _storage.NewSectionCreated += StorageOnNewSectionCreated;
                _logger.LogDebug("Passive mode enabled: DpsDataUpdated event subscribed");
                break;

            case DpsUpdateMode.Active:
                // Active mode: use timer, but still listen for section events to pause/resume
                _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
                _storage.NewSectionCreated -= StorageOnNewSectionCreated;
                _storage.NewSectionCreated += StorageOnNewSectionCreated;
                StartDpsUpdateTimer(AppConfig.DpsUpdateInterval);
                _logger.LogDebug("Active mode enabled: timer started with interval {Interval}ms",
                    AppConfig.DpsUpdateInterval);
                break;

            default:
                _logger.LogWarning("Unknown DPS update mode: {Mode}", AppConfig.DpsUpdateMode);
                break;
        }

        _logger.LogInformation("Update mode configuration complete. Mode: {Mode}, Timer enabled: {TimerEnabled}",
            AppConfig.DpsUpdateMode,
            _dpsUpdateTimer?.IsEnabled ?? false);
    }

    /// <summary>
    /// Start or restart DPS update timer with specified interval
    /// </summary>
    private void StartDpsUpdateTimer(int intervalMs)
    {
        // Validate interval
        var clampedInterval = Math.Clamp(intervalMs, 100, 5000);
        if (clampedInterval != intervalMs)
        {
            _logger.LogWarning("DPS update interval {Original}ms clamped to {Clamped}ms",
                intervalMs, clampedInterval);
        }

        if (_dpsUpdateTimer == null)
        {
            _dpsUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(clampedInterval)
            };
            _dpsUpdateTimer.Tick += DpsUpdateTimerOnTick;
            _logger.LogInformation("Created NEW DPS update timer with interval {Interval}ms", clampedInterval);
        }
        else
        {
            _dpsUpdateTimer.Stop();
            _dpsUpdateTimer.Interval = TimeSpan.FromMilliseconds(clampedInterval);
            _logger.LogInformation("Updated EXISTING DPS update timer interval to {Interval}ms", clampedInterval);
        }

        _dpsUpdateTimer.Start();
        _logger.LogInformation(
            "DPS update timer STARTED. Enabled={Enabled}, Interval={Interval}ms",
            _dpsUpdateTimer.IsEnabled,
            _dpsUpdateTimer.Interval.TotalMilliseconds);
    }

    /// <summary>
    /// Stop DPS update timer
    /// </summary>
    private void StopDpsUpdateTimer()
    {
        if (_dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Stop();
            _dpsUpdateTimer.Tick -= DpsUpdateTimerOnTick;
            _logger.LogDebug("DPS update timer stopped");
        }
    }

    /// <summary>
    /// Timer tick handler for active update mode
    /// </summary>
    private void DpsUpdateTimerOnTick(object? sender, EventArgs e)
    {
        // 每10次tick输出一次日志,避免日志爆炸
        if (_dpsUpdateTimer != null && _dpsUpdateTimer.IsEnabled)
        {
            var currentSecond = DateTime.Now.Second;
            if (currentSecond % 10 == 0) // 每10秒输出一次
            {
                _logger.LogInformation("Timer tick triggered - calling DataStorage_DpsDataUpdated");
            }
        }

        // Call the same update logic as event-based mode
        DataStorage_DpsDataUpdated();
    }


    // 添加事件处理方法(约第1175行 StorageOnNewSectionCreated 之前):
    // ⭐ 修复: 分段清空前事件处理 - 同步保存快照
    private void StorageOnBeforeSectionCleared()
    {
        // ⭐ 关键修复: 使用 Invoke 而不是 BeginInvoke，确保保存完成后再返回
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(StorageOnBeforeSectionCleared);
            return;
        }

        _logger.LogInformation("=== BeforeSectionCleared: 准备保存快照 (数据还在!) ===");

        // 只在 Current 模式下保存
        if (ScopeTime != ScopeTime.Current)
        {
            _logger.LogDebug("跳过快照保存: ScopeTime={ScopeTime}, DataCount={Count}", ScopeTime, _storage.GetStatisticsCount(true));
            return;
        }

        var statCount = _storage.GetStatisticsCount(false);
        if (statCount <= 0) return;
        try
        {
            var duration = _timer.Elapsed - _sectionStartElapsed;
            _logger.LogInformation(
                "脱战自动保存快照, 数据量: {Count}, 时长: {Duration:F1}s",
                _storage.GetStatisticsCount(false),
                duration.TotalSeconds);

            // ⭐ 关键: 这里会同步阻塞，直到快照保存完成
            SnapshotService.SaveCurrentSnapshot(_storage, duration, Options.MinimalDurationInSeconds);

            // 设置标志,跳过 StorageOnNewSectionCreated 中的重复保存
            _skipNextSnapshotSave = true;

            _logger.LogInformation("✅ 脱战自动保存快照成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 脱战自动保存快照失败");
        }
    }

    private void DataStorage_DpsDataUpdated()
    {
        _logger.LogTrace("DataStorage_DpsDataUpdated called");

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(DataStorage_DpsDataUpdated);
            return;
        }


        var stat = _storage.GetStatistics(ScopeTime == ScopeTime.Total);

        // ⭐ Removed the automatic snapshot saving logic here since it's now handled by:
        // 1. BeforeSectionCleared event (triggered by DataStorageV2 before clearing)
        // 2. SectionEnded event (triggered when timeout occurs)

        // 只处理战斗开始的情况
        if (!_timer.IsRunning && HasData(stat))
        {
            _logger.LogInformation("检测到战斗数据,启动计时器");
            _timer.Start();
            _sectionStartElapsed = _timer.Elapsed;
            _sectionTimedOut = false; // ⭐ Reset timeout flag when combat starts
        }

        // If we're waiting for the first datapoint of new section and data arrives, reset UI
        if (_awaitingSectionStart)
        {
            var hasSectionDamage = HasData(_storage.GetStatistics(false));
            _logger.LogDebug("Awaiting section start - has section damage: {HasSectionDamage}", hasSectionDamage);
            if (hasSectionDamage)
            {
                foreach (var subVm in StatisticData.Values)
                {
                    subVm.Reset();
                }

                _sectionStartElapsed = _timer.Elapsed;
                _lastSectionElapsed = TimeSpan.Zero;
                _awaitingSectionStart = false;
                _sectionTimedOut = false; // ⭐ Reset timeout flag when new section starts
                _skipNextSnapshotSave = false; // ⬅️ 重置标志
                _logger.LogDebug("Section start processed, new section begins");
            }
        }

        // Always update data (even if empty) to ensure UI reflects current state
        UpdateData(stat);
        UpdateBattleDuration();
    }

    private bool HasData(bool full)
    {
        return _storage.GetStatisticsCount(full) > 0;
    }

    private bool HasData(IReadOnlyDictionary<long, PlayerStatistics> stats)
    {
        return stats.Count > 0;
    }

    private void UpdateData(IReadOnlyDictionary<long, PlayerStatistics> data)
    {
        _logger.LogTrace(WpfLogEvents.VmUpdateData, "Update data requested: {Count} entries", data.Count);

        // ⭐ 修复: 使用配置中的UID而不是storage的CurrentPlayerInfo.UID
        var currentPlayerUid = _configManager.CurrentConfig.Uid;

        // Pre-process data once for all statistic types
        var processedDataByType = PreProcessDataForAllTypes(data);

        // Update each subViewModel with its pre-processed data
        foreach (var (statisticType, processedData) in processedDataByType)
        {
            if (!StatisticData.TryGetValue(statisticType, out var subViewModel)) continue;
            subViewModel.ScopeTime = ScopeTime;
            subViewModel.UpdateDataOptimized(processedData, currentPlayerUid);
        }

        // ⭐ REMOVED: RecordSamples is now called automatically in Core layer (DataStorageV2)
        // Sample recording is handled by the Core layer for proper separation of concerns

        // Update team total damage and DPS
        UpdateTeamTotalStats(data);
    }


    /// <summary>
    /// Updates team total damage and DPS statistics
    /// </summary>
    private void UpdateTeamTotalStats(IReadOnlyDictionary<long, PlayerStatistics> data)
    {
        if (!ShowTeamTotalDamage) return;

        ulong totalValue = 0;
        double totalElapsedSeconds = 0;
        var playerCount = 0; // 统计玩家数量
        var npcCount = 0; // 统计NPC数量
        var skippedPlayers = 0; // 统计跳过的玩家
        var skippedNpcs = 0; // 统计跳过的NPC

        // 根据当前统计类型计算不同的数值
        foreach (var dpsData in data.Values)
        {
            // 跳过NPC数据 (除非是NPC承伤统计)
            if (dpsData.IsNpc && StatisticIndex != StatisticType.NpcTakenDamage)
            {
                skippedNpcs++;
                continue;
            }

            // 跳过玩家数据 (如果是NPC承伤统计)
            if (!dpsData.IsNpc && StatisticIndex == StatisticType.NpcTakenDamage)
            {
                skippedPlayers++;
                continue;
            }

            // 统计计入的数据类型
            if (dpsData.IsNpc) npcCount++;
            else playerCount++;

            // 根据统计类型累加不同的数值
            ulong value = StatisticIndex switch
            {
                StatisticType.Damage => dpsData.AttackDamage.Total.ConvertToUnsigned(),
                StatisticType.Healing => dpsData.Healing.Total.ConvertToUnsigned(),
                StatisticType.TakenDamage => dpsData.TakenDamage.Total.ConvertToUnsigned(),
                StatisticType.NpcTakenDamage => dpsData.TakenDamage.Total.ConvertToUnsigned(),
                _ => 0
            };

            totalValue += value;

            // 计算经过时间
            var elapsedTicks = dpsData.LastTick - (dpsData.StartTick ?? 0);
            if (elapsedTicks > 0)
            {
                var elapsedSeconds = TimeSpan.FromTicks(elapsedTicks).TotalSeconds;
                // 使用最大经过时间作为团队持续时间
                if (elapsedSeconds > totalElapsedSeconds)
                {
                    totalElapsedSeconds = elapsedSeconds;
                }
            }
        }

        // 更新标签
        TeamTotalLabel = StatisticIndex switch
        {
            StatisticType.Damage => "团队DPS",
            StatisticType.Healing => "团队治疗",
            StatisticType.TakenDamage => "团队承伤",
            StatisticType.NpcTakenDamage => "NPC承伤",
            _ => "团队DPS"
        };

        // ⭐ 关键修复: 只有在有新数据时更新,否则保持上次的值
        if (totalValue > 0 || data.Count > 0)
        {
            TeamTotalDamage = totalValue;
            TeamTotalDps = totalElapsedSeconds > 0 ? totalValue / totalElapsedSeconds : 0;
        }

        // 详细日志输出(只在有数据时输出,避免日志爆炸)
        if (data.Count > 0)
        {
            _logger.LogDebug(
                "TeamTotal [{Type}]: Total={Total:N0}, DPS={Dps:N0}, " +
                "Players={Players}(skipped={SkipP}), NPCs={NPCs}(skipped={SkipN}), " +
                "Duration={Duration:F1}s, Label={Label}",
                StatisticIndex, totalValue, TeamTotalDps,
                playerCount, skippedPlayers, npcCount, skippedNpcs,
                totalElapsedSeconds, TeamTotalLabel);
        }
    }


    /// <summary>
    /// Updates team total damage and DPS statistics
    /// </summary>
    private void UpdateTeamTotalStats(IReadOnlyList<DpsData> data)
    {
        if (!ShowTeamTotalDamage) return;

        ulong totalValue = 0;
        double totalElapsedSeconds = 0;
        var playerCount = 0; // 统计玩家数量
        var npcCount = 0; // 统计NPC数量
        var skippedPlayers = 0; // 统计跳过的玩家
        var skippedNpcs = 0; // 统计跳过的NPC

        // 根据当前统计类型计算不同的数值
        foreach (var dpsData in data)
        {
            // 跳过NPC数据 (除非是NPC承伤统计)
            if (dpsData.IsNpcData && StatisticIndex != StatisticType.NpcTakenDamage)
            {
                skippedNpcs++;
                continue;
            }

            // 跳过玩家数据 (如果是NPC承伤统计)
            if (!dpsData.IsNpcData && StatisticIndex == StatisticType.NpcTakenDamage)
            {
                skippedPlayers++;
                continue;
            }

            // 统计计入的数据类型
            if (dpsData.IsNpcData) npcCount++;
            else playerCount++;

            // 根据统计类型累加不同的数值
            ulong value = StatisticIndex switch
            {
                StatisticType.Damage => dpsData.TotalAttackDamage.ConvertToUnsigned(),
                StatisticType.Healing => dpsData.TotalHeal.ConvertToUnsigned(),
                StatisticType.TakenDamage => dpsData.TotalTakenDamage.ConvertToUnsigned(),
                StatisticType.NpcTakenDamage => dpsData.TotalTakenDamage.ConvertToUnsigned(),
                _ => 0
            };

            totalValue += value;

            // 计算经过时间
            var elapsedTicks = dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0);
            if (elapsedTicks > 0)
            {
                var elapsedSeconds = TimeSpan.FromTicks(elapsedTicks).TotalSeconds;
                // 使用最大经过时间作为团队持续时间
                if (elapsedSeconds > totalElapsedSeconds)
                {
                    totalElapsedSeconds = elapsedSeconds;
                }
            }
        }

        // 更新标签
        TeamTotalLabel = StatisticIndex switch
        {
            StatisticType.Damage => "团队DPS",
            StatisticType.Healing => "团队治疗",
            StatisticType.TakenDamage => "团队承伤",
            StatisticType.NpcTakenDamage => "NPC承伤",
            _ => "团队DPS"
        };

        // ⭐ 关键修复: 只有在有新数据时更新,否则保持上次的值
        if (totalValue > 0 || data.Count > 0)
        {
            TeamTotalDamage = totalValue;
            TeamTotalDps = totalElapsedSeconds > 0 ? totalValue / totalElapsedSeconds : 0;
        }

        // 详细日志输出(只在有数据时输出,避免日志爆炸)
        if (data.Count > 0)
        {
            _logger.LogDebug(
                "TeamTotal [{Type}]: Total={Total:N0}, DPS={Dbs:N0}, " +
                "Players={Players}(skipped={SkipP}), NPCs={NPCs}(skipped={SkipN}), " +
                "Duration={Duration:F1}s, Label={Label}",
                StatisticIndex, totalValue, TeamTotalDps,
                playerCount, skippedPlayers, npcCount, skippedNpcs,
                totalElapsedSeconds, TeamTotalLabel);
        }
    }

    private TimeSpan ComputeSectionDuration()
    {
        // Duration after the new section is created
        var elapsed = _timer.Elapsed - _sectionStartElapsed;
        if (_awaitingSectionStart || elapsed < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return elapsed;
    }

    /// <summary>
    /// Pre-processes data once for all statistic types to avoid redundant iterations
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> PreProcessDataForAllTypes(
        IReadOnlyDictionary<long, PlayerStatistics> data)
    {
        var result = new Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>>
        {
            [StatisticType.Damage] = new(),
            [StatisticType.Healing] = new(),
            [StatisticType.TakenDamage] = new(),
            [StatisticType.NpcTakenDamage] = new()
        };

        // 获取当前用户UID用于隐藏自己的DPS数据
        var currentPlayerUid = _configManager.CurrentConfig.Uid;

        foreach (var playerStats in data.Values)
        {
            var durationTicks = playerStats.LastTick - (playerStats.StartTick ?? 0);

            // Build skill lists once for reuse
            var (totalDmg, totalHeal, totalTaken) = BuildSkillListFromStats(playerStats);

            // ✅ Process Damage - 根据IsIncludeNpcData控制NPC数据显示
            var damageValue = (ulong)Math.Max(0, playerStats.AttackDamage.Total);
            if (damageValue > 0)
            {
                // 1. 玩家数据始终显示
                // 2. NPC数据只在勾选"统计BOSS"时显示
                var shouldShowInDamageList = !playerStats.IsNpc || IsIncludeNpcData;

                if (shouldShowInDamageList)
                {
                    result[StatisticType.Damage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, damageValue, durationTicks, totalDmg, totalHeal, totalTaken, playerStats.Uid);
                }
            }

            // Process Healing - 只统计玩家治疗
            var healingValue = (ulong)Math.Max(0, playerStats.Healing.Total);
            if (healingValue > 0 && !playerStats.IsNpc)
            {
                result[StatisticType.Healing][playerStats.Uid] = new DpsDataProcessed(
                    playerStats, healingValue, durationTicks, totalDmg, totalHeal, totalTaken, playerStats.Uid);
            }

            // Process TakenDamage
            var takenDamageValue = (ulong)Math.Max(0, playerStats.TakenDamage.Total);
            if (takenDamageValue > 0)
            {
                // ⭐ NPC承伤统计:只显示NPC
                if (playerStats.IsNpc)
                {
                    _logger.LogDebug(
                        $"NPC TakenDamage: UID={playerStats.Uid}, Value={takenDamageValue}, IsNpcData={playerStats.IsNpc}");
                    result[StatisticType.NpcTakenDamage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, takenDamageValue, durationTicks, totalDmg, totalHeal, totalTaken, playerStats.Uid);
                }
                else // 玩家 TakenDamage - 只显示玩家
                {
                    result[StatisticType.TakenDamage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, takenDamageValue, durationTicks, totalDmg, totalHeal, totalTaken, playerStats.Uid);
                }
            }
        }

        _logger.LogDebug(
            "PreProcess complete (PlayerStatistics path): Damage count = {Count}, NpcTakenDamage count = {I}, IsIncludeNpcData = {IsIncludeNpcData}", result[StatisticType.Damage].Count, result[StatisticType.NpcTakenDamage].Count, IsIncludeNpcData);
        return result;
    }

    public record struct Data
    {
        public int HitCount { get; set; }
        public long TotalValue { get; set; }
        public long NormalValue { get; set; }
        public double Average { get; set; }
        public double CritRate { get; set; }
        public long CritValue { get; set; }
        public int CritCount { get; set; }
        public long LuckyValue { get; set; }
        public int LuckyCount { get; set; }

        public readonly void Deconstruct(out int hitCount, out long totalValue, out long normalValue,
            out double average, out double critRate, out long critValue, out int critCount, out long luckyValue,
            out int luckyCount)
        {
            hitCount = HitCount;
            totalValue = TotalValue;
            normalValue = NormalValue;
            average = Average;
            critRate = CritRate;
            critValue = CritValue;
            critCount = CritCount;
            luckyValue = LuckyValue;
            luckyCount = LuckyCount;
        }
    }


    /// <summary>
    /// Build skill lists from PlayerStatistics (no battle log iteration!)
    /// </summary>
    private (List<SkillItemViewModel> damage, List<SkillItemViewModel> healing, List<SkillItemViewModel> taken)
        BuildSkillListFromStats(PlayerStatistics playerStats)
    {
        // ✅ Damage/Healing skills: Use EmbeddedSkillConfig to distinguish type
        var damageSkills = new List<SkillItemViewModel>();
        var healingSkills = new List<SkillItemViewModel>();

        foreach (var (skillId, skillStat) in playerStats.AttackDamage.Skills)
        {
            var skillName = EmbeddedSkillConfig.TryGet(skillId.ToString(), out var def)
                ? def.Name
                : skillId.ToString();

            var vm = new SkillItemViewModel
            {
                SkillId = skillId,
                SkillName = skillName,
                Damage = new SkillItemViewModel.SkillValue
                {
                    TotalValue = (long)skillStat.TotalValue,
                    HitCount = skillStat.UseTimes,
                    CritCount = skillStat.CritTimes,
                    LuckyCount = skillStat.LuckyTimes,
                    Average = skillStat.UseTimes > 0 ? Math.Round((double)skillStat.TotalValue / skillStat.UseTimes) : 0,
                    CritRate = skillStat.UseTimes > 0 ? (double)skillStat.CritTimes / skillStat.UseTimes : 0,
                    CritValue = 0,  // Not available in PlayerStatistics
                    LuckyValue = 0  // Not available in PlayerStatistics
                }
            };

            damageSkills.Add(vm);
        }

        foreach (var (skillId, skillStat) in playerStats.Healing.Skills)
        {
            var skillName = EmbeddedSkillConfig.TryGet(skillId.ToString(), out var def)
                ? def.Name
                : skillId.ToString();

            var vm = new SkillItemViewModel
            {
                SkillId = skillId,
                SkillName = skillName,
                Heal = new SkillItemViewModel.SkillValue
                {
                    TotalValue = (long)skillStat.TotalValue,
                    HitCount = skillStat.UseTimes,
                    CritCount = skillStat.CritTimes,
                    LuckyCount = skillStat.LuckyTimes,
                    Average = skillStat.UseTimes > 0 ? Math.Round((double)skillStat.TotalValue / skillStat.UseTimes) : 0,
                    CritRate = skillStat.UseTimes > 0 ? (double)skillStat.CritTimes / skillStat.UseTimes : 0,
                    CritValue = 0,  // Not available in PlayerStatistics
                    LuckyValue = 0  // Not available in PlayerStatistics
                }
            };

            healingSkills.Add(vm);
        }

        // Sort by total value descending
        damageSkills = damageSkills.OrderByDescending(s => s.Damage?.TotalValue ?? 0).ToList();
        healingSkills = healingSkills.OrderByDescending(s => s.Heal?.TotalValue ?? 0).ToList();

        // ✅ Taken damage skills: Direct from PlayerStatistics.TakenDamageSkills
        var takenSkills = playerStats.TakenDamage.Skills.Values
            .OrderByDescending(s => s.TotalValue)
            .Select(s => new SkillItemViewModel
            {
                SkillId = s.SkillId,
                SkillName = EmbeddedSkillConfig.TryGet(s.SkillId.ToString(), out var def)
                    ? def.Name
                    : s.SkillId.ToString(),
                TakenDamage = new SkillItemViewModel.SkillValue
                {
                    TotalValue = (long)s.TotalValue,
                    HitCount = s.UseTimes,
                    CritCount = s.CritTimes,
                    LuckyCount = s.LuckyTimes,
                    Average = s.UseTimes > 0 ? Math.Round((double)s.TotalValue / s.UseTimes) : 0,
                    CritRate = s.UseTimes > 0 ? (double)s.CritTimes / s.UseTimes : 0,
                    CritValue = 0,
                    LuckyValue = 0
                }
            })
            .ToList();

        return (damageSkills, healingSkills, takenSkills);
    }

    [RelayCommand]
    public void AddRandomData()
    {
        UpdateData();
    }

    [RelayCommand]
    private void SetSkillDisplayLimit(int limit)
    {
        var clampedLimit = Math.Max(0, limit);
        _logger.LogDebug("SetSkillDisplayLimit: 修改技能显示条数为 {Limit}", clampedLimit);

        foreach (var vm in StatisticData.Values)
        {
            vm.SkillDisplayLimit =
                clampedLimit; // Displayed skill count will be changed after SkillDisplayLimit is set
        }

        // ⭐ 保存到配置
        _configManager.CurrentConfig.SkillDisplayLimit = clampedLimit;
        _ = _configManager.SaveAsync();
        _logger.LogDebug("技能显示数量已保存到配置: {Limit}", clampedLimit);

        // Notify that current data's SkillDisplayLimit changed
        OnPropertyChanged(nameof(CurrentStatisticData));

        _logger.LogDebug("SetSkillDisplayLimit: 技能显示条数已更新,所有slot的FilteredSkillList已刷新");
    }

    [RelayCommand]
    private void MinimizeWindow()
    {
        _windowManagement.DpsStatisticsView.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    private void NextMetricType()
    {
        StatisticIndex = StatisticIndex.Next();
    }

    [RelayCommand]
    private void PreviousMetricType()
    {
        StatisticIndex = StatisticIndex.Previous();
    }

    [RelayCommand]
    private void ToggleScopeTime()
    {
        ScopeTime = ScopeTime.Next();
    }

    protected void UpdateData()
    {
        DataStorage_DpsDataUpdated();
    }

    [RelayCommand]
    private void Refresh()
    {
        _logger.LogDebug(WpfLogEvents.VmRefresh, "Manual refresh requested");

        try
        {
            DataStorage_DpsDataUpdated();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh DPS statistics");
        }
    }


    [RelayCommand]
    private void OpenContextMenu()
    {
        ShowContextMenu = true;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManagement.SettingsView.Show();
    }

    [RelayCommand]
    private void Shutdown()
    {
        _appControlService.Shutdown();
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

    private void DurationTimerOnTick(object? sender, EventArgs e)
    {
        UpdateBattleDuration();
    }

    private void UpdateBattleDuration()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(UpdateBattleDuration);
            return;
        }

        if (_timer.IsRunning)
        {
            if (ScopeTime == ScopeTime.Current)
            {
                if (_awaitingSectionStart)
                {
                    // Freeze to last section elapsed until new data arrives
                    BattleDuration = _lastSectionElapsed;
                    return;
                }

                // ⭐ If section has timed out, freeze at captured final duration
                if (_sectionTimedOut && _lastSectionElapsed > TimeSpan.Zero)
                {
                    BattleDuration = _lastSectionElapsed;
                    return;
                }

                // Normal case: display current section duration
                var elapsed = _timer.Elapsed - _sectionStartElapsed;
                if (elapsed < TimeSpan.Zero)
                {
                    elapsed = TimeSpan.Zero;
                }

                BattleDuration = elapsed;
            }
            else // ScopeTime.Total
            {
                // ⭐ 全程模式: 显示累计战斗时长
                if (_awaitingSectionStart)
                {
                    // 脱战时: 只显示累计时长，不增长
                    BattleDuration = _totalCombatDuration;
                }
                else
                {
                    // 战斗中: 累计时长 + 当前战斗区间的进度
                    var currentSectionDuration = _timer.Elapsed - _sectionStartElapsed;
                    BattleDuration = _totalCombatDuration + currentSectionDuration;
                }
            }
        }
    }


    private void StorageOnNewSectionCreated()
    {
        _dispatcher.BeginInvoke(() =>
        {
            _logger.LogInformation("=== NewSectionCreated triggered (数据已被清空) ===");

            // ⭐ 检查是否应跳过(DataStorage_DpsDataUpdated 已保存)
            if (_skipNextSnapshotSave)
            {
                _logger.LogInformation("⏭️ 跳过快照保存(已在脱战前保存)");
                _skipNextSnapshotSave = false;
            }

            // 累加上一次战斗的时长到全程计时
            if (_timer.IsRunning)
            {
                // Use the captured _lastSectionElapsed instead of recalculating
                // This ensures we use the exact duration from when the section ended
                if (_lastSectionElapsed > TimeSpan.Zero)
                {
                    _totalCombatDuration += _lastSectionElapsed;
                    _logger.LogInformation("累加战斗时长: +{Duration:F1}s, 全程累计: {Total:F1}s",
                        _lastSectionElapsed.TotalSeconds, _totalCombatDuration.TotalSeconds);
                }
            }

            // ⭐ NOW set awaiting flag since section data has been cleared
            _awaitingSectionStart = true;
            _sectionTimedOut = false; // ⭐ Reset timeout flag since section is now cleared
            UpdateBattleDuration();

            _logger.LogInformation("NewSection完成: awaiting={AwaitingStart}, 全程时长={TotalDuration:F1}s",
                _awaitingSectionStart, _totalCombatDuration.TotalSeconds);
        });
    }

    /// <summary>
    /// 玩家信息更新事件处理
    /// </summary>
    private void StorageOnPlayerInfoUpdated(PlayerInfo? info)
    {
        if (info == null)
        {
            return;
        }

        foreach (var subViewModel in StatisticData.Values)
        {
            if (!subViewModel.DataDictionary.TryGetValue(info.UID, out var slot))
            {
                continue;
            }

            if (_dispatcher.CheckAccess())
            {
                ApplyUpdate();
            }
            else
            {
                _dispatcher.BeginInvoke((Action)ApplyUpdate);
            }

            continue;

            void ApplyUpdate()
            {
                slot.Player.Name = info.Name ?? slot.Player.Name;
                slot.Player.Class = info.ProfessionID.GetClassNameById();
                slot.Player.Spec = info.Spec;
                slot.Player.Uid = info.UID;

                if (_storage.CurrentPlayerUUID == info.UID)
                {
                    subViewModel.CurrentPlayerSlot = slot;
                }
            }
        }
    }

    /// <summary>
    /// 服务器切换事件处理
    /// </summary>
    private void StorageOnServerChanged(string currentServer, string prevServer)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(StorageOnServerChanged, currentServer, prevServer);
        }
        _logger.LogInformation("服务器切换: {Prev} -> {Current}", prevServer, currentServer);

        // 在全程模式下,服务器切换时保存全程快照
        if (ScopeTime != ScopeTime.Total || _storage.GetStatisticsCount(true) <= 0) return;
        try
        {
            // ⭐ 传递用户设置的最小时长
            SnapshotService.SaveTotalSnapshot(_storage, BattleDuration, Options.MinimalDurationInSeconds);
            _logger.LogInformation("服务器切换时保存全程快照成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务器切换时保存快照失败");
        }
    }

    partial void OnScopeTimeChanged(ScopeTime value)
    {
        _logger.LogInformation("=== ScopeTime changed: {OldValue} -> {NewValue} ===", ScopeTime, value);

        // ⭐ 关键修复: 切换模式时,强制清空所有SubViewModel的数据
        // 原因: UpdateDataOptimized 只更新存在的玩家,不会移除"不在新列表中"的玩家
        // 导致从"全程"切回"当前"时,全程才有的玩家还残留在UI中
        foreach (var subViewModel in StatisticData.Values)
        {
            subViewModel.ScopeTime = value;

            // ⭐ 清空UI数据,避免旧数据残留
            subViewModel.Data.Clear();
            subViewModel.DataDictionary.Clear();
        }

        UpdateBattleDuration();

        // ⭐ 立即用新模式的数据重新填充UI
        UpdateData();

        // Notify that CurrentPlayerSlot might have changed
        OnPropertyChanged(nameof(CurrentStatisticData));

        _logger.LogInformation("=== ScopeTime change complete ===");
    }

    partial void OnStatisticIndexChanged(StatisticType value)
    {
        _logger.LogDebug("OnStatisticIndexChanged: 切换到统计类型 {Type}", value);

        OnPropertyChanged(nameof(CurrentStatisticData));

        RefreshData();

        _logger.LogDebug("OnStatisticIndexChanged: 统计类型已切换,强制刷新完成");
    }

    private void RefreshData()
    {
        var stat = _storage.GetStatistics(ScopeTime == ScopeTime.Total);
        UpdateData(stat);
    }

    // 当ShowTeamTotalDamage改变时保存配置
    partial void OnShowTeamTotalDamageChanged(bool value)
    {
        _logger.LogDebug("ShowTeamTotalDamage changed to: {Value}", value);

        // 保存到配置
        _configManager.CurrentConfig.ShowTeamTotalDamage = value;
        _ = _configManager.SaveAsync();
        _logger.LogInformation("显示团队总伤设置已保存到配置: {Value}", value);
    }

    // 当IsIncludeNpcData改变时刷新数据
    partial void OnIsIncludeNpcDataChanged(bool value)
    {
        _logger.LogDebug($"IsIncludeNpcData changed to: {value}");

        // ⭐ 保存到配置
        _configManager.CurrentConfig.IsIncludeNpcData = value;
        _ = _configManager.SaveAsync();
        _logger.LogInformation("统计NPC设置已保存到配置: {Value}", value);

        // 如果取消勾选,清除所有StatisticData中的NPC数据
        if (!value)
        {
            _logger.LogInformation("Removing NPC data from UI (IsIncludeNpcData=false)");

            // 遍历所有统计类型的SubViewModel
            foreach (var subViewModel in StatisticData.Values)
            {
                // 找出所有NPC的slot
                var npcSlots = subViewModel.Data
                    .Where(slot => slot.Player.IsNpc)
                    .ToList(); // ToList()避免遍历时修改集合

                // 从UI中移除NPC数据
                foreach (var npcSlot in npcSlots)
                {
                    _dispatcher.Invoke(() =>
                    {
                        subViewModel.Data.Remove(npcSlot);
                        _logger.LogDebug("Removed NPC slot: UID={PlayerUid}, Name={PlayerName}", npcSlot.Player.Uid, npcSlot.Player.Name);
                    });
                }

                _logger.LogInformation($"Removed {npcSlots.Count} NPC slots from {subViewModel.GetType().Name}");
            }
        }

        // 刷新当前显示的数据
        UpdateData();
    }

    // 快照查看模式相关命令
    [RelayCommand]
    private void ViewFullSnapshot()
    {
        // 查看全程快照(合并所有分段)
        // 只在当前有战斗数据时允许
        if (_storage.GetStatisticsCount(true) == 0)
        {
            _messageDialogService.Show("查看全程快照", "当前没有可用的全程快照数据。", _windowManagement.DpsStatisticsView);
            return;
        }

        // 切换到全程模式
        _logger.LogInformation("切换到全程模式以查看快照");
        ScopeTime = ScopeTime.Total;
    }

    [RelayCommand]
    private void ViewCurrentSnapshot()
    {
        // 查看当前战斗快照
        // 只在有分段数据时允许
        if (_storage.GetStatisticsCount(false) == 0)
        {
            _messageDialogService.Show("查看战斗快照", "当前没有可用的战斗快照数据。", _windowManagement.DpsStatisticsView);
            return;
        }

        // 切换到当前模式
        _logger.LogInformation("切换到当前模式以查看战斗快照");
        ScopeTime = ScopeTime.Current;
    }

    // 加载快照命令
    [RelayCommand]
    private void LoadSnapshot(BattleSnapshotData snapshot)
    {
        if (snapshot == null)
        {
            _logger.LogWarning("尝试加载空快照");
            return;
        }

        try
        {
            _logger.LogInformation("加载快照: {Label}", snapshot.DisplayLabel);

            // ⭐ 进入快照查看模式
            EnterSnapshotViewMode(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载快照失败: {Label}", snapshot.DisplayLabel);
        }
    }

    /// <summary>
    /// 进入快照查看模式
    /// </summary>
    private void EnterSnapshotViewMode(BattleSnapshotData snapshot)
    {
        _dispatcher.Invoke(() =>
        {
            _logger.LogInformation("=== 进入快照查看模式 ===");

            // 1. 保存当前状态
            _wasPassiveMode = AppConfig.DpsUpdateMode == DpsUpdateMode.Passive;
            _wasTimerRunning = _dpsUpdateTimer?.IsEnabled ?? false;

            // 2. 停止DPS更新定时器
            if (_dpsUpdateTimer != null)
            {
                _dpsUpdateTimer.Stop();
                _logger.LogDebug("已停止DPS更新定时器");
            }

            // 停止战斗时长定时器
            if (_durationTimer != null)
            {
                _durationTimer.Stop();
                _logger.LogDebug("已停止战斗时长定时器");
            }

            // 停止主计时器(Stopwatch)
            if (_timer.IsRunning)
            {
                _timer.Stop();
                _logger.LogDebug("已停止主计时器");
            }

            // 3. 取消订阅实时数据事件
            _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
            _storage.NewSectionCreated -= StorageOnNewSectionCreated;
            _logger.LogDebug("已取消订阅实时数据事件");

            // 4. 设置快照模式标记
            IsViewingSnapshot = true;
            CurrentSnapshot = snapshot;

            // 5. 加载快照数据到UI
            LoadSnapshotDataToUI(snapshot);

            _logger.LogInformation("快照查看模式已启动: {Label}, 战斗时长: {Duration}",
                snapshot.DisplayLabel, snapshot.Duration);
        });
    }

    /// <summary>
    /// 退出快照查看模式,恢复实时统计
    /// </summary>
    [RelayCommand]
    private void ExitSnapshotViewMode()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(ExitSnapshotViewMode);
            return;
        }
        _logger.LogInformation("=== 退出快照查看模式 ===");

        // 1. 清除快照状态
        IsViewingSnapshot = false;
        CurrentSnapshot = null;

        // 2. 清空UI数据
        foreach (var subVm in StatisticData.Values)
        {
            subVm.Reset();
        }

        // 3. 恢复DPS更新机制
        if (_wasPassiveMode)
        {
            _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
            _logger.LogDebug("已恢复订阅DpsDataUpdated事件");
        }
        else if (_wasTimerRunning && _dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Start();
            _logger.LogDebug("已恢复DPS更新定时器");
        }

        // 恢复战斗时长定时器
        if (_durationTimer != null && !_durationTimer.IsEnabled)
        {
            _durationTimer.Start();
            _logger.LogDebug("已恢复战斗时长定时器");
        }

        // 恢复主计时器(如果之前在运行)
        // 注意:只有当有实时数据时才需要恢复
        var hasData = _storage.HasData();
        if (hasData && !_timer.IsRunning)
        {
            _timer.Start();
            _logger.LogDebug("已恢复主计时器");
        }

        _storage.NewSectionCreated += StorageOnNewSectionCreated;

        // 4. 刷新实时数据
        RefreshData();
        UpdateBattleDuration();

        _logger.LogInformation("已恢复实时DPS统计模式");
    }

    /// <summary>
    /// 将快照数据加载到UI显示
    /// </summary>
    private void LoadSnapshotDataToUI(BattleSnapshotData snapshot)
    {
        _logger.LogDebug("开始加载快照数据到UI...");

        // ⭐ 关键修复: 先清空所有SubViewModel的现有数据,避免实时数据残留
        foreach (var subViewModel in StatisticData.Values)
        {
            subViewModel.Data.Clear();
            subViewModel.DataDictionary.Clear();
        }

        // 1. 设置战斗时长
        BattleDuration = snapshot.Duration;

        // 2. 设置团队总伤/治疗
        TeamTotalDamage = snapshot.TeamTotalDamage;
        TeamTotalDps = snapshot.Duration.TotalSeconds > 0
            ? snapshot.TeamTotalDamage / snapshot.Duration.TotalSeconds
            : 0;

        // 3. 构建每个统计类型的数据
        var damageData = new Dictionary<long, DpsDataProcessed>();
        var healingData = new Dictionary<long, DpsDataProcessed>();
        var takenData = new Dictionary<long, DpsDataProcessed>();
        var npcTakenData = new Dictionary<long, DpsDataProcessed>();

        foreach (var (uid, playerData) in snapshot.Players)
        {
            // ⭐ 关键修复: 添加空值检查
            if (playerData == null)
            {
                _logger.LogWarning("Player data is null for UID: {Uid}", uid);
                continue;
            }

            // 构建技能列表
            var damageSkills = ConvertSnapshotSkillsToViewModel(playerData.DamageSkills, StatisticType.Damage);
            var healingSkills = ConvertSnapshotSkillsToViewModel(playerData.HealingSkills, StatisticType.Healing);
            var takenSkills = ConvertSnapshotSkillsToViewModel(playerData.TakenSkills, StatisticType.TakenDamage);

            // 解析职业
            //Enum.TryParse<Classes>(playerData.Profession, out var playerClass);

            // 伤害统计
            if (playerData.TotalDamage > 0)
            {
                // 根据IsIncludeNpcData控制NPC显示
                var shouldShow = !playerData.IsNpc || IsIncludeNpcData;
                if (shouldShow)
                {
                    // ⭐ 使用默认的DpsData而不是null
                    var dummyDpsData = new PlayerStatistics(uid);
                    damageData[uid] = new DpsDataProcessed(
                        dummyDpsData,
                        playerData.TotalDamage,
                        0, damageSkills, healingSkills, takenSkills,
                        playerData.Uid
                    );
                }
            }

            // 治疗统计(不含NPC)
            if (playerData.TotalHealing > 0 && !playerData.IsNpc)
            {
                var dummyDpsData = new PlayerStatistics(uid);
                healingData[uid] = new DpsDataProcessed(
                    dummyDpsData,
                    playerData.TotalHealing,
                    0, damageSkills, healingSkills, takenSkills,
                    playerData.Uid
                );
            }

            // 承伤统计
            if (playerData.TakenDamage > 0)
            {
                if (playerData.IsNpc)
                {
                    // NPC承伤
                    var dummyDpsData = new PlayerStatistics(uid);
                    npcTakenData[uid] = new DpsDataProcessed(
                        dummyDpsData,
                        playerData.TakenDamage,
                        0, damageSkills, healingSkills, takenSkills,
                        playerData.Uid
                    );
                }
                else // 玩家承伤
                {
                    var dummyDpsData = new PlayerStatistics(uid);
                    takenData[uid] = new DpsDataProcessed(
                        dummyDpsData,
                        playerData.TakenDamage,
                        0, damageSkills, healingSkills, takenSkills,
                        playerData.Uid
                    );
                }
            }
        }

        // 4. 更新各个统计类型的UI
        StatisticData[StatisticType.Damage].UpdateDataOptimized(damageData, 0);
        StatisticData[StatisticType.Healing].UpdateDataOptimized(healingData, 0);
        StatisticData[StatisticType.TakenDamage].UpdateDataOptimized(takenData, 0);
        StatisticData[StatisticType.NpcTakenDamage].UpdateDataOptimized(npcTakenData, 0);
    }

    /// <summary>
    /// 将快照技能数据转换为ViewModel
    /// </summary>
    private List<SkillItemViewModel> ConvertSnapshotSkillsToViewModel(List<SnapshotSkillData> snapshotSkills,
        StatisticType statisticType)
    {
        if (snapshotSkills.Count == 0)
            return new List<SkillItemViewModel>();

        return snapshotSkills.Select(s =>
        {
            var average = s.UseTimes > 0 ? Math.Round(s.TotalValue / (double)s.UseTimes) : 0d;
            var avgValue = average > int.MaxValue ? int.MaxValue : (int)average;
            var critRate = s.UseTimes > 0 ? (double)s.CritTimes / s.UseTimes : 0d;

            var value = new SkillItemViewModel.SkillValue
            {
                TotalValue = (long)s.TotalValue,
                HitCount = s.UseTimes,
                CritCount = s.CritTimes,
                LuckyCount = s.LuckyTimes,
                Average = avgValue,
                CritRate = critRate
            };

            var vm = new SkillItemViewModel
            {
                SkillId = s.SkillId,
                SkillName = s.SkillName
            };

            switch (statisticType)
            {
                case StatisticType.Healing:
                    vm.Heal = value;
                    break;
                case StatisticType.TakenDamage:
                case StatisticType.NpcTakenDamage:
                    vm.TakenDamage = value;
                    break;
                default:
                    vm.Damage = value;
                    break;
            }

            return vm;
        }).ToList();
    }

    partial void OnAppConfigChanging(AppConfig? oldValue, AppConfig newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= AppConfigOnPropertyChanged;
        newValue.PropertyChanged += AppConfigOnPropertyChanged;
        ApplyMaskToPlayers(newValue.MaskPlayerName);
        ApplyPlayerInfoFormatToPlayers(newValue.PlayerInfoFormatString);
        ApplyPlayerInfoFormatSwitchToPlayers(newValue.UseCustomFormat);
    }

    private void AppConfigOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppConfig.MaskPlayerName))
        {
            ApplyMaskToPlayers(AppConfig.MaskPlayerName);
        }

        if (e.PropertyName == nameof(AppConfig.PlayerInfoFormatString))
        {
            ApplyPlayerInfoFormatToPlayers(AppConfig.PlayerInfoFormatString);
        }

        if (e.PropertyName == nameof(AppConfig.UseCustomFormat))
        {
            ApplyPlayerInfoFormatSwitchToPlayers(AppConfig.UseCustomFormat);
        }

        // ⭐ 新增: 监听主题颜色和背景图片的变化
        if (e.PropertyName == nameof(AppConfig.ThemeColor))
        {
            OnPropertyChanged(nameof(ThemeColor));
        }

        if (e.PropertyName == nameof(AppConfig.BackgroundImagePath))
        {
            OnPropertyChanged(nameof(BackgroundImagePath));
        }
    }

    private void ApplyMaskToPlayers(bool mask)
    {
        InvokeOnDispatcher(() =>
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SetPlayerInfoMask(mask);
            }
        });
    }

    private void ApplyPlayerInfoFormatSwitchToPlayers(bool valueUseCustomFormat)
    {
        InvokeOnDispatcher(() =>
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SetUsePlayerInfoFormat(valueUseCustomFormat);
            }
        });
    }

    private void ApplyPlayerInfoFormatToPlayers(string formatString)
    {
        InvokeOnDispatcher(() =>
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SetPlayerInfoFormat(formatString);
            }
        });
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