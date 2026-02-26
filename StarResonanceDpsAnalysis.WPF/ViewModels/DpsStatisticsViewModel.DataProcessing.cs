using Microsoft.Extensions.Logging;
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
        _logger.LogTrace("Update data");
        _dataSourceEngine.CurrentSource.Refresh();
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
            BattleDuration = _dataSourceEngine.CurrentSource.BattleDuration;
            _logger.LogTrace("Update battle duration: {duration}", BattleDuration);
            //if (!_timerService.IsRunning) return;

            //if (ScopeTime == ScopeTime.Current)
            //{
            //    // Use timer service for section elapsed
            //    BattleDuration = _timerService.SectionDuration;
            //}
            //else // ScopeTime.Total
            //{
            //    BattleDuration = _timerService.TotalDuration;
            //}
        }
    }

    private void ResetBattleDurationIfInCurrentScope()
    {
        if (ScopeTime != ScopeTime.Current) return;
        InvokeOnDispatcher(() => BattleDuration = TimeSpan.Zero);
    }

    /// <summary>
    /// Apply processed data prepared by providers/engine to sub-viewmodels and team totals.
    /// This centralizes UI update logic when providers pre-process data.
    /// </summary>
    private void ApplyProcessedData(object? sender, Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> processedByType)
    {
        //_logger.LogTrace("ApplyProcessedData, sender:{senderType}", sender?.GetType());
        //_logger.LogTrace("StatisticData.Damage.Count1:{Count}", StatisticData[StatisticType.Damage].Data.Count);
        InvokeOnDispatcher(Action);
        //_logger.LogTrace("StatisticData.Damage.Count2:{Count}", StatisticData[StatisticType.Damage].Data.Count);
        return;

        // Ensure UI updates on dispatcher
        void Action()
        {
            var currentPlayerUid = _storage.CurrentPlayerInfo.UID > 0 ? _storage.CurrentPlayerInfo.UID : _configManager.CurrentConfig.Uid;

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
        }
    }
}

