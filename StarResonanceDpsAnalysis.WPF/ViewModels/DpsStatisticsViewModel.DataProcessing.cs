using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Models;

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
        _dataSourceEngine.DeliverProcessedData();
    }

    private void UpdateTeamTotalStats(IReadOnlyDictionary<long, DpsDataProcessed> data)
    {
        // Delegate to TeamStatsUIManager following Single Responsibility Principle
        var teamStats = _dataProcessor.CalculateTeamTotal(data);
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
                // Use timer service for section elapsed
                BattleDuration = _timerService.GetSectionElapsed();
            }
            else // ScopeTime.Total
            {
                BattleDuration = _timerService.TotalCombatDuration;
            }
        }
    }

    /// <summary>
    /// Apply processed data prepared by providers/engine to sub-viewmodels and team totals.
    /// This centralizes UI update logic when providers pre-process data.
    /// </summary>
    private void ApplyProcessedData(Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> processedByType)
    {
        InvokeOnDispatcher(Action);
        return;

        // Ensure UI updates on dispatcher
        void Action()
        {
            var currentPlayerUid = _storage.CurrentPlayerUUID > 0 ? _storage.CurrentPlayerUUID : _configManager.CurrentConfig.Uid;

            // Apply processed data to each sub viewmodel
            foreach (var (statisticType, processed) in processedByType)
            {
                if (!StatisticData.TryGetValue(statisticType, out var subViewModel)) continue;
                subViewModel.ScopeTime = ScopeTime;
                subViewModel.UpdateDataOptimized(processed, currentPlayerUid);
            }

            // Update team totals
            var teamStats = _dataProcessor.CalculateTeamTotal(processedByType[StatisticIndex]);
            _teamStatsManager.UpdateTeamStats(teamStats, StatisticIndex, processedByType.Count > 0);

            // Update duration
            UpdateBattleDuration();
        }

    }
}

