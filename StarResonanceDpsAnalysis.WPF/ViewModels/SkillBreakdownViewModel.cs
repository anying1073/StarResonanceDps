using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Properties;
using System.Collections.ObjectModel;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using System.Windows.Threading;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for the skill breakdown view, showing detailed statistics for a player.
/// </summary>
public partial class SkillBreakdownViewModel : BaseViewModel, IDisposable
{
    private readonly ILogger<SkillBreakdownViewModel> _logger;
    private readonly LocalizationManager _localizationManager;
    private readonly IDataStorage _storage;
    [ObservableProperty] private StatisticType _statisticIndex;
    private PlayerStatistics? _playerStatistics;
    
    // NEW: App configuration for number formatting
    [ObservableProperty] private Config.AppConfig _appConfig;
    
    // ? 新增：实时更新定时器
    private DispatcherTimer? _updateTimer;
    private const int UpdateIntervalMs = 1000; // 每秒更新一次

    // NEW: Tab ViewModels for modular components
    [ObservableProperty] private TabContentViewModel _dpsTabViewModel;
    [ObservableProperty] private TabContentViewModel _healingTabViewModel;
    [ObservableProperty] private TabContentViewModel _tankingTabViewModel;

    /// <summary>
    /// ViewModel for the skill breakdown view, showing detailed statistics for a player.
    /// </summary>
    public SkillBreakdownViewModel(
        ILogger<SkillBreakdownViewModel> logger, 
        LocalizationManager localizationManager,
        IDataStorage storage,
        Config.IConfigManager configManager)
    {
        _logger = logger;
        _localizationManager = localizationManager;
        _storage = storage;
        _appConfig = configManager.CurrentConfig;

        var xAxis = GetXAxisName();
        _dpsTabViewModel = new TabContentViewModel(CreatePlotViewModel(xAxis, StatisticType.Damage));
        _healingTabViewModel = new TabContentViewModel(CreatePlotViewModel(xAxis, StatisticType.Healing));
        _tankingTabViewModel = new TabContentViewModel(CreatePlotViewModel(xAxis, StatisticType.TakenDamage));
        
        // ? 初始化更新定时器
        InitializeUpdateTimer();
    }

