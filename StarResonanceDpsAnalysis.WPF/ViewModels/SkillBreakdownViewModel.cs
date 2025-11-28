using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using StarResonanceDpsAnalysis.WPF.Extensions;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for the skill breakdown view, showing detailed statistics for a player.
/// </summary>
public partial class SkillBreakdownViewModel(ILogger<SkillBreakdownViewModel> logger) : BaseViewModel
{
    /// <summary>
    /// Initializes the ViewModel from a <see cref="StatisticDataViewModel"/>.
    /// </summary>
    public void InitializeFrom(StatisticDataViewModel slot)
    {
        logger.LogDebug("Initializing SkillBreakdownViewModel from StatisticDataViewModel for player {PlayerName}",
            slot.Player.Name);

        ObservedSlot = slot;

        // Player Info
        PlayerName = slot.Player?.Name ?? "Unknown";
        Uid = slot.Player?.Uid ?? 0;
        PowerLevel = slot.Player?.PowerLevel ?? 0;

        var duration = slot.Duration > 0 ? slot.Duration : 1;

        // Calculate statistics from skills
        DamageStats = slot.Damage.TotalSkillList.FromSkillsToDamage(duration);
        HealingStats = slot.Heal.TotalSkillList.FromSkillsToHealing(duration);
        TakenDamageStats = slot.TakenDamage.TotalSkillList.FromSkillsToDamageTaken(duration);

        // Initialize Chart Data
        InitializeTimeSeries(slot.Damage.Dps, DpsPlot);
        InitializeTimeSeries(slot.Heal.Dps, HpsPlot);
        InitializeTimeSeries(slot.TakenDamage.Dps, DtpsPlot);

        UpdatePieChart(slot.Damage.TotalSkillList, DpsPlot);
        UpdatePieChart(slot.Heal.TotalSkillList, HpsPlot);
        UpdatePieChart(slot.TakenDamage.TotalSkillList, DtpsPlot);

        logger.LogDebug("SkillBreakdownViewModel initialized for player: {PlayerName}", PlayerName);
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
        var duration = ObservedSlot.Duration > 0 ? ObservedSlot.Duration : 1;
        skills.UpdateDamage(duration, DamageStats);
    }

    private void HealSkillChanged(IReadOnlyList<SkillItemViewModel>? skills)
    {
        if (ObservedSlot is null) return;
        if (skills is null) return;
        var duration = ObservedSlot.Duration > 0 ? ObservedSlot.Duration : 1;
        skills.UpdateHealing(duration, HealingStats);
    }

    private void TakenDamageSkillChanged(IReadOnlyList<SkillItemViewModel>? skills)
    {
        if (ObservedSlot is null) return;
        if (skills is null) return;
        var duration = ObservedSlot.Duration > 0 ? ObservedSlot.Duration : 1;
        skills.UpdateDamageTaken(duration, TakenDamageStats);
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

    [ObservableProperty] private PlotViewModel _dpsPlot = new(new PlotOptions { YAxisTitle = "Time (s)" });

    [ObservableProperty] private PlotViewModel _hpsPlot = new(new PlotOptions { YAxisTitle = "Time (s)" });

    [ObservableProperty] private PlotViewModel _dtpsPlot = new(new PlotOptions { YAxisTitle = "Time (s)" });

    #endregion

    #region Zoom State

    [ObservableProperty] private double _zoomLevel = 1.0;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 0.2;

    #endregion

    #region Chart Initialization

    private void InitializeTimeSeries(ObservableCollection<(TimeSpan duration, double section, double total)> data,
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

        foreach (var (duration, section, _) in data)
        {
            target.LineSeriesData.Points.Add(new DataPoint(duration.TotalSeconds, section));
        }

        target.RefreshSeries();
    }

    private static void UpdatePieChart(IReadOnlyList<SkillItemViewModel> skills, PlotViewModel target)
    {
        target.SetPieSeriesData(skills);
    }

    #endregion

    #region Zoom Commands

    [RelayCommand]
    private void ZoomIn()
    {
        if (ZoomLevel >= MaxZoom) return;
        ZoomLevel += ZoomStep;
        UpdateAllChartZooms();
        logger.LogDebug("Zoomed in to {ZoomLevel}", ZoomLevel);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (ZoomLevel <= MinZoom) return;
        ZoomLevel -= ZoomStep;
        UpdateAllChartZooms();
        logger.LogDebug("Zoomed out to {ZoomLevel}", ZoomLevel);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        ResetAllChartZooms();
        logger.LogDebug("Zoom reset to default");
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
        logger.LogDebug("Confirm SkillBreakDown");
    }

    [RelayCommand]
    private void Cancel()
    {
        logger.LogDebug("Cancel SkillBreakDown");
    }

    #endregion
}