using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
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

public partial class SkillBreakdownViewModel : BaseViewModel, IDisposable
{
    private readonly ILogger<SkillBreakdownViewModel> _logger;
    private readonly LocalizationManager _localizationManager;
    private readonly IDataStorage _storage;
    private readonly Config.IConfigManager _configManager;

    // Small UI-side throttle layer (like old cache-service idea, but only for the opened player)
    private readonly DispatcherTimer _liveRefreshTimer;
    private bool _pendingLiveRefresh;

    private const int LiveUiRefreshIntervalMs = 1000;

    private bool _suppressFreezeOnSelfClear;

    [ObservableProperty] private StatisticType _statisticIndex;
    [ObservableProperty] private Config.AppConfig _appConfig;

    [ObservableProperty] private TabContentViewModel _dpsTabViewModel;
    [ObservableProperty] private TabContentViewModel _healingTabViewModel;
    [ObservableProperty] private TabContentViewModel _tankingTabViewModel;

    [ObservableProperty] private string _playerName = string.Empty;
    [ObservableProperty] private long _uid;
    [ObservableProperty] private long _powerLevel;

    [ObservableProperty] private double _zoomLevel = 1.0;

    private const double MinZoom = 0.5;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 0.2;

    private PlayerStatistics? _playerStatistics;

    // true: current live storage instance, should keep updating
    // false: detached replay/history snapshot, should stay frozen
    private bool _isLiveSource;

    private ScopeTime _scopeTime = ScopeTime.Current;

    // Stored only for frozen/history rebuild
    private List<BattleLog> _fixedReplayLogs = new();

    private int TimeSeriesPointCapacity => Math.Clamp(AppConfig.TimeSeriesSampleCapacity, 50, 1000);

    public SkillBreakdownViewModel(
        ILogger<SkillBreakdownViewModel> logger,
        LocalizationManager localizationManager,
        IDataStorage storage,
        Config.IConfigManager configManager)
    {
        _logger = logger;
        _localizationManager = localizationManager;
        _storage = storage;
        _configManager = configManager;
        _appConfig = configManager.CurrentConfig;

        var xAxis = GetXAxisName();
        _dpsTabViewModel = new TabContentViewModel(CreatePlotViewModel(xAxis, StatisticType.Damage));
        _healingTabViewModel = new TabContentViewModel(CreatePlotViewModel(xAxis, StatisticType.Healing));
        _tankingTabViewModel = new TabContentViewModel(CreatePlotViewModel(xAxis, StatisticType.TakenDamage));

        _dpsTabViewModel.Plot.DamageDisplayMode = _appConfig.DamageDisplayType;
        _healingTabViewModel.Plot.DamageDisplayMode = _appConfig.DamageDisplayType;
        _tankingTabViewModel.Plot.DamageDisplayMode = _appConfig.DamageDisplayType;

        _liveRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(LiveUiRefreshIntervalMs)
        };
        _liveRefreshTimer.Tick += OnLiveRefreshTimerTick;
        _liveRefreshTimer.Start();

        // Live updates: no DataStorage re-read on every event
        _storage.DpsDataUpdated += OnStorageDpsDataUpdated;

        // Freeze current live display right before section/manual clear
        _storage.BeforeSectionCleared += OnBeforeSectionCleared;

