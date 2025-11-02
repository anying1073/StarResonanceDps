using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Exceptions;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Logging;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DpsStatisticsOptions : BaseViewModel
{
    [ObservableProperty] private int _minimalDurationInSeconds;

    [RelayCommand]
    private void SetMinimalDuration(int duration)
    {
        MinimalDurationInSeconds = duration;
    }
}

public partial class DpsStatisticsViewModel : BaseViewModel, IDisposable
{
    private readonly IApplicationControlService _appControlService;
    private readonly IConfigManager _configManager;
    private readonly Dispatcher _dispatcher;
    // Use a single stopwatch for both total and section durations
    private readonly Stopwatch _timer = new();
    // Snapshot of elapsed time at the moment a new section starts
    private TimeSpan _sectionStartElapsed = TimeSpan.Zero;
    // Whether we are waiting for the first datapoint of a new section
    private bool _awaitingSectionStart;
    // Captured elapsed of the last section to freeze UI until new data arrives
    private TimeSpan _lastSectionElapsed = TimeSpan.Zero;
    private readonly ILogger<DpsStatisticsViewModel> _logger;
    private readonly IDataStorage _storage;
    private readonly IWindowManagementService _windowManagement;
    private readonly ITopmostService _topmostService;
    private DispatcherTimer? _durationTimer;

    // Timer for active update mode
    private DispatcherTimer? _dpsUpdateTimer;

    private bool _isInitialized;
    [ObservableProperty] private ScopeTime _scopeTime = ScopeTime.Current;
    [ObservableProperty] private bool _showContextMenu;
    [ObservableProperty] private SortDirectionEnum _sortDirection = SortDirectionEnum.Descending;
    [ObservableProperty] private string _sortMemberPath = "Value";
    [ObservableProperty] private StatisticType _statisticIndex;
    [ObservableProperty] private AppConfig _appConfig;
    [ObservableProperty] private TimeSpan _battleDuration;

    /// <inheritdoc/>
    public DpsStatisticsViewModel(IApplicationControlService appControlService,
        IDataStorage storage,
        ILogger<DpsStatisticsViewModel> logger,
        IConfigManager configManager,
        IWindowManagementService windowManagement,
        ITopmostService topmostService,
        DebugFunctions debugFunctions,
        Dispatcher dispatcher)
    {
        StatisticData = new Dictionary<StatisticType, DpsStatisticsSubViewModel>
        {
            {
                StatisticType.Damage,
                new DpsStatisticsSubViewModel(logger, dispatcher, StatisticType.Damage, storage, debugFunctions)
            },
            {
                StatisticType.TakenDamage,
                new DpsStatisticsSubViewModel(logger, dispatcher, StatisticType.TakenDamage, storage, debugFunctions)
            },
            {
                StatisticType.Healing,
                new DpsStatisticsSubViewModel(logger, dispatcher,StatisticType.Healing, storage, debugFunctions)
            },
            {
                StatisticType.NpcTakenDamage,
                new DpsStatisticsSubViewModel(logger, dispatcher,StatisticType.TakenDamage, storage, debugFunctions)
            }
        };
        _configManager = configManager;
        _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;
        _appConfig = _configManager.CurrentConfig;

        DebugFunctions = debugFunctions;
        _appControlService = appControlService;
        _storage = storage;
        _logger = logger;
        _windowManagement = windowManagement;
        _topmostService = topmostService;
        _dispatcher = dispatcher;

        // Subscribe to DebugFunctions events to handle sample data requests
        DebugFunctions.SampleDataRequested += OnSampleDataRequested;
        _storage.PlayerInfoUpdated += StorageOnPlayerInfoUpdated;

        // set config
    }

    public Dictionary<StatisticType, DpsStatisticsSubViewModel> StatisticData { get; }

    public DpsStatisticsSubViewModel CurrentStatisticData => StatisticData[StatisticIndex];

    public DebugFunctions DebugFunctions { get; }

    public DpsStatisticsOptions Options { get; } = new();

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

        _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
        _storage.NewSectionCreated -= StorageOnNewSectionCreated;
        _storage.PlayerInfoUpdated -= StorageOnPlayerInfoUpdated;
        _storage.Dispose();

