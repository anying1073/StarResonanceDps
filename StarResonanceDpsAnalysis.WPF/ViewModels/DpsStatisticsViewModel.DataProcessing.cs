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
        InvokeOnDispatcher(() => BattleDuration = _dataSourceEngine.CurrentSource.BattleDuration);
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
        InvokeOnDispatcher(Action);
        return;

        void Action()
        {
            var currentPlayerUid = _storage.CurrentPlayerInfo.UID > 0
                ? _storage.CurrentPlayerInfo.UID
                : _configManager.CurrentConfig.Uid;

            // 先に一回だけ取得
            var playerInfoDict = _dataSourceEngine.GetPlayerInfoDictionary();
            var excludeSpecial = !IsIncludeNpcData;

            foreach (var (statisticType, processed) in processedByType)
            {
                if (!StatisticData.TryGetValue(statisticType, out var subViewModel)) continue;
                subViewModel.ScopeTime = ScopeTime;

                // SubViewModel 側で除外される（UpdateDataOptimized内）
                subViewModel.UpdateDataOptimized(processed, currentPlayerUid);
            }

            // TeamTotal も “計測から除外” を反映
            if (!processedByType.TryGetValue(StatisticIndex, out var currentTypeProcessed))
            {
                var emptyStats = _dataProcessor.CalculateTeamTotal(new Dictionary<long, DpsDataProcessed>());
                _teamStatsManager.UpdateTeamStats(emptyStats, StatisticIndex, false);
                return;
            }

            Dictionary<long, DpsDataProcessed> totalDict = currentTypeProcessed;

            if (excludeSpecial && currentTypeProcessed.Count > 0)
            {
                // 除外対象が存在するか確認 → ある時だけ新Dictionaryを作る（普段は割当ゼロ）
                var anyExcluded = false;
                foreach (var uid in currentTypeProcessed.Keys)
                {
                    if (playerInfoDict.TryGetValue(uid, out var info) &&
                        PlayerInfoViewModel.IsSpecialNpcChineseName(info?.Name))
                    {
                        anyExcluded = true;
                        break;
                    }
                }

                if (anyExcluded)
                {
                    var filtered = new Dictionary<long, DpsDataProcessed>(currentTypeProcessed.Count);
                    foreach (var kv in currentTypeProcessed)
                    {
                        if (playerInfoDict.TryGetValue(kv.Key, out var info) &&
                            PlayerInfoViewModel.IsSpecialNpcChineseName(info?.Name))
                            continue;

                        filtered[kv.Key] = kv.Value;
                    }

                    totalDict = filtered;
                }
            }

            var teamStats = _dataProcessor.CalculateTeamTotal(totalDict);
            _teamStatsManager.UpdateTeamStats(teamStats, StatisticIndex, totalDict.Count > 0);
        }
    }
}

