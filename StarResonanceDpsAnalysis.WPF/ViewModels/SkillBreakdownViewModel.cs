using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Properties;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for the skill breakdown view, showing detailed statistics for a player.
/// </summary>
public partial class SkillBreakdownViewModel : BaseViewModel
{
    private readonly ILogger<SkillBreakdownViewModel> _logger;
    private readonly LocalizationManager _localizationManager;
    [ObservableProperty] private StatisticType _statisticIndex;

    /// <summary>
    /// ViewModel for the skill breakdown view, showing detailed statistics for a player.
    /// </summary>
    public SkillBreakdownViewModel(ILogger<SkillBreakdownViewModel> logger, LocalizationManager localizationManager)
    {
        _logger = logger;
        _localizationManager = localizationManager;
        var xAxis = GetXAxisName();
        _dpsPlot = new PlotViewModel(new PlotOptions
        {
            XAxisTitle = xAxis,
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = StatisticType.Damage
        });
        _hpsPlot = new PlotViewModel(new PlotOptions
        {
            XAxisTitle = xAxis,
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = StatisticType.Healing
        });
        _dtpsPlot = new PlotViewModel(new PlotOptions
        {
            XAxisTitle = xAxis,
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = StatisticType.TakenDamage
        });
    }

    /// <summary>
    /// Initializes the ViewModel from a <see cref="StatisticDataViewModel"/>.
    /// </summary>
    public void InitializeFrom(StatisticDataViewModel slot, StatisticType statisticType)
    {
        _logger.LogDebug("Initializing SkillBreakdownViewModel from StatisticDataViewModel for player {PlayerName}",
            slot.Player.Name);

        ObservedSlot = slot;

        // Player Info
        PlayerName = slot.Player?.Name ?? "Unknown";
        Uid = slot.Player?.Uid ?? 0;
        PowerLevel = slot.Player?.PowerLevel ?? 0;
        StatisticIndex = statisticType;

        var duration = TimeSpan.FromTicks(slot.DurationTicks);

        // Calculate statistics from skills
        DamageStats = slot.Damage.TotalSkillList.FromSkillsToDamage(duration);
        HealingStats = slot.Heal.TotalSkillList.FromSkillsToHealing(duration);
        TakenDamageStats = slot.TakenDamage.TotalSkillList.FromSkillsToDamageTaken(duration);

        //UpdatePlotOption();

        // Initialize Chart Data
        InitializeTimeSeries(slot.Damage.Dps, DpsPlot);
        InitializeTimeSeries(slot.Heal.Dps, HpsPlot);
        InitializeTimeSeries(slot.TakenDamage.Dps, DtpsPlot);

        InitializePie(slot.Damage, DpsPlot);
        InitializePie(slot.Heal, HpsPlot);
        InitializePie(slot.TakenDamage, DtpsPlot);

        UpdateHitTypeDistribution(DamageStats, DpsPlot);
        UpdateHitTypeDistribution(HealingStats, HpsPlot);
        UpdateHitTypeDistribution(TakenDamageStats, DtpsPlot);

        _logger.LogDebug("SkillBreakdownViewModel initialized for player: {PlayerName}", PlayerName);
    }

