using System.Diagnostics;
using System.IO;
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
    private readonly Stopwatch _battleTimer = new();
    private readonly IConfigManager _configManager;
    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _fullBattleTimer = new();
    private readonly ILogger<DpsStatisticsViewModel> _logger;
    private readonly IDataStorage _storage;
    private readonly IWindowManagementService _windowManagement;
    private DispatcherTimer? _durationTimer;
    private bool _isInitialized;
    [ObservableProperty] private NumberDisplayMode _numberDisplayMode = NumberDisplayMode.Wan;
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
        DebugFunctions = debugFunctions;
        _appControlService = appControlService;
        _storage = storage;
        _logger = logger;
        _configManager = configManager;
        _windowManagement = windowManagement;
        _dispatcher = dispatcher;

        // Subscribe to DebugFunctions events to handle sample data requests
        DebugFunctions.SampleDataRequested += OnSampleDataRequested;
        _storage.PlayerInfoUpdated += StorageOnPlayerInfoUpdated;

        // set config
        _appConfig = _configManager.CurrentConfig;
        _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;
    }

    public Dictionary<StatisticType, DpsStatisticsSubViewModel> StatisticData { get; }

    public DpsStatisticsSubViewModel CurrentStatisticData => StatisticData[StatisticIndex];

    public DebugFunctions DebugFunctions { get; }

    public DpsStatisticsOptions Options { get; } = new();
    private Stopwatch InUsingTimer => ScopeTime == ScopeTime.Total ? _fullBattleTimer : _battleTimer;

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
            AppConfig = newConfig;
        }
        else
        {
            _dispatcher.Invoke(() => AppConfig = newConfig);
        }
    }

    private void OnSampleDataRequested(object? sender, EventArgs e)
    {
        // Handle the event from DebugFunctions
        AddRandomData();
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

        _logger.LogDebug("VM Loaded");
        LoadPlayerCache();

        EnsureDurationTimerStarted();
        UpdateBattleDuration();

        // 开始监听DPS更新事件
        _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
        _storage.NewSectionCreated += StorageOnNewSectionCreated;
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

    private void DataStorage_DpsDataUpdated()
    {
        if (!_fullBattleTimer.IsRunning)
        {
            _fullBattleTimer.Restart();
        }

        if (!_battleTimer.IsRunning)
        {
            _battleTimer.Restart();
        }

        var dpsList = ScopeTime == ScopeTime.Total
            ? _storage.ReadOnlyFullDpsDataList
            : _storage.ReadOnlySectionedDpsDataList;
        UpdateData(dpsList);
    }

    private void UpdateData(IReadOnlyList<DpsData> data)
    {
        _logger.LogInformation("Update data");

        var currentPlayerUid = _storage.CurrentPlayerInfo.UID;

        // Pre-process data once for all statistic types
        var processedDataByType = PreProcessDataForAllTypes(data);

        // Update each subViewModel with its pre-processed data
        foreach (var (statisticType, processedData) in processedDataByType)
        {
            if (StatisticData.TryGetValue(statisticType, out var subViewModel))
            {
                subViewModel.ScopeTime = ScopeTime;
                subViewModel.UpdateDataOptimized(processedData, currentPlayerUid);
            }
        }

        UpdateBattleDuration();
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

            if (_storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.UID, out var playerInfo))
            {
                playerName = playerInfo.Name ?? $"UID: {dpsData.UID}";
                playerClass = playerInfo.ProfessionID.GetClassNameById();
                playerSpec = playerInfo.Spec;
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
                    dpsData, damageValue, duration, skillList, playerName, playerClass, playerSpec);
            }

            // Process Healing
            var healingValue = dpsData.TotalHeal.ConvertToUnsigned();
            if (healingValue > 0)
            {
                result[StatisticType.Healing][dpsData.UID] = new DpsDataProcessed(
                    dpsData, healingValue, duration, skillList, playerName, playerClass, playerSpec);
            }

            // Process TakenDamage
            var takenDamageValue = dpsData.TotalTakenDamage.ConvertToUnsigned();
            if (takenDamageValue > 0)
            {
                // Process NpcTakenDamage (only for NPCs)
                if (dpsData.IsNpcData)
                {
                    result[StatisticType.NpcTakenDamage][dpsData.UID] = new DpsDataProcessed(
                        dpsData, takenDamageValue, duration, skillList, playerName, playerClass, playerSpec);
                }
                else
                {
                    result[StatisticType.TakenDamage][dpsData.UID] = new DpsDataProcessed(
                        dpsData, takenDamageValue, duration, skillList, playerName, playerClass, playerSpec);
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
    public void AddTestItem()
    {
        CurrentStatisticData.AddTestItem();
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
        _logger.LogDebug("Manual refresh requested");

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

        var elapsed = InUsingTimer.Elapsed;
        BattleDuration = elapsed;
    }

    private void StorageOnNewSectionCreated()
    {
        _dispatcher.BeginInvoke(() =>
        {
            _battleTimer.Reset();
            UpdateBattleDuration();
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