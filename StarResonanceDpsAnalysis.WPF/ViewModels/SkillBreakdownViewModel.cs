using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
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
/// Graph points are read directly from PlayerStatistics.
/// </summary>
public partial class SkillBreakdownViewModel : BaseViewModel, IDisposable
{
    private readonly ILogger<SkillBreakdownViewModel> _logger;
    private readonly LocalizationManager _localizationManager;
    private readonly IDataStorage _storage;

    [ObservableProperty] private StatisticType _statisticIndex;
    private PlayerStatistics? _playerStatistics;

    [ObservableProperty] private Config.AppConfig _appConfig;

    private int TimeSeriesPointCapacity => Math.Clamp(AppConfig.TimeSeriesSampleCapacity, 50, 1000);

    [ObservableProperty] private TabContentViewModel _dpsTabViewModel;
    [ObservableProperty] private TabContentViewModel _healingTabViewModel;
    [ObservableProperty] private TabContentViewModel _tankingTabViewModel;

    /// <summary>
    /// True when the current source is the live storage object.
    /// False when the current source is a history snapshot loaded from JSON.
    /// </summary>
    private bool _isLiveSource;

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

        _storage.DpsDataUpdated += OnStorageDpsDataUpdated;
    }

    private void OnStorageDpsDataUpdated()
    {
        if (!_isLiveSource || _playerStatistics == null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(OnStorageDpsDataUpdated));
            return;
        }

        try
        {
            RefreshCurrentPlayerFromStorage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing SkillBreakdownViewModel from live storage");
        }
    }

    /// <summary>
    /// Initialize from PlayerStatistics directly.
    /// </summary>
    public void InitializeFrom(
        PlayerStatistics playerStats,
        PlayerInfo? playerInfo,
        StatisticType statisticType)
    {
        _logger.LogDebug(
            "Initializing SkillBreakdownViewModel from PlayerStatistics for UID {Uid}",
            playerStats.Uid);

        _playerStatistics = playerStats;
        _isLiveSource = IsCurrentStorageInstance(playerStats);

        UpdatePlayerInfo(playerStats, playerInfo);
        StatisticIndex = statisticType;

        // Always render immediately from the passed-in object first.
        // This is important for both history snapshots and freshly opened live view.
        RefreshAllStatistics();

        _logger.LogDebug(
            "SkillBreakdownViewModel initialized from PlayerStatistics: {Name}, LiveSource={IsLiveSource}",
            PlayerName,
            _isLiveSource);
    }

    private bool IsCurrentStorageInstance(PlayerStatistics playerStats)
    {
        var liveStats = _storage.GetStatistics(fullSession: false);
        return liveStats.TryGetValue(playerStats.Uid, out var currentLiveRef)
               && ReferenceEquals(currentLiveRef, playerStats);
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

    private void RefreshCurrentPlayerFromStorage()
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

        var latestStats = _storage.GetStatistics(fullSession: false);
        if (latestStats.TryGetValue(playerUid, out var updated))
        {
            _playerStatistics = updated;
            RefreshAllStatistics();
            return;
        }

        // Current section was cleared or the player disappeared.
        ClearAllStatistics();
    }

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

    private void UpdatePlayerInfo(PlayerStatistics playerStats, PlayerInfo? playerInfo)
    {
        PlayerName = playerInfo?.Name ?? $"UID: {playerStats.Uid}";
        Uid = playerStats.Uid;
        PowerLevel = playerInfo?.CombatPower ?? 0;
    }

    private void RefreshAllStatistics()
    {
        if (_playerStatistics == null)
        {
            _logger.LogWarning("Cannot refresh statistics: PlayerStatistics is null");
            return;
        }

        var duration = TimeSpan.FromTicks(
            Math.Max(0, _playerStatistics.LastTick - (_playerStatistics.StartTick ?? 0)));

        var skillLists = _playerStatistics.ToSkillItemVmList(_localizationManager);

        UpdateStatisticSet(
            DpsTabViewModel,
            _playerStatistics.AttackDamage,
            skillLists.Damage,
            duration,
            _playerStatistics.GetDeltaDpsSamples());

        UpdateStatisticSet(
            HealingTabViewModel,
            _playerStatistics.Healing,
            skillLists.Healing,
            duration,
            _playerStatistics.GetDeltaHpsSamples());

        UpdateStatisticSet(
            TankingTabViewModel,
            _playerStatistics.TakenDamage,
            skillLists.Taken,
            duration,
            _playerStatistics.GetDeltaDtpsSamples());
    }

    private void UpdateStatisticSet(
        TabContentViewModel tabViewModel,
        StatisticValues statisticValues,
        List<SkillItemViewModel> skills,
        TimeSpan duration,
        IReadOnlyList<DpsDataPoint> timeSeries)
    {
        var stats = statisticValues.ToDataStatistics(duration);
        tabViewModel.Stats = stats;

        PopulateSkills(tabViewModel.SkillList.SkillItems, skills);

        UpdateChartsForStatistic(skills, timeSeries, stats, tabViewModel.Plot);
    }

    private void PopulateSkills(ObservableCollection<SkillItemViewModel> target, List<SkillItemViewModel> source)
    {
        target.Clear();
        foreach (var skill in source)
        {
            target.Add(skill);
        }
    }

    private void UpdateChartsForStatistic(
        List<SkillItemViewModel> skills,
        IReadOnlyList<DpsDataPoint> timeSeries,
        DataStatisticsViewModel stats,
        PlotViewModel plot)
    {
        UpdateTimeSeriesChart(timeSeries, plot);

        plot.SetPieSeriesData(skills);

        UpdateHitTypeDistribution(stats, plot);
    }

    private void UpdateTimeSeriesChart(IReadOnlyList<DpsDataPoint> samples, PlotViewModel target)
    {
        target.LineSeriesData.Points.Clear();

        if (samples != null)
        {
            foreach (var sample in samples)
            {
                target.LineSeriesData.Points.Add(new DataPoint(sample.Time.TotalSeconds, sample.Value));
            }
        }

        AdjustTimeAxisWindow(target.LineSeriesData.Points, target);
        target.RefreshSeries();
    }

    private void AdjustTimeAxisWindow(IReadOnlyList<DataPoint> samples, PlotViewModel target)
    {
        var xAxis = target.SeriesPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
        if (xAxis == null)
        {
            return;
        }

        if (samples == null || samples.Count == 0)
        {
            xAxis.Minimum = 0;
            return;
        }

        if (samples.Count >= TimeSeriesPointCapacity)
        {
            var oldestX = samples[0].X;
            var newMin = Math.Max(0, oldestX);
            xAxis.Minimum = newMin;
        }
        else
        {
            xAxis.Minimum = 0;
        }
    }

    private static void UpdateHitTypeDistribution(DataStatisticsViewModel stat, PlotViewModel target)
    {
        if (stat.Hits <= 0)
        {
            target.SetHitTypeDistribution(0, 0, 0);
            return;
        }

        var crit = (double)stat.CritCount / stat.Hits * 100;
        var lucky = (double)stat.LuckyCount / stat.Hits * 100;
        var normal = 100 - crit - lucky;

        target.SetHitTypeDistribution(normal, crit, lucky);
    }

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

    private void ClearAllStatistics()
    {
        ClearSingleStatisticSet(DpsTabViewModel);
        ClearSingleStatisticSet(HealingTabViewModel);
        ClearSingleStatisticSet(TankingTabViewModel);
    }

    private static void ClearSingleStatisticSet(TabContentViewModel tabViewModel)
    {
        tabViewModel.Stats = new StatisticValues().ToDataStatistics(TimeSpan.Zero);

        tabViewModel.SkillList.SkillItems.Clear();

        ClearTimeSeriesChart(tabViewModel.Plot);

        tabViewModel.Plot.SetPieSeriesData(Array.Empty<SkillItemViewModel>());
        tabViewModel.Plot.SetHitTypeDistribution(0, 0, 0);
    }

    private static void ClearTimeSeriesChart(PlotViewModel target)
    {
        target.LineSeriesData.Points.Clear();

        var xAxis = target.SeriesPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom);
        if (xAxis != null)
        {
            xAxis.Minimum = 0;
        }

        target.RefreshSeries();
    }

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

        /*
        if (_playerStatistics == null)
        {
            ClearAllStatistics();
            _logger.LogDebug("Manual refresh completed (no player statistics)");
            return;
        }

        if (_isLiveSource)
        {
            RefreshCurrentPlayerFromStorage();
        }
        else
        {
            RefreshAllStatistics();
        }
        */

        _logger.LogDebug("Manual refresh completed");
    }

    public void Dispose()
    {
        _storage.DpsDataUpdated -= OnStorageDpsDataUpdated;
    }

    [RelayCommand]
    private void Unloaded()
    {
        // No background cache service anymore.
    }

    #endregion
}