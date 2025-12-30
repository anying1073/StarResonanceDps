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
        PlayerInfo? playerInfo,
        StatisticType statisticType,
        StatisticDataViewModel slot)
    {
        _logger.LogDebug("Initializing SkillBreakdownViewModel from PlayerStatistics for UID {Uid}",
            playerStats.Uid);
        _playerStatistics = playerStats;

        ObservedSlot = slot; // Keep reference for backward compatibility

        // Player Info
        PlayerName = playerInfo?.Name ?? $"UID: {playerStats.Uid}";
        Uid = playerStats.Uid;
        PowerLevel = playerInfo?.CombatPower ?? 0;
        StatisticIndex = statisticType;

        var duration = TimeSpan.FromTicks(playerStats.LastTick - (playerStats.StartTick ?? 0));

        // Build skill lists from PlayerStatistics (no battle log iteration!)
        var (damageSkills, healingSkills, takenSkills) =
            StatisticsToViewModelConverter.BuildSkillListsFromPlayerStats(playerStats);

        // Calculate stats from PlayerStatistics directly
        DamageStats = playerStats.AttackDamage.ToDataStatistics(duration);
        HealingStats = playerStats.Healing.ToDataStatistics(duration);
        TakenDamageStats = playerStats.TakenDamage.ToDataStatistics(duration);

        // ⭐ Populate Skills collections
        foreach (var skill in damageSkills)
        {
            DamageStats.Skills.Add(skill);
        }
        foreach (var skill in healingSkills)
        {
            HealingStats.Skills.Add(skill);
        }
        foreach (var skill in takenSkills)
        {
            TakenDamageStats.Skills.Add(skill);
        }

        // ⭐ NEW: Load time series data directly from PlayerStatistics Core layer
        InitializeTimeSeriesFromCore(playerStats.GetDpsSamples(), DpsPlot);
        InitializeTimeSeriesFromCore(playerStats.GetHpsSamples(), HpsPlot);
        InitializeTimeSeriesFromCore(playerStats.GetDtpsSamples(), DtpsPlot);
        // Update Tab ViewModels
        UpdateTabViewModels();

        // Use accurate skill lists from PlayerStatistics
        UpdatePieChartDirect(damageSkills, DpsPlot);
        UpdatePieChartDirect(healingSkills, HpsPlot);
        UpdatePieChartDirect(takenSkills, DtpsPlot);

        UpdateHitTypeDistribution(DamageStats, DpsPlot);
        UpdateHitTypeDistribution(HealingStats, HpsPlot);
        UpdateHitTypeDistribution(TakenDamageStats, DtpsPlot);

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

    /// <summary>
    /// ⭐ NEW: Initialize time series chart from Core layer samples (PlayerStatistics)
    /// </summary>
    private void InitializeTimeSeriesFromCore(IReadOnlyList<DpsDataPoint> samples, PlotViewModel target)
    {
        target.LineSeriesData.Points.Clear();
        foreach (var sample in samples)
        {
            target.LineSeriesData.Points.Add(new DataPoint(sample.Time.TotalSeconds, sample.Value));
        }
        target.RefreshSeries();
    }

    /// <summary>
    /// LEGACY: Initialize time series from ViewModel ObservableCollection (backward compatibility)
    /// </summary>
    private void InitializeTimeSeries(ObservableCollection<(TimeSpan time, double value)> data,
        PlotViewModel target)
    {
        RefreshTimeSeries(target, data);
    }

    private void RefreshTimeSeries(PlotViewModel target, ICollection<(TimeSpan time, double value)> data)
    {
        target.LineSeriesData.Points.Clear();
        foreach (var (time, value) in data)
        {
            target.LineSeriesData.Points.Add(new DataPoint(time.TotalSeconds, value));
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
    /// Update pie chart directly with skill list (no event subscription)
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

        // Update ChartViewModel properties for the new UI
        // We need to access the corresponding TabContentViewModel's ChartViewModel
        // But here we only have PlotViewModel target.
        // We should update the TabContentViewModel instead.
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
        if (_playerStatistics == null)
        {
            _logger.LogDebug("PlayerStatistic is null, refresh abort, return");
            return;
        }
        var duration = TimeSpan.FromTicks(_playerStatistics.LastTick - (_playerStatistics.StartTick ?? 0));

        // Build skill lists from PlayerStatistics (no battle log iteration!)
        var (damageSkills, healingSkills, takenSkills) =
            StatisticsToViewModelConverter.BuildSkillListsFromPlayerStats(_playerStatistics);

        DamageStats = _playerStatistics.AttackDamage.ToDataStatistics(duration);
        HealingStats = _playerStatistics.Healing.ToDataStatistics(duration);
        TakenDamageStats = _playerStatistics.TakenDamage.ToDataStatistics(duration);

        // ⭐ Populate Skills collections
        DamageStats.Skills.Clear();
        foreach (var skill in damageSkills)
        {
            DamageStats.Skills.Add(skill);
        }
        HealingStats.Skills.Clear();
        foreach (var skill in healingSkills)
        {
            HealingStats.Skills.Add(skill);
        }
        TakenDamageStats.Skills.Clear();
        foreach (var skill in takenSkills)
        {
            TakenDamageStats.Skills.Add(skill);
        }

        // ⭐ Refresh time series from Core layer
        InitializeTimeSeriesFromCore(_playerStatistics.GetDpsSamples(), DpsPlot);
        InitializeTimeSeriesFromCore(_playerStatistics.GetHpsSamples(), HpsPlot);
        InitializeTimeSeriesFromCore(_playerStatistics.GetDtpsSamples(), DtpsPlot);

        // Use accurate skill lists from PlayerStatistics
        UpdatePieChartDirect(damageSkills, DpsPlot);
        UpdatePieChartDirect(healingSkills, HpsPlot);
        UpdatePieChartDirect(takenSkills, DtpsPlot);

        UpdateHitTypeDistribution(DamageStats, DpsPlot);
        UpdateHitTypeDistribution(HealingStats, HpsPlot);
        UpdateHitTypeDistribution(TakenDamageStats, DtpsPlot);
        _logger.LogDebug("Manual refresh");
    }

    #endregion
}