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

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for the skill breakdown view, showing detailed statistics for a player.
/// </summary>
public partial class SkillBreakdownViewModel : BaseViewModel
{
    private readonly ILogger<SkillBreakdownViewModel> _logger;
    private readonly LocalizationManager _localizationManager;
    [ObservableProperty] private StatisticType _statisticIndex;
    private PlayerStatistics? _playerStatistics;

    // NEW: Tab ViewModels for modular components
    [ObservableProperty] private TabContentViewModel _dpsTabViewModel = new();
    [ObservableProperty] private TabContentViewModel _healingTabViewModel = new();
    [ObservableProperty] private TabContentViewModel _tankingTabViewModel = new();

    /// <summary>
    /// ViewModel for the skill breakdown view, showing detailed statistics for a player.
    /// </summary>
    public SkillBreakdownViewModel(ILogger<SkillBreakdownViewModel> logger, LocalizationManager localizationManager)
    {
        _logger = logger;
        _localizationManager = localizationManager;
        
        var xAxis = GetXAxisName();
        _dpsPlot = CreatePlotViewModel(xAxis, StatisticType.Damage);
        _hpsPlot = CreatePlotViewModel(xAxis, StatisticType.Healing);
        _dtpsPlot = CreatePlotViewModel(xAxis, StatisticType.TakenDamage);
        // Initialize Tab ViewModels
        InitializeTabViewModels();
    }

