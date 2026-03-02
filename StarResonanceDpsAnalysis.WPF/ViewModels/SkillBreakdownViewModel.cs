using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;

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

    // 实时更新定时器
    private DispatcherTimer? _updateTimer;
    private const int UpdateIntervalMs = 1000; // 每秒更新一次

    // 一度でも時系列データを受け取ったか
    private bool _hasSeenAnyTimeSeriesData;

    // 空サンプルになったらグラフを凍結するか（今回はタイマー停止しない）
    private bool _freezeChartAndStopTimerWhenSeriesBecomesEmpty = true;

    // 空になったログを一度だけ出すためのフラグ
    private bool _hasLoggedSeriesBecameEmpty;

    // UIDごとの最後の非空折れ線キャッシュ（アプリ起動中のみ保持）
    private sealed class FrozenSeriesCache
    {
        public List<DataPoint> DpsPoints { get; } = new();
        public List<DataPoint> HpsPoints { get; } = new();
        public List<DataPoint> DtpsPoints { get; } = new();
    }

    private static readonly Dictionary<long, FrozenSeriesCache> s_frozenSeriesByUid = new();

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

        _dpsTabViewModel.Plot.DamageDisplayMode = _appConfig.DamageDisplayType;
        _healingTabViewModel.Plot.DamageDisplayMode = _appConfig.DamageDisplayType;
        _tankingTabViewModel.Plot.DamageDisplayMode = _appConfig.DamageDisplayType;

        InitializeUpdateTimer();
    }

    /// <summary>
    /// 初始化实时更新定时器
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
    /// 定时器回调：刷新统计数据
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_playerStatistics == null)
        {
            return;
        }

        var playerUid = _playerStatistics.Uid;
        if (playerUid == 0)
        {
            return;
        }

        try
        {
            var latestStats = _storage.GetStatistics(fullSession: false);
            if (latestStats.TryGetValue(playerUid, out var updated))
            {
                var hasAnySamples = HasAnyTimeSeriesSamples(updated);

                if (hasAnySamples)
                {
                    _hasSeenAnyTimeSeriesData = true;
                    _hasLoggedSeriesBecameEmpty = false; // 次回空になったときにまた1回だけログを出せるようにする
                }

                _playerStatistics = updated;
                RefreshAllStatistics();

                if (!hasAnySamples && _freezeChartAndStopTimerWhenSeriesBecomesEmpty && _hasSeenAnyTimeSeriesData)
                {
                    // タイマーは止めない。最後の非空キャッシュで表示を維持し続ける。
                    if (!_hasLoggedSeriesBecameEmpty)
                    {
                        _logger.LogDebug(
                            "Time series became empty after having data. Freeze current chart and continue real-time update.");

                        _hasLoggedSeriesBecameEmpty = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating skill breakdown data");
        }
    }

    /// <summary>
    /// 启动实时更新
    /// </summary>
    public void StartRealTimeUpdate()
    {
        if (_updateTimer == null)
        {
            InitializeUpdateTimer();
        }

        _updateTimer.Start();
        _logger.LogDebug("Started real-time update for SkillBreakdownView");
    }

    /// <summary>
    /// 停止实时更新
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
        PlayerInfo? playerInfo,
        StatisticType statisticType)
    {
        _logger.LogDebug("Initializing SkillBreakdownViewModel from PlayerStatistics for UID {Uid}",
            playerStats.Uid);

        _playerStatistics = playerStats;

        // live samples が空でも、以前の凍結グラフキャッシュがあれば「見たことあり」扱い
        _hasSeenAnyTimeSeriesData =
            HasAnyTimeSeriesSamples(playerStats) || HasFrozenSeriesCache(playerStats.Uid);

        _hasLoggedSeriesBecameEmpty = false;

        // Update player info
        UpdatePlayerInfo(playerStats, playerInfo);
        StatisticIndex = statisticType;

        // Update all statistics
        RefreshAllStatistics();

        // 启动实时更新
        StartRealTimeUpdate();

        _logger.LogDebug("SkillBreakdownViewModel initialized from PlayerStatistics: {Name}", PlayerName);
    }

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

    private static bool HasAnyTimeSeriesSamples(PlayerStatistics stats)
    {
        return stats.GetDeltaDpsSamples().Count > 0
            || stats.GetDeltaHpsSamples().Count > 0
            || stats.GetDeltaDtpsSamples().Count > 0;
    }

    private static bool HasFrozenSeriesCache(long uid)
    {
        return s_frozenSeriesByUid.TryGetValue(uid, out var cache)
            && (cache.DpsPoints.Count > 0
                || cache.HpsPoints.Count > 0
                || cache.DtpsPoints.Count > 0);
    }

    private static FrozenSeriesCache GetOrCreateFrozenSeriesCache(long uid)
    {
        if (!s_frozenSeriesByUid.TryGetValue(uid, out var cache))
        {
            cache = new FrozenSeriesCache();
            s_frozenSeriesByUid[uid] = cache;
        }

        return cache;
    }

    /// <summary>
    /// live samples が非空なら cache を更新して返す。
    /// live samples が空なら cache をそのまま返す。
    /// </summary>
    private static IReadOnlyList<DataPoint> GetDisplaySeriesPoints(
        IReadOnlyList<DpsDataPoint> liveSamples,
        List<DataPoint> cachedPoints)
    {
        if (liveSamples != null && liveSamples.Count > 0)
        {
            cachedPoints.Clear();

            foreach (var sample in liveSamples)
            {
                cachedPoints.Add(new DataPoint(sample.Time.TotalSeconds, sample.Value));
            }
        }

        return cachedPoints;
    }

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
            HitTypeCriticalLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_CriticalLucky),
            StatisticType = statisticType,
        });
    }

    /// <summary>
    /// Update player basic information
    /// </summary>
    private void UpdatePlayerInfo(PlayerStatistics playerStats, PlayerInfo? playerInfo)
    {
        PlayerName = playerInfo?.Name ?? $"UID: {playerStats.Uid}";
        Uid = playerStats.Uid;
        PowerLevel = playerInfo?.CombatPower ?? 0;
    }

    /// <summary>
    /// Refresh all statistics from PlayerStatistics
    /// </summary>
    private void RefreshAllStatistics()
    {
        if (_playerStatistics == null)
        {
            _logger.LogWarning("Cannot refresh statistics: PlayerStatistics is null");
            return;
        }

        var duration = TimeSpan.FromTicks(_playerStatistics.LastTick - (_playerStatistics.StartTick ?? 0));
        var skillLists = _playerStatistics.ToSkillItemVmList(_localizationManager);

        // UIDごとの最後の非空折れ線キャッシュ
        var frozenCache = GetOrCreateFrozenSeriesCache(_playerStatistics.Uid);

        var dpsPoints = GetDisplaySeriesPoints(_playerStatistics.GetDeltaDpsSamples(), frozenCache.DpsPoints);
        var hpsPoints = GetDisplaySeriesPoints(_playerStatistics.GetDeltaHpsSamples(), frozenCache.HpsPoints);
        var dtpsPoints = GetDisplaySeriesPoints(_playerStatistics.GetDeltaDtpsSamples(), frozenCache.DtpsPoints);

        // Update damage statistics
        UpdateStatisticSet(
            DpsTabViewModel,
            _playerStatistics.AttackDamage,
            skillLists.Damage,
            duration,
            dpsPoints);

        // Update healing statistics
        UpdateStatisticSet(
            HealingTabViewModel,
            _playerStatistics.Healing,
            skillLists.Healing,
            duration,
            hpsPoints);

        // Update taken damage statistics
        UpdateStatisticSet(
            TankingTabViewModel,
            _playerStatistics.TakenDamage,
            skillLists.Taken,
            duration,
            dtpsPoints);
    }

    /// <summary>
    /// Update a single statistic set with all its associated data
    /// </summary>
    private void UpdateStatisticSet(
        TabContentViewModel tabViewModel,
        StatisticValues statisticValues,
        List<SkillItemViewModel> skills,
        TimeSpan duration,
        IReadOnlyList<DataPoint> timeSeries)
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
    /// Update all charts for a single statistic type
    /// </summary>
    private static void UpdateChartsForStatistic(
        List<SkillItemViewModel> skills,
        IReadOnlyList<DataPoint> timeSeries,
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
    /// Update time series chart from display points
    /// </summary>
    private static void UpdateTimeSeriesChart(IReadOnlyList<DataPoint> samples, PlotViewModel target)
    {
        // 空なら既存のグラフを保持
        if (samples == null || samples.Count == 0)
        {
            return;
        }

        target.LineSeriesData.Points.Clear();
        foreach (var sample in samples)
        {
            target.LineSeriesData.Points.Add(sample);
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
    /// Clear all statistics currently shown in the view.
    /// This clears stats, skill lists, time-series chart, pie chart, hit-type distribution,
    /// and also clears frozen line caches for the current player.
    /// </summary>
    private void ClearAllStatistics()
    {
        if (_playerStatistics != null)
        {
            s_frozenSeriesByUid.Remove(_playerStatistics.Uid);
        }

        _hasSeenAnyTimeSeriesData = false;
        _hasLoggedSeriesBecameEmpty = false;

        ClearSingleStatisticSet(DpsTabViewModel);
        ClearSingleStatisticSet(HealingTabViewModel);
        ClearSingleStatisticSet(TankingTabViewModel);
    }

    /// <summary>
    /// Clear one tab's displayed stats and charts.
    /// </summary>
    private static void ClearSingleStatisticSet(TabContentViewModel tabViewModel)
    {
        tabViewModel.Stats = new StatisticValues().ToDataStatistics(TimeSpan.Zero);

        tabViewModel.SkillList.SkillItems.Clear();

        ClearTimeSeriesChart(tabViewModel.Plot);

        tabViewModel.Plot.SetPieSeriesData(Array.Empty<SkillItemViewModel>());
        tabViewModel.Plot.SetHitTypeDistribution(0, 0, 0);
    }

    /// <summary>
    /// Clear time-series line chart immediately.
    /// </summary>
    private static void ClearTimeSeriesChart(PlotViewModel target)
    {
        target.LineSeriesData.Points.Clear();
        target.RefreshSeries();
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
        ClearAllStatistics();
        _logger.LogDebug("Manual refresh completed");
    }

    /// <summary>
    /// 实现 IDisposable 接口以释放定时器资源
    /// </summary>
    public void Dispose()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= UpdateTimer_Tick;
            _updateTimer = null;
        }
    }

    [RelayCommand]
    private void Unloaded()
    {
        StopRealTimeUpdate();
    }

    #endregion
}