    /// <summary>
    /// ? 初始化实时更新定时器
    /// </summary>
    private void InitializeUpdateTimer()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(UpdateIntervalMs)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
    }

    /// <summary>
    /// ? 定时器回调：刷新统计数据
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_playerStatistics == null || ObservedSlot?.Player.Uid == null)
        {
            return;
        }

        try
        {
            // ? 从存储中获取最新的PlayerStatistics
            var latestStats = _storage.GetStatistics(fullSession: false);
            if (latestStats.TryGetValue(ObservedSlot.Player.Uid, out var updated))
            {
                _playerStatistics = updated;
                RefreshAllStatistics();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating skill breakdown data");
        }
    }

    /// <summary>
    /// ? 启动实时更新
    /// </summary>
    public void StartRealTimeUpdate()
    {
        _updateTimer?.Start();
        _logger.LogDebug("Started real-time update for SkillBreakdownView");
    }

    /// <summary>
    /// ? 停止实时更新
    /// </summary>
    public void StopRealTimeUpdate()
    {
        _updateTimer?.Stop();
        _logger.LogDebug("Stopped real-time update for SkillBreakdownView");
    }

    /// <summary>
    /// Initialize from PlayerStatistics directly 
    /// </summary>
    public void InitializeFrom(
        PlayerStatistics playerStats,
        Core.Data.Models.PlayerInfo? playerInfo,
        StatisticType statisticType,
        StatisticDataViewModel slot)
    {
        _logger.LogDebug("Initializing SkillBreakdownViewModel from PlayerStatistics for UID {Uid}",
            playerStats.Uid);

        _playerStatistics = playerStats;
        ObservedSlot = slot;

        // Update player info
        UpdatePlayerInfo(playerStats, playerInfo);
        StatisticIndex = statisticType;

        // Update all statistics
        RefreshAllStatistics();
        
        // ? 启动实时更新
        StartRealTimeUpdate();

        _logger.LogDebug("SkillBreakdownViewModel initialized from PlayerStatistics: {Name}", PlayerName);
    }

    #region Observed Slot (Data Source)

    [ObservableProperty] private StatisticDataViewModel? _observedSlot;

    #endregion

    #region Player Info Properties

    [ObservableProperty] private string _playerName = string.Empty;
    [ObservableProperty] private long _uid;
    [ObservableProperty] private long _powerLevel;

    #endregion

    #region Zoom State

    [ObservableProperty] private double _zoomLevel = 1.0;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 0.2;

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Create a PlotViewModel with localized options
    /// </summary>
    private PlotViewModel CreatePlotViewModel(string xAxisTitle, StatisticType statisticType)
    {
        return new PlotViewModel(new PlotOptions
        {
            XAxisTitle = xAxisTitle,
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = statisticType,
        });
    }

    /// <summary>
    /// Update player basic information
    /// </summary>
    private void UpdatePlayerInfo(PlayerStatistics playerStats, Core.Data.Models.PlayerInfo? playerInfo)
    {
        PlayerName = playerInfo?.Name ?? $"UID: {playerStats.Uid}";
        Uid = playerStats.Uid;
        PowerLevel = playerInfo?.CombatPower ?? 0;
    }

    /// <summary>
    /// Refresh all statistics from PlayerStatistics (Single Responsibility)
    /// </summary>
    private void RefreshAllStatistics()
    {
        if (_playerStatistics == null)
        {
            _logger.LogWarning("Cannot refresh statistics: PlayerStatistics is null");
            return;
        }

        var duration = TimeSpan.FromTicks(_playerStatistics.LastTick - (_playerStatistics.StartTick ?? 0));
        var skillLists = _playerStatistics.ToSkillItemVmList();

        // Update damage statistics 
        UpdateStatisticSet(DpsTabViewModel,
            _playerStatistics.AttackDamage, skillLists.Damage, duration, _playerStatistics.GetDeltaDpsSamples());

        // Update healing statistics 
        UpdateStatisticSet(HealingTabViewModel,
            _playerStatistics.Healing, skillLists.Healing, duration, _playerStatistics.GetDeltaHpsSamples());

        // Update taken damage statistics 
        UpdateStatisticSet(TankingTabViewModel,
            _playerStatistics.TakenDamage, skillLists.Taken, duration, _playerStatistics.GetDeltaDtpsSamples());
    }

    /// <summary>
    /// Update a single statistic set with all its associated data (Open/Closed Principle)
    /// </summary>
    private void UpdateStatisticSet(
        TabContentViewModel tabViewModel,
        StatisticValues statisticValues,
        List<SkillItemViewModel> skills,
        TimeSpan duration,
        IReadOnlyList<DpsDataPoint> timeSeries)
    {
        // Convert and set statistics
        var stats = statisticValues.ToDataStatistics(duration);
        tabViewModel.Stats = stats;
        
        PopulateSkills(tabViewModel.SkillList.SkillItems, skills);

        // Update charts
        UpdateChartsForStatistic(skills, timeSeries, stats, tabViewModel.Plot);
    }

    /// <summary>
    /// Populate skills collection efficiently
    /// </summary>
    private void PopulateSkills(ObservableCollection<SkillItemViewModel> target, List<SkillItemViewModel> source)
    {
        target.Clear();
        foreach (var skill in source)
        {
            target.Add(skill);
        }
    }

    /// <summary>
    /// Update all charts for a single statistic type (Single Responsibility)
    /// </summary>
    private static void UpdateChartsForStatistic(
        List<SkillItemViewModel> skills,
        IReadOnlyList<DpsDataPoint> timeSeries,
        DataStatisticsViewModel stats,
        PlotViewModel plot)
    {
        // Time series
        UpdateTimeSeriesChart(timeSeries, plot);

        // Pie chart
        plot.SetPieSeriesData(skills);

        // Hit type distribution
        UpdateHitTypeDistribution(stats, plot);
    }

    /// <summary>
    /// Update time series chart from Core layer samples
    /// </summary>
    private static void UpdateTimeSeriesChart(IReadOnlyList<DpsDataPoint> samples, PlotViewModel target)
    {
        target.LineSeriesData.Points.Clear();
        foreach (var sample in samples)
        {
            target.LineSeriesData.Points.Add(new DataPoint(sample.Time.TotalSeconds, sample.Value));
        }
        target.RefreshSeries();
    }

    /// <summary>
    /// Update hit type distribution for a statistic
    /// </summary>
    private static void UpdateHitTypeDistribution(DataStatisticsViewModel stat, PlotViewModel target)
    {
        if (stat.Hits <= 0) return;

        var crit = (double)stat.CritCount / stat.Hits * 100;
        var lucky = (double)stat.LuckyCount / stat.Hits * 100;
        var normal = 100 - crit - lucky;

        target.SetHitTypeDistribution(normal, crit, lucky);
    }

    /// <summary>
    /// Update plot options with current localization
    /// </summary>
    private void UpdatePlotOption()
    {
        var xAxis = GetXAxisName();

        UpdateSinglePlotOption(DpsTabViewModel.Plot, xAxis, StatisticType.Damage,
            ResourcesKeys.SkillBreakdown_Chart_RealTimeDps,
            ResourcesKeys.SkillBreakdown_Chart_HitTypeDistribution);

        UpdateSinglePlotOption(HealingTabViewModel.Plot, xAxis, StatisticType.Healing,
            ResourcesKeys.SkillBreakdown_Chart_RealTimeHps,
            ResourcesKeys.SkillBreakdown_Chart_HealTypeDistribution);

        UpdateSinglePlotOption(TankingTabViewModel.Plot, xAxis, StatisticType.TakenDamage,
            ResourcesKeys.SkillBreakdown_Chart_RealTimeDtps,
            ResourcesKeys.SkillBreakdown_Chart_HitTypeDistribution);
    }

    /// <summary>
    /// Update options for a single plot
    /// </summary>
    private void UpdateSinglePlotOption(
        PlotViewModel plot,
        string xAxisTitle,
        StatisticType statisticType,
        string seriesTitleKey,
        string distributionTitleKey)
    {
        plot.UpdateOption(new PlotOptions
        {
            SeriesPlotTitle = _localizationManager.GetString(seriesTitleKey),
            XAxisTitle = xAxisTitle,
            DistributionPlotTitle = _localizationManager.GetString(distributionTitleKey),
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = statisticType
        });
    }

    private string GetXAxisName()
    {
        return _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_DpsSeriesXAxis);
    }

    #endregion

    #region Zoom Commands

    [RelayCommand]
    private void ZoomIn()
    {
        if (ZoomLevel >= MaxZoom) return;
        ZoomLevel += ZoomStep;
        ApplyZoomToAllCharts();
        _logger.LogDebug("Zoomed in to {ZoomLevel}", ZoomLevel);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (ZoomLevel <= MinZoom) return;
        ZoomLevel -= ZoomStep;
        ApplyZoomToAllCharts();
        _logger.LogDebug("Zoomed out to {ZoomLevel}", ZoomLevel);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        ResetAllChartZooms();
        _logger.LogDebug("Zoom reset to default");
    }

    private void ApplyZoomToAllCharts()
    {
        DpsTabViewModel.Plot.ApplyZoomToModel(ZoomLevel);
        HealingTabViewModel.Plot.ApplyZoomToModel(ZoomLevel);
        TankingTabViewModel.Plot.ApplyZoomToModel(ZoomLevel);
    }

    private void ResetAllChartZooms()
    {
        DpsTabViewModel.Plot.ResetModelZoom();
        HealingTabViewModel.Plot.ResetModelZoom();
        TankingTabViewModel.Plot.ResetModelZoom();
    }

    #endregion

    #region Command Handlers

    [RelayCommand]
    private void Confirm()
    {
        _logger.LogDebug("Confirm SkillBreakDown");
    }

    [RelayCommand]
    private void Cancel()
    {
        _logger.LogDebug("Cancel SkillBreakDown");
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_playerStatistics == null)
        {
            _logger.LogDebug("PlayerStatistic is null, refresh abort, return");
            return;
        }

        RefreshAllStatistics();
        _logger.LogDebug("Manual refresh completed");
    }

    /// <summary>
    /// ? 实现IDisposable接口以释放定时器资源
    /// </summary>
    public void Dispose()
    {
        StopRealTimeUpdate();
        _updateTimer = null;
    }

    [RelayCommand]
    private void Unloaded()
    {
        StopRealTimeUpdate();
    }

    #endregion
}