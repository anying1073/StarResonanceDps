using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Data processing methods partial class for DpsStatisticsViewModel
/// Contains methods for updating and processing DPS data
/// Now uses ICombatSectionStateManager and ITeamStatsUIManager for SOLID compliance
/// </summary>
public partial class DpsStatisticsViewModel
{
    protected void UpdateData()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger.LogTrace("DataStorage_DpsDataUpdated called");

            var stat = _storage.GetStatistics(ScopeTime == ScopeTime.Total);

            if (!_timerService.IsRunning && HasData(stat))
            {
                _logger.LogInformation("检测到战斗数据,启动计时器 (using DpsTimerService)");
                _timerService.Start();
                _combatState.SectionTimedOut = false;
            }

            if (_combatState.AwaitingSectionStart)
            {
                var hasSectionDamage = HasData(_storage.GetStatistics(false));
                _logger.LogDebug("Awaiting section start - has section damage: {HasSectionDamage}", hasSectionDamage);

                if (hasSectionDamage)
                {
                    foreach (var subVm in StatisticData.Values)
                    {
                        subVm.Reset();
                    }

                    _timerService.StartNewSection();
                    _combatState.MarkSectionStarted();
                    _logger.LogDebug("Section start processed, new section begins");
                }
            }

            UpdateData(stat);
            UpdateBattleDuration();
        }
    }

    private void UpdateData(IReadOnlyDictionary<long, PlayerStatistics> data)
    {
        _logger.LogTrace(WpfLogEvents.VmUpdateData, "Update data requested: {Count} entries", data.Count);

        var currentPlayerUid = _storage.CurrentPlayerUUID > 0 ? _storage.CurrentPlayerUUID : _configManager.CurrentConfig.Uid;

        var processedDataByType = _dataProcessor.PreProcessData(data, IsIncludeNpcData);

        foreach (var (statisticType, processedData) in processedDataByType)
        {
            if (!StatisticData.TryGetValue(statisticType, out var subViewModel)) continue;
            subViewModel.ScopeTime = ScopeTime;
            subViewModel.UpdateDataOptimized(processedData, currentPlayerUid);
        }

        UpdateTeamTotalStats(data);
    }

    private void UpdateTeamTotalStats(IReadOnlyDictionary<long, PlayerStatistics> data)
    {
        // Delegate to TeamStatsUIManager following Single Responsibility Principle
        var teamStats = _dataProcessor.CalculateTeamTotal(data, StatisticIndex);
        _teamStatsManager.UpdateTeamStats(teamStats, StatisticIndex, data.Count > 0);
    }

    private void UpdateBattleDuration()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            if (!_timerService.IsRunning) return;

            if (ScopeTime == ScopeTime.Current)
            {
                if (_combatState.AwaitingSectionStart)
                {
                    BattleDuration = _combatState.LastSectionElapsed;
                    return;
                }

                if (_combatState.SectionTimedOut && _combatState.LastSectionElapsed > TimeSpan.Zero)
                {
                    BattleDuration = _combatState.LastSectionElapsed;
                    return;
                }

                // Use timer service for section elapsed
                BattleDuration = _timerService.GetSectionElapsed();
            }
            else // ScopeTime.Total
            {
                if (_combatState.AwaitingSectionStart)
                {
                    BattleDuration = _combatState.TotalCombatDuration;
                }
                else
                {
                    var currentSectionDuration = _timerService.GetSectionElapsed();
                    BattleDuration = _combatState.TotalCombatDuration + currentSectionDuration;
                }
            }
        }
    }

    private void RefreshData()
    {
        var stat = _storage.GetStatistics(ScopeTime == ScopeTime.Total);
        UpdateData(stat);
    }

    private bool HasData(IReadOnlyDictionary<long, PlayerStatistics> stats)
    {
        return stats.Count > 0;
    }
}
