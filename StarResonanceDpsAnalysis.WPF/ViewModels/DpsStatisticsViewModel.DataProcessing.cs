using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Data processing methods partial class for DpsStatisticsViewModel
/// Contains methods for updating and processing DPS data
/// Now uses ICombatSectionStateManager and ITeamStatsUIManager for SOLID compliance
/// </summary>
public partial class DpsStatisticsViewModel
{
    // ★ここに1箇所だけ：最新 processed をタブ切替でも使えるようにキャッシュ
    private readonly Dictionary<StatisticType, IReadOnlyDictionary<long, DpsDataProcessed>> _latestProcessedByType = new();
    private static readonly IReadOnlyDictionary<long, DpsDataProcessed> EmptyProcessed = new Dictionary<long, DpsDataProcessed>();

    // Configuration.cs から呼べるように（partial 同一クラス内なので private でOK）
    private void ClearProcessedCache() => _latestProcessedByType.Clear();

    protected void UpdateData()
    {
        _logger.LogTrace("Update data");
        _dataSourceEngine.CurrentSource.Refresh();
    }

    // 互換用（もし他で呼んでたら壊さない）
    private void UpdateTeamTotalStats(IReadOnlyDictionary<long, DpsDataProcessed> data)
    {
        var playerInfoDict = _dataSourceEngine.GetPlayerInfoDictionary();
        UpdateTeamTotalStats(StatisticIndex, data, playerInfoDict);
    }

    private void PublishEmptyTeamTotal(StatisticType type)
    {
        var emptyStats = _dataProcessor.CalculateTeamTotal(EmptyProcessed);
        _teamStatsManager.UpdateTeamStats(emptyStats, type, false);

        TeamTotalDamage = 0;
        TeamTotalDps = 0;
    }

    private IReadOnlyDictionary<long, DpsDataProcessed>? GetCurrentTypeProcessedDict(StatisticType type)
    {
        return _latestProcessedByType.TryGetValue(type, out var dict) ? dict : null;
    }

    private void UpdateTeamTotalStats(
        StatisticType type,
        IReadOnlyDictionary<long, DpsDataProcessed> data,
        IReadOnlyDictionary<long, PlayerInfo> playerInfoDict)
    {
        // “計測から除外” を反映（元コードの挙動を維持）
        var excludeSpecial = !IsIncludeNpcData;

        IReadOnlyDictionary<long, DpsDataProcessed> totalDict = data;

        if (excludeSpecial && data.Count > 0)
        {
            // 除外対象が存在するか確認 → ある時だけ新Dictionaryを作る（普段は割当ゼロ）
            var anyExcluded = false;
            foreach (var uid in data.Keys)
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
                var filtered = new Dictionary<long, DpsDataProcessed>(data.Count);
                foreach (var kv in data)
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
        _teamStatsManager.UpdateTeamStats(teamStats, type, totalDict.Count > 0);

        // 念のため同期（TeamStatsUpdatedイベントでも更新されるが、即反映もできる）
        TeamTotalDamage = teamStats.TotalValue;
        TeamTotalDps = teamStats.TotalDps;
    }

    // ★タブ切替で呼ぶ：キャッシュから再計算してTeamTotalを切り替える
    private void RecalculateAndPublishTeamTotalFor(StatisticType type)
    {
        if (!ShowTeamTotalDamage)
        {
            _teamStatsManager.ResetTeamStats();
            TeamTotalDamage = 0;
            TeamTotalDps = 0;
            return;
        }

        var playerInfoDict = _dataSourceEngine.GetPlayerInfoDictionary();
        var dict = GetCurrentTypeProcessedDict(type);

        if (dict is null)
        {
            PublishEmptyTeamTotal(type);
            return;
        }

        UpdateTeamTotalStats(type, dict, playerInfoDict);
    }

    private void UpdateBattleDuration()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            BattleDuration = _dataSourceEngine.CurrentSource.BattleDuration;
            _logger.LogTrace("Update battle duration: {duration}", BattleDuration);
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
        InvokeOnDispatcher(Action);
        return;

        void Action()
        {
            var currentPlayerUid = _storage.CurrentPlayerInfo.UID > 0
                ? _storage.CurrentPlayerInfo.UID
                : _configManager.CurrentConfig.Uid;

            // 先に一回だけ取得
            var playerInfoDict = _dataSourceEngine.GetPlayerInfoDictionary();

            // SubViewModel 更新
            foreach (var (statisticType, processed) in processedByType)
            {
                if (!StatisticData.TryGetValue(statisticType, out var subViewModel)) continue;
                subViewModel.ScopeTime = ScopeTime;

                subViewModel.UpdateDataOptimized(processed, currentPlayerUid);
            }

            // ★重要：最新 processed をキャッシュ（タブ切替でも使う）
            _latestProcessedByType.Clear();
            foreach (var (type, dict) in processedByType)
                _latestProcessedByType[type] = dict;

            // ★現在タブの TeamTotal を更新（Damage固定をやめる）
            RecalculateAndPublishTeamTotalFor(StatisticIndex);
        }
    }
}