    private void InitializeTabViewModels()
    {
        // Setup DPS Tab
        _dpsTabViewModel.ChartViewModel.TimeSeriesTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_RealTimeDps);
        _dpsTabViewModel.ChartViewModel.PieChartTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_SkillDamageDistribution);
        _dpsTabViewModel.ChartViewModel.HitTypeChartTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_HitTypeDistribution);
        _dpsTabViewModel.ChartViewModel.ResetZoomCommand = ResetZoomCommand;
        _dpsTabViewModel.ChartViewModel.ZoomInCommand = ZoomInCommand;
        _dpsTabViewModel.ChartViewModel.ZoomOutCommand = ZoomOutCommand;
        _dpsTabViewModel.SkillListViewModel.SectionTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Section_DamageSkillsAnalysis) + " (DAMAGE ANALYSIS)";
        _dpsTabViewModel.SkillListViewModel.IconColor = "#2297F4";
        _dpsTabViewModel.StatsViewModel.TotalLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_TotalDamage);
        _dpsTabViewModel.StatsViewModel.AverageLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_AverageDps);
        _dpsTabViewModel.StatsViewModel.HitsLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_TotalHits);
        _dpsTabViewModel.StatsViewModel.CritRateLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritRate);
        _dpsTabViewModel.StatsViewModel.CritCountLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritCount);
        _dpsTabViewModel.StatsViewModel.LuckyRateLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckRate);
        _dpsTabViewModel.StatsViewModel.LuckyCountLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckCount);
        _dpsTabViewModel.StatsViewModel.NormalValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_NormalDamage);
        _dpsTabViewModel.StatsViewModel.CritValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritDamage);
        _dpsTabViewModel.StatsViewModel.LuckyValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckDamage);

        // Setup Healing Tab
        _healingTabViewModel.ChartViewModel.TimeSeriesTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_RealTimeHps);
        _healingTabViewModel.ChartViewModel.PieChartTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_HealingDistribution);
        _healingTabViewModel.ChartViewModel.HitTypeChartTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_HealTypeDistribution);
        _healingTabViewModel.ChartViewModel.ResetZoomCommand = ResetZoomCommand;
        _healingTabViewModel.ChartViewModel.ZoomInCommand = ZoomInCommand;
        _healingTabViewModel.ChartViewModel.ZoomOutCommand = ZoomOutCommand;
        _healingTabViewModel.SkillListViewModel.SectionTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Section_HealingSkillsAnalysis) + " (HEALING ANALYSIS)";
        _healingTabViewModel.SkillListViewModel.IconColor = "#3CB371";
        _healingTabViewModel.StatsViewModel.TotalLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_TotalHealing);
        _healingTabViewModel.StatsViewModel.AverageLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_AverageHps);
        _healingTabViewModel.StatsViewModel.HitsLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_TotalHits);
        _healingTabViewModel.StatsViewModel.CritRateLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritRate);
        _healingTabViewModel.StatsViewModel.CritCountLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritCount);
        _healingTabViewModel.StatsViewModel.LuckyRateLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckRate);
        _healingTabViewModel.StatsViewModel.LuckyCountLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckCount);
        _healingTabViewModel.StatsViewModel.NormalValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_NormalDamage);
        _healingTabViewModel.StatsViewModel.CritValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritDamage);
        _healingTabViewModel.StatsViewModel.LuckyValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckDamage);

        // Setup Tanking Tab
        _tankingTabViewModel.ChartViewModel.TimeSeriesTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_RealTimeDtps);
        _tankingTabViewModel.ChartViewModel.PieChartTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_DamageSourcesDistribution);
        _tankingTabViewModel.ChartViewModel.HitTypeChartTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_HitTypeDistribution);
        _tankingTabViewModel.ChartViewModel.ResetZoomCommand = ResetZoomCommand;
        _tankingTabViewModel.ChartViewModel.ZoomInCommand = ZoomInCommand;
        _tankingTabViewModel.ChartViewModel.ZoomOutCommand = ZoomOutCommand;
        _tankingTabViewModel.SkillListViewModel.SectionTitle = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Section_DamageTakenAnalysis) + " (TANKING ANALYSIS)";
        _tankingTabViewModel.SkillListViewModel.IconColor = "#E64A19";
        _tankingTabViewModel.StatsViewModel.TotalLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_TotalDamageTaken);
        _tankingTabViewModel.StatsViewModel.AverageLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_AverageDtps);
        _tankingTabViewModel.StatsViewModel.HitsLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_TotalHitsTaken);
        _tankingTabViewModel.StatsViewModel.CritRateLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritRate);
        _tankingTabViewModel.StatsViewModel.CritCountLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritCount);
        _tankingTabViewModel.StatsViewModel.LuckyRateLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckRate);
        _tankingTabViewModel.StatsViewModel.LuckyCountLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckCount);
        _tankingTabViewModel.StatsViewModel.NormalValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_NormalDamage);
        _tankingTabViewModel.StatsViewModel.CritValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_CritDamage);
        _tankingTabViewModel.StatsViewModel.LuckyValueLabel = _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Label_LuckDamage);
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

        _logger.LogDebug("SkillBreakdownViewModel initialized from PlayerStatistics: {Name}", PlayerName);
    }

    private void UpdateTabViewModels()
    {
        // Update DPS Tab
        DpsTabViewModel.StatsViewModel.UpdateFromDataStatistics(DamageStats);
        DpsTabViewModel.ChartViewModel.UpdateFromPlotViewModel(DpsPlot);
        DpsTabViewModel.ChartViewModel.NormalCount = DamageStats.Hits - DamageStats.CritCount - DamageStats.LuckyCount;
        DpsTabViewModel.ChartViewModel.CritCount = DamageStats.CritCount;
        DpsTabViewModel.ChartViewModel.LuckyCount = DamageStats.LuckyCount;
        if (DamageStats.Hits > 0)
        {
            DpsTabViewModel.ChartViewModel.NormalPercentage = (double)DpsTabViewModel.ChartViewModel.NormalCount / DamageStats.Hits * 100;
            DpsTabViewModel.ChartViewModel.CritPercentage = (double)DamageStats.CritCount / DamageStats.Hits * 100;
            DpsTabViewModel.ChartViewModel.LuckyPercentage = (double)DamageStats.LuckyCount / DamageStats.Hits * 100;
        }
        else
        {
            DpsTabViewModel.ChartViewModel.NormalPercentage = 0;
            DpsTabViewModel.ChartViewModel.CritPercentage = 0;
            DpsTabViewModel.ChartViewModel.LuckyPercentage = 0;
        }

        if (ObservedSlot != null)
        {
            var skills = ObservedSlot.Damage.TotalSkillList;
            UpdateSkillPercentages(skills, DamageStats.Total);
            DpsTabViewModel.SkillListViewModel.SkillItems = new ObservableCollection<SkillItemViewModel>(skills);
        }

        // Update Healing Tab
        HealingTabViewModel.StatsViewModel.UpdateFromDataStatistics(HealingStats);
        HealingTabViewModel.ChartViewModel.UpdateFromPlotViewModel(HpsPlot);
        HealingTabViewModel.ChartViewModel.NormalCount = HealingStats.Hits - HealingStats.CritCount - HealingStats.LuckyCount;
        HealingTabViewModel.ChartViewModel.CritCount = HealingStats.CritCount;
        HealingTabViewModel.ChartViewModel.LuckyCount = HealingStats.LuckyCount;
        if (HealingStats.Hits > 0)
        {
            HealingTabViewModel.ChartViewModel.NormalPercentage = (double)HealingTabViewModel.ChartViewModel.NormalCount / HealingStats.Hits * 100;
            HealingTabViewModel.ChartViewModel.CritPercentage = (double)HealingStats.CritCount / HealingStats.Hits * 100;
            HealingTabViewModel.ChartViewModel.LuckyPercentage = (double)HealingStats.LuckyCount / HealingStats.Hits * 100;
        }
        else
        {
            HealingTabViewModel.ChartViewModel.NormalPercentage = 0;
            HealingTabViewModel.ChartViewModel.CritPercentage = 0;
            HealingTabViewModel.ChartViewModel.LuckyPercentage = 0;
        }

        if (ObservedSlot != null)
        {
            var skills = ObservedSlot.Heal.TotalSkillList;
            UpdateSkillPercentages(skills, HealingStats.Total);
            HealingTabViewModel.SkillListViewModel.SkillItems = new ObservableCollection<SkillItemViewModel>(skills);
        }

        // Update Tanking Tab
        TankingTabViewModel.StatsViewModel.UpdateFromDataStatistics(TakenDamageStats);
        TankingTabViewModel.ChartViewModel.UpdateFromPlotViewModel(DtpsPlot);
        TankingTabViewModel.ChartViewModel.NormalCount = TakenDamageStats.Hits - TakenDamageStats.CritCount - TakenDamageStats.LuckyCount;
        TankingTabViewModel.ChartViewModel.CritCount = TakenDamageStats.CritCount;
        TankingTabViewModel.ChartViewModel.LuckyCount = TakenDamageStats.LuckyCount;
        if (TakenDamageStats.Hits > 0)
        {
            TankingTabViewModel.ChartViewModel.NormalPercentage = (double)TankingTabViewModel.ChartViewModel.NormalCount / TakenDamageStats.Hits * 100;
            TankingTabViewModel.ChartViewModel.CritPercentage = (double)TakenDamageStats.CritCount / TakenDamageStats.Hits * 100;
            TankingTabViewModel.ChartViewModel.LuckyPercentage = (double)TakenDamageStats.LuckyCount / TakenDamageStats.Hits * 100;
        }
        else
        {
            TankingTabViewModel.ChartViewModel.NormalPercentage = 0;
            TankingTabViewModel.ChartViewModel.CritPercentage = 0;
            TankingTabViewModel.ChartViewModel.LuckyPercentage = 0;
        }

        if (ObservedSlot != null)
        {
            var skills = ObservedSlot.TakenDamage.TotalSkillList;
            UpdateSkillPercentages(skills, TakenDamageStats.Total);
            TankingTabViewModel.SkillListViewModel.SkillItems = new ObservableCollection<SkillItemViewModel>(skills);
        }
    }

    private void UpdateSkillPercentages(IEnumerable<SkillItemViewModel> skills, long total)
    {
        var skillList = skills.ToList();
        if (skillList.Count == 0) return;

        // Calculate max value for relative percentage (so the top item is 100%)
        double max = 0;
        foreach (var skill in skillList)
        {
            double val = 0;
            if (skill.Damage.TotalValue > 0) val = skill.Damage.TotalValue;
            else if (skill.Heal.TotalValue > 0) val = skill.Heal.TotalValue;
            else if (skill.TakenDamage.TotalValue > 0) val = skill.TakenDamage.TotalValue;
            
            if (val > max) max = val;
        }

        if (max <= 0)
        {
            foreach (var skill in skillList) skill.Percentage = 0;
            return;
        }

        foreach (var skill in skillList)
        {
            double val = 0;
            if (skill.Damage.TotalValue > 0) val = skill.Damage.TotalValue;
            else if (skill.Heal.TotalValue > 0) val = skill.Heal.TotalValue;
            else if (skill.TakenDamage.TotalValue > 0) val = skill.TakenDamage.TotalValue;
            
            skill.Percentage = val / max * 100; 
        }
    }

    #region Observed Slot (Data Source)

    [ObservableProperty] private StatisticDataViewModel? _observedSlot;

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
            StatisticType = statisticType
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
        var (damageSkills, healingSkills, takenSkills) = 
            StatisticsToViewModelConverter.BuildSkillListsFromPlayerStats(_playerStatistics);

        // Update damage statistics
        UpdateStatisticSet(
            _playerStatistics.AttackDamage,
            damageSkills,
            duration,
            stats => DamageStats = stats,
            _playerStatistics.GetDpsSamples(),
            DpsPlot);

        // Update healing statistics
        UpdateStatisticSet(
            _playerStatistics.Healing,
            healingSkills,
            duration,
            stats => HealingStats = stats,
            _playerStatistics.GetHpsSamples(),
            HpsPlot);

        // Update taken damage statistics
        UpdateStatisticSet(
            _playerStatistics.TakenDamage,
            takenSkills,
            duration,
            stats => TakenDamageStats = stats,
            _playerStatistics.GetDtpsSamples(),
            DtpsPlot);
    }

    /// <summary>
    /// Update a single statistic set with all its associated data (Open/Closed Principle)
    /// </summary>
    private void UpdateStatisticSet(
        StatisticValues statisticValues,
        List<SkillItemViewModel> skills,
        TimeSpan duration,
        Action<DataStatistics> setStatistics,
        IReadOnlyList<DpsDataPoint> timeSeries,
        PlotViewModel plot)
    {
        // Convert and set statistics
        var stats = statisticValues.ToDataStatistics(duration);
        PopulateSkills(stats.Skills, skills);
        setStatistics(stats);

        // Update charts
        UpdateChartsForStatistic(skills, timeSeries, stats, plot);
    }

    /// <summary>
    /// Populate skills collection efficiently
    /// </summary>
    private static void PopulateSkills(ObservableCollection<SkillItemViewModel> target, List<SkillItemViewModel> source)
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
        DataStatistics stats,
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
    private static void UpdateHitTypeDistribution(DataStatistics stat, PlotViewModel target)
    {
        if (stat.Hits <= 0) return;
        
        var crit = (double)stat.CritCount / stat.Hits * 100;
        var lucky = (double)stat.LuckyCount / stat.Hits * 100;
        var normal = 100 - crit - lucky;
        
        target.SetHitTypeDistribution(normal, crit, lucky);

        // Update ChartViewModel properties for the new UI
        // We need to access the corresponding TabContentViewModel's ChartViewModel
        // But here we only have PlotViewModel target.
        // We should update the TabContentViewModel instead.
    }

    /// <summary>
    /// Update plot options with current localization
    /// </summary>
    private void UpdatePlotOption()
    {
        var xAxis = GetXAxisName();
        
        UpdateSinglePlotOption(DpsPlot, xAxis, StatisticType.Damage,
            ResourcesKeys.SkillBreakdown_Chart_RealTimeDps,
            ResourcesKeys.SkillBreakdown_Chart_HitTypeDistribution);
            
        UpdateSinglePlotOption(HpsPlot, xAxis, StatisticType.Healing,
            ResourcesKeys.SkillBreakdown_Chart_RealTimeHps,
            ResourcesKeys.SkillBreakdown_Chart_HealTypeDistribution);
            
        UpdateSinglePlotOption(DtpsPlot, xAxis, StatisticType.TakenDamage,
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
        if (_playerStatistics == null)
        {
            _logger.LogDebug("PlayerStatistic is null, refresh abort, return");
            return;
        }

        RefreshAllStatistics();
        _logger.LogDebug("Manual refresh completed");
    }

    #endregion
}