        _configManager.ConfigurationUpdated += OnConfigurationUpdated;
    }

    public void InitializeFrom(
        PlayerStatistics playerStats,
        PlayerInfo? playerInfo,
        StatisticType statisticType,
        IReadOnlyList<BattleLog>? battleLogs = null,
        ScopeTime scopeTime = ScopeTime.Current)
    {
        _scopeTime = scopeTime;
        _playerStatistics = playerStats;
        _isLiveSource = IsCurrentStorageInstance(playerStats, scopeTime);

        _fixedReplayLogs = battleLogs?.ToList() ?? new List<BattleLog>();
        _pendingLiveRefresh = false;

        UpdatePlayerInfo(playerStats, playerInfo);
        StatisticIndex = statisticType;

        if (_isLiveSource)
        {
            // Live: use direct reference, redraw immediately once.
            RefreshAllStatistics();
        }
        else
        {
            // Frozen/history path: rebuild exactly once from logs if available.
            if (!TryRefreshFromReplayLogs(_fixedReplayLogs))
            {
                RefreshAllStatistics();
            }
        }

        _logger.LogDebug(
            "SkillBreakdown initialized: UID={Uid}, Live={Live}, Scope={Scope}, ReplayLogs={Count}",
            playerStats.Uid,
            _isLiveSource,
            _scopeTime,
            _fixedReplayLogs.Count);
    }

    private bool IsCurrentStorageInstance(PlayerStatistics playerStats, ScopeTime scopeTime)
    {
        // This is only called on window-open, so the one-time DataStorage access here is fine.
        var liveStats = _storage.GetStatistics(fullSession: scopeTime == ScopeTime.Total);
        return liveStats.TryGetValue(playerStats.Uid, out var currentLiveRef)
               && ReferenceEquals(currentLiveRef, playerStats);
    }

    /// <summary>
    /// High-frequency event path:
    /// just mark dirty. Actual redraw is throttled by _liveRefreshTimer.
    /// </summary>
    private void OnStorageDpsDataUpdated()
    {
        if (!_isLiveSource)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(OnStorageDpsDataUpdated));
            return;
        }

        _pendingLiveRefresh = true;
    }

    /// <summary>
    /// Low-frequency coalesced redraw.
    /// This is the small "cache layer" that restores old behavior style.
    /// </summary>
    private void OnLiveRefreshTimerTick(object? sender, EventArgs e)
    {
        if (!_pendingLiveRefresh || !_isLiveSource)
        {
            return;
        }

        _pendingLiveRefresh = false;

        try
        {
            if (!TryRebindLivePlayerStatistics())
            {
                ClearAllStatistics();
                return;
            }

            RefreshAllStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying throttled live refresh in SkillBreakdown");
        }
    }

    /// <summary>
    /// Freeze current live view right before section/manual clear.
    /// This preserves the last visible state after save/clear.
    /// </summary>
    private void OnBeforeSectionCleared()
    {
        if (_suppressFreezeOnSelfClear)
        {
            return;
        }

        if (!_isLiveSource || _playerStatistics == null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(OnBeforeSectionCleared));
            return;
        }

        try
        {
            _pendingLiveRefresh = false;

            var logs = _storage.GetBattleLogsForPlayer(_playerStatistics.Uid, _scopeTime == ScopeTime.Total);
            if (logs.Count == 0)
            {
                return;
            }

            var frozen = SkillBreakdownReplayBuilder.BuildForPlayer(
                _playerStatistics.Uid,
                logs,
                Math.Max(1, _storage.SampleRecordingInterval),
                TimeSeriesPointCapacity);

            if (frozen == null)
            {
                return;
            }

            _fixedReplayLogs = logs.ToList();
            _playerStatistics = frozen;
            _isLiveSource = false;

            RefreshAllStatistics();

            _logger.LogDebug("SkillBreakdown frozen before clear for UID {Uid}", _playerStatistics.Uid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error freezing SkillBreakdown before clear");
        }
    }

    private void OnConfigurationUpdated(object? sender, Config.AppConfig newConfig)
    {
        AppConfig = newConfig;

        if (_playerStatistics == null)
        {
            return;
        }

        if (_isLiveSource)
        {
            // Live: redraw from current live ref only.
            RefreshAllStatistics();
            return;
        }

        // Frozen/history: rebuild from stored logs to respect new point capacity, etc.
        if (!TryRefreshFromReplayLogs(_fixedReplayLogs))
        {
            RefreshAllStatistics();
        }
    }

    private bool TryRebindLivePlayerStatistics()
    {
        var liveStats = _storage.GetStatistics(fullSession: _scopeTime == ScopeTime.Total);

        if (liveStats.TryGetValue(Uid, out var currentLiveRef))
        {
            _playerStatistics = currentLiveRef;
            return true;
        }

        _playerStatistics = null;
        return false;
    }

    private bool TryRefreshFromReplayLogs(IReadOnlyList<BattleLog>? logs)
    {
        if (_playerStatistics == null || logs == null || logs.Count == 0)
        {
            return false;
        }

        var rebuilt = SkillBreakdownReplayBuilder.BuildForPlayer(
            _playerStatistics.Uid,
            logs,
            Math.Max(1, _storage.SampleRecordingInterval),
            TimeSeriesPointCapacity);

        if (rebuilt == null)
        {
            return false;
        }

        _playerStatistics = rebuilt;
        RefreshAllStatistics();
        return true;
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
        target.RefreshSeries();
    }

    private void RefreshAllStatistics()
    {
        if (_playerStatistics == null)
        {
            return;
        }

        var duration = TimeSpan.FromTicks(Math.Max(0, _playerStatistics.LastTick - (_playerStatistics.StartTick ?? 0)));
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
            xAxis.Minimum = Math.Max(0, oldestX - 1.0);
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

    private string GetXAxisName()
    {
        return _localizationManager.GetString(ResourcesKeys.SkillBreakdown_Chart_DpsSeriesXAxis);
    }

    [RelayCommand]
    private void ZoomIn()
    {
        if (ZoomLevel >= MaxZoom) return;
        ZoomLevel += ZoomStep;
        ApplyZoomToAllCharts();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (ZoomLevel <= MinZoom) return;
        ZoomLevel -= ZoomStep;
        ApplyZoomToAllCharts();
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        ResetAllChartZooms();
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

    [RelayCommand]
    private void Refresh()
    {
        _pendingLiveRefresh = false;

        // history / frozen 表示ならローカル表示だけ消す
        if (!_isLiveSource)
        {
            ClearAllStatistics();
            return;
        }

        _suppressFreezeOnSelfClear = true;
        try
        {
            if (_scopeTime == ScopeTime.Total)
            {
                _storage.ClearAllDpsData();
            }
            else
            {
                _storage.ClearDpsData();
            }
        }
        finally
        {
            _suppressFreezeOnSelfClear = false;
        }

        // 旧参照を捨てる。次の live 更新で storage から再bind する
        _playerStatistics = null;
        _fixedReplayLogs.Clear();
        ClearAllStatistics();
    }

    public void Dispose()
    {
        _liveRefreshTimer.Stop();
        _liveRefreshTimer.Tick -= OnLiveRefreshTimerTick;

        _storage.DpsDataUpdated -= OnStorageDpsDataUpdated;
        _storage.BeforeSectionCleared -= OnBeforeSectionCleared;
        _configManager.ConfigurationUpdated -= OnConfigurationUpdated;
    }
}