    /// <summary>
    /// ? NEW: Initialize from PlayerStatistics directly (100% accurate!)
    /// </summary>
    public void InitializeFrom(
        PlayerStatistics playerStats,
        PlayerInfo? playerInfo,
        StatisticType statisticType,
        StatisticDataViewModel slot)
    {
        _logger.LogDebug("Initializing SkillBreakdownViewModel from PlayerStatistics for UID {Uid}", 
            playerStats.Uid);

        ObservedSlot = slot; // Keep reference for DPS time series

        // Player Info
        PlayerName = playerInfo?.Name ?? $"UID: {playerStats.Uid}";
        Uid = playerStats.Uid;
        PowerLevel = playerInfo?.CombatPower ?? 0;
        StatisticIndex = statisticType;

        var duration = TimeSpan.FromTicks(playerStats.LastTick - (playerStats.StartTick ?? 0));

        // ? Build skill lists from PlayerStatistics (no battle log iteration!)
        var (damageSkills, healingSkills, takenSkills) = 
            StatisticsToViewModelConverter.BuildSkillListsFromPlayerStats(playerStats);

        // ? Calculate stats from PlayerStatistics directly
        DamageStats = playerStats.AttackDamage.ToDataStatistics(duration);
        HealingStats = playerStats.Healing.ToDataStatistics(duration);
        TakenDamageStats = playerStats.TakenDamage.ToDataStatistics(duration);

        // Initialize Chart Data (still use time series from slot for DPS trends)
        InitializeTimeSeries(slot.Damage.Dps, DpsPlot);
        InitializeTimeSeries(slot.Heal.Dps, HpsPlot);
        InitializeTimeSeries(slot.TakenDamage.Dps, DtpsPlot);

        // ? Use accurate skill lists from PlayerStatistics
        UpdatePieChartDirect(damageSkills, DpsPlot);
        UpdatePieChartDirect(healingSkills, HpsPlot);
        UpdatePieChartDirect(takenSkills, DtpsPlot);

        UpdateHitTypeDistribution(DamageStats, DpsPlot);
        UpdateHitTypeDistribution(HealingStats, HpsPlot);
        UpdateHitTypeDistribution(TakenDamageStats, DtpsPlot);

        _logger.LogDebug("SkillBreakdownViewModel initialized from PlayerStatistics: {Name}", PlayerName);
    }