        foreach (var dpsStatisticsSubViewModel in StatisticData.Values)
        {
            dpsStatisticsSubViewModel.Initialized = false;
        }

        _isInitialized = false;
    }

    private void ConfigManagerOnConfigurationUpdated(object? sender, AppConfig newConfig)
    {
        if (_dispatcher.CheckAccess())
        {
            var oldMode = AppConfig.DpsUpdateMode;
            var oldInterval = AppConfig.DpsUpdateInterval;

            AppConfig = newConfig;

            // If update mode or interval changed, reconfigure update mechanism
            if (oldMode != newConfig.DpsUpdateMode || oldInterval != newConfig.DpsUpdateInterval)
            {
                ConfigureDpsUpdateMode();
        }
        }
        else
        {
            _dispatcher.Invoke(() =>
            {
                var oldMode = AppConfig.DpsUpdateMode;
                var oldInterval = AppConfig.DpsUpdateInterval;

                AppConfig = newConfig;

                if (oldMode != newConfig.DpsUpdateMode || oldInterval != newConfig.DpsUpdateInterval)
                {
                    ConfigureDpsUpdateMode();
        }
            });
    }
    }

    private void OnSampleDataRequested(object? sender, EventArgs e)
    {
        // Handle the event from DebugFunctions
        AddRandomData();
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
        catch(InvalidOperationException ex)
        {
            // Ignore
            _logger.LogError(ex, "Failed to save AppConfig");
        }
    }

    [RelayCommand]
    public void ResetAll()
    {
        _storage.ClearAllDpsData();
        _timer.Reset();
        _sectionStartElapsed = TimeSpan.Zero;
        _awaitingSectionStart = false;
        _lastSectionElapsed = TimeSpan.Zero;

        // Clear current UI data for all statistic types and rebuild from the new section snapshot
        foreach (var subVm in StatisticData.Values)
        {
            subVm.Reset();
        }
    }

    [RelayCommand]
    public void ResetSection()
    {
        _storage.ClearDpsData();
        // Move section start to current elapsed so section duration becomes zero
        _sectionStartElapsed = _timer.Elapsed;
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
        if (!_isInitialized) return;

        _logger.LogInformation("Configuring DPS update mode: {Mode}, Interval: {Interval}ms",
            AppConfig.DpsUpdateMode, AppConfig.DpsUpdateInterval);

        switch (AppConfig.DpsUpdateMode)
        {
            case DpsUpdateMode.Passive:
                // Passive mode: subscribe to event
                StopDpsUpdateTimer();
                _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated; // Unsubscribe first to avoid duplicate
                _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
                _storage.NewSectionCreated -= StorageOnNewSectionCreated;
                _storage.NewSectionCreated += StorageOnNewSectionCreated;
                _logger.LogDebug("Passive mode enabled: listening to DpsDataUpdated event");
                break;

            case DpsUpdateMode.Active:
                // Active mode: use timer, unsubscribe from event
                _storage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
                _storage.NewSectionCreated -= StorageOnNewSectionCreated;
                StartDpsUpdateTimer(AppConfig.DpsUpdateInterval);
                _logger.LogDebug("Active mode enabled: timer interval {Interval}ms", AppConfig.DpsUpdateInterval);
                break;

            default:
                _logger.LogWarning("Unknown DPS update mode: {Mode}", AppConfig.DpsUpdateMode);
                break;
        }
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
        }
        else
        {
            _dpsUpdateTimer.Stop();
            _dpsUpdateTimer.Interval = TimeSpan.FromMilliseconds(clampedInterval);
        }

        _dpsUpdateTimer.Start();
        _logger.LogDebug("DPS update timer started with interval {Interval}ms", clampedInterval);
    }

    /// <summary>
    /// Stop DPS update timer
    /// </summary>
    private void StopDpsUpdateTimer()
    {
        if (_dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Stop();
            _logger.LogDebug("DPS update timer stopped");
        }
    }

    /// <summary>
    /// Timer tick handler for active update mode
    /// </summary>
    private void DpsUpdateTimerOnTick(object? sender, EventArgs e)
    {
        // Call the same update logic as event-based mode
        DataStorage_DpsDataUpdated();
    }

    private void DataStorage_DpsDataUpdated()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(DataStorage_DpsDataUpdated);
            return;
        }

        var dpsList = ScopeTime == ScopeTime.Total
            ? _storage.ReadOnlyFullDpsDataList
            : _storage.ReadOnlySectionedDpsDataList;

        // Only start the timer when there is actual damage data present
        if (!_timer.IsRunning && HasDamageData(dpsList))
        {
            _timer.Start();
        }
        // else if (_timer.IsRunning && !HasDamageData(dpsList))
        // {
        //     // Stop recording if timer was running but no more damage data
        //     // (IsRecordingActive removed - not part of interface)
        // }

        // If a new section was created, wait until first datapoint to reset UI and mark section start
        var hasSectionDamage = HasDamageData(_storage.ReadOnlySectionedDpsDataList);
        if (_awaitingSectionStart && hasSectionDamage)
        {
            foreach (var subVm in StatisticData.Values)
            {
                subVm.Reset();
            }
            _sectionStartElapsed = _timer.Elapsed;
            _lastSectionElapsed = TimeSpan.Zero;
            _awaitingSectionStart = false;
        }

        UpdateData(dpsList);
        UpdateBattleDuration();
    }

    private static bool HasDamageData(IReadOnlyList<DpsData> data)
    {
        return data.Any(t => t.TotalAttackDamage > 0);
    }

    private void UpdateData(IReadOnlyList<DpsData> data)
    {
        _logger.LogTrace(WpfLogEvents.VmUpdateData, "Update data requested: {Count} entries", data.Count);

        var currentPlayerUid = _storage.CurrentPlayerInfo.UID;

        // Pre-process data once for all statistic types
        var processedDataByType = PreProcessDataForAllTypes(data);

        // Update each subViewModel with its pre-processed data
        foreach (var (statisticType, processedData) in processedDataByType)
        {
            if (!StatisticData.TryGetValue(statisticType, out var subViewModel)) continue;
            subViewModel.ScopeTime = ScopeTime;
            subViewModel.UpdateDataOptimized(processedData, currentPlayerUid);
        }
    }

    /// <summary>
    /// Pre-processes data once for all statistic types to avoid redundant iterations
    /// </summary>
    private Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> PreProcessDataForAllTypes(
        IReadOnlyList<DpsData> data)
    {
        var result = new Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>>
        {
            [StatisticType.Damage] = new(),
            [StatisticType.Healing] = new(),
            [StatisticType.TakenDamage] = new(),
            [StatisticType.NpcTakenDamage] = new()
        };

        // Single pass through the data
        foreach (var dpsData in data)
        {
            // Calculate common values once
            var duration = (dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0)).ConvertToUnsigned();
            var skillList = BuildSkillListSnapshot(dpsData);

            // Get player info once
            string playerName;
            Classes playerClass;
            ClassSpec playerSpec;
            int powerLevel = 0;


            if (_storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.UID, out var playerInfo))
            {
                playerName = playerInfo.Name ?? $"UID: {dpsData.UID}";
                playerClass = playerInfo.ProfessionID.GetClassNameById();
                playerSpec = playerInfo.Spec;
                powerLevel = playerInfo.CombatPower ?? 0;
            }
            else
            {
                playerName = $"UID: {dpsData.UID}";
                playerClass = Classes.Unknown;
                playerSpec = ClassSpec.Unknown;
            }

            // Process Damage
            var damageValue = dpsData.TotalAttackDamage.ConvertToUnsigned();
            if (damageValue > 0)
            {
                result[StatisticType.Damage][dpsData.UID] = new DpsDataProcessed(
                    dpsData, damageValue, duration, skillList, playerName, playerClass, playerSpec,
                    powerLevel);
            }

            // Process Healing
            var healingValue = dpsData.TotalHeal.ConvertToUnsigned();
            if (healingValue > 0)
            {
                result[StatisticType.Healing][dpsData.UID] = new DpsDataProcessed(
                    dpsData, healingValue, duration, skillList, playerName, playerClass, playerSpec, powerLevel);
            }

            // Process TakenDamage
            var takenDamageValue = dpsData.TotalTakenDamage.ConvertToUnsigned();
            if (takenDamageValue > 0)
            {
                // Process NpcTakenDamage (only for NPCs)
                if (dpsData.IsNpcData)
                {
                    result[StatisticType.NpcTakenDamage][dpsData.UID] = new DpsDataProcessed(
                        dpsData, takenDamageValue, duration, skillList, playerName, playerClass, playerSpec, powerLevel);
                }
                else
                {
                    result[StatisticType.TakenDamage][dpsData.UID] = new DpsDataProcessed(
                        dpsData, takenDamageValue, duration, skillList, playerName, playerClass, playerSpec, powerLevel);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds skill list snapshot
    /// </summary>
    private List<SkillItemViewModel> BuildSkillListSnapshot(DpsData dpsData)
    {
        var skills = dpsData.ReadOnlySkillDataList;
        if (skills.Count == 0)
        {
            return [];
        }

        var orderedSkills = skills.OrderByDescending(static s => s.TotalValue);

        var skillDisplayLimit = CurrentStatisticData?.SkillDisplayLimit ?? 8;

        var projected = orderedSkills.Select(skill =>
        {
            var average = skill.UseTimes > 0
                ? Math.Round(skill.TotalValue / (double)skill.UseTimes)
                : 0d;

            var avgDamage = average > int.MaxValue
                ? int.MaxValue
                : (int)average;

            var skillIdText = skill.SkillId.ToString();
            var skillName = EmbeddedSkillConfig.TryGet(skillIdText, out var definition)
                ? definition.Name
                : skillIdText;

            return new SkillItemViewModel
            {
                SkillName = skillName,
                TotalDamage = skill.TotalValue,
                HitCount = skill.UseTimes,
                CritCount = skill.CritTimes,
                AvgDamage = avgDamage
            };
        });

        return skillDisplayLimit > 0
            ? projected.Take(skillDisplayLimit).ToList()
            : projected.ToList();
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
        foreach (var vm in StatisticData.Values)
        {
            vm.SkillDisplayLimit = clampedLimit;
        }

        // Notify that current data's SkillDisplayLimit changed
        OnPropertyChanged(nameof(CurrentStatisticData));
    }

    protected void AddTestItem()
    {
        CurrentStatisticData.AddTestItem();
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

        // Reload cached player details so that recent changes in the on-disk
        // cache are reflected in the UI.
        LoadPlayerCache();

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
            if (ScopeTime == ScopeTime.Current && _awaitingSectionStart)
            {
                // Freeze to last section elapsed until new data arrives
                BattleDuration = _lastSectionElapsed;
                return;
            }

            var elapsed = ScopeTime == ScopeTime.Total
                ? _timer.Elapsed
                : _timer.Elapsed - _sectionStartElapsed;

            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            BattleDuration = elapsed;
        }
    }

    private void StorageOnNewSectionCreated()
    {
        _dispatcher.BeginInvoke(() =>
        {
            // Freeze current section duration and await first datapoint of the new section
            _lastSectionElapsed = _timer.IsRunning ? (_timer.Elapsed - _sectionStartElapsed) : TimeSpan.Zero;
            _awaitingSectionStart = true;
            UpdateBattleDuration();

            // Do NOT clear current UI data here; wait until data for the new section arrives
        });
    }

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

                if (_storage.CurrentPlayerInfo.UID == info.UID)
                {
                    subViewModel.CurrentPlayerSlot = slot;
                }
            }
        }
    }

    partial void OnScopeTimeChanged(ScopeTime value)
    {
        foreach (var subViewModel in StatisticData.Values)
        {
            subViewModel.ScopeTime = value;
        }

        UpdateBattleDuration();
        UpdateData();

        // Notify that CurrentPlayerSlot might have changed
        OnPropertyChanged(nameof(CurrentStatisticData));
    }

    partial void OnStatisticIndexChanged(StatisticType value)
    {
        OnPropertyChanged(nameof(CurrentStatisticData));
        UpdateData();
    }
}