    private void UpdatePlotOption()
    {
        var xAxis = GetXAxisName();
        DpsPlot.UpdateOption(new PlotOptions
        {
            SeriesPlotTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_RealTimeDps),
            XAxisTitle = xAxis,
            DistributionPlotTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_HitTypeDistribution),
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = StatisticType.Damage
        });
        HpsPlot.UpdateOption(new PlotOptions
        {
            SeriesPlotTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_RealTimeHps),
            XAxisTitle = xAxis,
            DistributionPlotTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_HealTypeDistribution),
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = StatisticType.Healing
        });
        DtpsPlot.UpdateOption(new PlotOptions
        {
            SeriesPlotTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_RealTimeDtps),
            XAxisTitle = xAxis,
            DistributionPlotTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_HitTypeDistribution),
            HitTypeCritical = _localizationManager.GetString(ResourcesKeys.Common_HitType_Critical),
            HitTypeNormal = _localizationManager.GetString(ResourcesKeys.Common_HitType_Normal),
            HitTypeLucky = _localizationManager.GetString(ResourcesKeys.Common_HitType_Lucky),
            StatisticType = StatisticType.TakenDamage
        });
    }

    private string GetXAxisName()
    {
        var xAxis = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_DpsSeriesXAxis);
        return xAxis;
    }

    #region Observed Slot (Data Source)

    [ObservableProperty] private StatisticDataViewModel? _observedSlot;

    partial void OnObservedSlotChanged(StatisticDataViewModel? oldValue, StatisticDataViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Damage.SkillChanged -= DamageSkillChanged;
            oldValue.Heal.SkillChanged -= HealSkillChanged;
            oldValue.TakenDamage.SkillChanged -= TakenDamageSkillChanged;
        }

        if (newValue is not null)
        {
            newValue.Damage.SkillChanged += DamageSkillChanged;
            newValue.Heal.SkillChanged += HealSkillChanged;
            newValue.TakenDamage.SkillChanged += TakenDamageSkillChanged;
        }
    }

    private void DamageSkillChanged(IReadOnlyList<SkillItemViewModel>? skills)
    {
        if (ObservedSlot is null) return;
        if (skills is null) return;
        var duration = TimeSpan.FromTicks(ObservedSlot.DurationTicks);

        skills.UpdateDamage(duration, DamageStats);
        UpdateHitTypeDistribution(DamageStats, DpsPlot);
    }

    private void HealSkillChanged(IReadOnlyList<SkillItemViewModel>? skills)
    {
        if (ObservedSlot is null) return;
        if (skills is null) return;
        var duration = TimeSpan.FromTicks(ObservedSlot.DurationTicks);
        skills.UpdateHealing(duration, HealingStats);
        UpdateHitTypeDistribution(HealingStats, HpsPlot);
    }

    private void TakenDamageSkillChanged(IReadOnlyList<SkillItemViewModel>? skills)
    {
        if (ObservedSlot is null) return;
        if (skills is null) return;
        var duration = TimeSpan.FromTicks(ObservedSlot.DurationTicks);
        skills.UpdateDamageTaken(duration, TakenDamageStats);
        UpdateHitTypeDistribution(TakenDamageStats, DtpsPlot);
    }

    #endregion

    #region Player Info Properties

    [ObservableProperty] private string _playerName = string.Empty;
    [ObservableProperty] private long _uid;
    [ObservableProperty] private long _powerLevel;

    #endregion

    #region Statistics

    [ObservableProperty] private DataStatistics _damageStats = new();
    [ObservableProperty] private DataStatistics _healingStats = new();
    [ObservableProperty] private DataStatistics _takenDamageStats = new();

    #endregion

    #region Chart Models - OxyPlot

    [ObservableProperty] private PlotViewModel _dpsPlot;

    [ObservableProperty] private PlotViewModel _hpsPlot;

    [ObservableProperty] private PlotViewModel _dtpsPlot;

    #endregion

    #region Zoom State

    [ObservableProperty] private double _zoomLevel = 1.0;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 0.2;

    #endregion

    #region Chart Initialization

    private static void InitializeTimeSeries(ObservableCollection<(TimeSpan duration, double section, double total)> data,
        PlotViewModel target)
    {
        void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add when args.NewItems is not null:
                    foreach ((TimeSpan duration, double section, double _) ss in args.NewItems)
                    {
                        target.LineSeriesData.Points.Add(new DataPoint(ss.duration.TotalSeconds, ss.section));
                    }

                    break;
                case NotifyCollectionChangedAction.Reset:
                    target.LineSeriesData.Points.Clear();
                    break;
            }

            target.RefreshSeries();
        }

        data.CollectionChanged += HandleCollectionChanged;

        target.LineSeriesData.Points.Clear();
        foreach (var (duration, section, _) in data)
        {
            target.LineSeriesData.Points.Add(new DataPoint(duration.TotalSeconds, section));
        }

        target.RefreshSeries();
    }

    private static void InitializePie(StatisticDataViewModel.SkillDataCollection data,
        PlotViewModel target)
    {
        data.SkillChanged += list =>
        {
            if (list == null) return;
            UpdatePieChart(list, target);
        };
        UpdatePieChart(data.TotalSkillList, target);
    }

    private static void UpdatePieChart(IReadOnlyList<SkillItemViewModel> skills, PlotViewModel target)
    {
        target.SetPieSeriesData(skills);
    }

    /// <summary>
    /// ? NEW: Update pie chart directly with skill list (no event subscription)
    /// </summary>
    private static void UpdatePieChartDirect(List<SkillItemViewModel> skills, PlotViewModel target)
    {
        target.SetPieSeriesData(skills);
    }

    private void UpdateHitTypeDistribution(DataStatistics stat, PlotViewModel target)
    {
        if (stat.Hits <= 0) return;
        var crit = (double)stat.CritCount / stat.Hits * 100;
        var lucky = (double)stat.LuckyCount / stat.Hits * 100;
        var normal = 100 - crit - lucky;
        target.SetHitTypeDistribution(normal, crit, lucky);
    }

    #endregion

    #region Zoom Commands

    [RelayCommand]
    private void ZoomIn()
    {
        if (ZoomLevel >= MaxZoom) return;
        ZoomLevel += ZoomStep;
        UpdateAllChartZooms();
        _logger.LogDebug("Zoomed in to {ZoomLevel}", ZoomLevel);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (ZoomLevel <= MinZoom) return;
        ZoomLevel -= ZoomStep;
        UpdateAllChartZooms();
        _logger.LogDebug("Zoomed out to {ZoomLevel}", ZoomLevel);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        ResetAllChartZooms();
        _logger.LogDebug("Zoom reset to default");
    }

    private void UpdateAllChartZooms()
    {
        DpsPlot.ApplyZoomToModel(ZoomLevel);
        HpsPlot.ApplyZoomToModel(ZoomLevel);
        DtpsPlot.ApplyZoomToModel(ZoomLevel);
    }

    private void ResetAllChartZooms()
    {
        DpsPlot.ResetModelZoom();
        HpsPlot.ResetModelZoom();
        DtpsPlot.ResetModelZoom();
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
        _logger.LogDebug("Manual refresh");
    }

    #endregion
}