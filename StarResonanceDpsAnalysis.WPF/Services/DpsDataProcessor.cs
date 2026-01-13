using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Implementation of DPS data processor
/// </summary>
public class DpsDataProcessor : IDpsDataProcessor
{
    private readonly ILogger<DpsDataProcessor> _logger;

    public DpsDataProcessor(ILogger<DpsDataProcessor> logger)
    {
        _logger = logger;
    }

    public Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> PreProcessData(
        IReadOnlyDictionary<long, PlayerStatistics> data,
        bool includeNpcData)
    {
        var result = new Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>>
        {
            [StatisticType.Damage] = new(),
            [StatisticType.Healing] = new(),
            [StatisticType.TakenDamage] = new(),
            [StatisticType.NpcTakenDamage] = new()
        };

        foreach (var playerStats in data.Values)
        {
            var durationTicks = playerStats.ElapsedTicks();

            // Process Damage
            var damageValue = (ulong)Math.Max(0, playerStats.AttackDamage.Total);
            if (damageValue > 0)
            {
                var shouldShowInDamageList = !playerStats.IsNpc || includeNpcData;
                if (shouldShowInDamageList)
                {
                    result[StatisticType.Damage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, damageValue, durationTicks, playerStats.Uid);
                }
            }

            // Process Healing - only players
            var healingValue = (ulong)Math.Max(0, playerStats.Healing.Total);
            if (healingValue > 0 && !playerStats.IsNpc)
            {
                result[StatisticType.Healing][playerStats.Uid] = new DpsDataProcessed(
                    playerStats, healingValue, durationTicks, playerStats.Uid);
            }

            // Process TakenDamage
            var takenDamageValue = (ulong)Math.Max(0, playerStats.TakenDamage.Total);
            if (takenDamageValue > 0)
            {
                if (playerStats.IsNpc)
                {
                    result[StatisticType.NpcTakenDamage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, takenDamageValue, durationTicks, playerStats.Uid);
                }
                else
                {
                    result[StatisticType.TakenDamage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, takenDamageValue, durationTicks, playerStats.Uid);
                }
            }
        }

        return result;
    }

    public TeamTotalStats CalculateTeamTotal(
        IReadOnlyDictionary<long, PlayerStatistics> data,
        StatisticType statisticType)
    {
        ulong totalValue = 0;
        double maxDuration = 0;
        var playerCount = 0;
        var npcCount = 0;

        foreach (var dpsData in data.Values)
        {
            // Skip based on statistic type
            if (dpsData.IsNpc && statisticType != StatisticType.NpcTakenDamage)
                continue;
            if (!dpsData.IsNpc && statisticType == StatisticType.NpcTakenDamage)
                continue;

            if (dpsData.IsNpc) npcCount++;
            else playerCount++;

            ulong value = statisticType switch
            {
                StatisticType.Damage => (ulong)Math.Max(0, dpsData.AttackDamage.Total),
                StatisticType.Healing => (ulong)Math.Max(0, dpsData.Healing.Total),
                StatisticType.TakenDamage => (ulong)Math.Max(0, dpsData.TakenDamage.Total),
                StatisticType.NpcTakenDamage => (ulong)Math.Max(0, dpsData.TakenDamage.Total),
                _ => 0
            };

            totalValue += value;

            var elapsedTicks = dpsData.ElapsedTicks();
            if (elapsedTicks > 0)
            {
                var elapsedSeconds = TimeSpan.FromTicks(elapsedTicks).TotalSeconds;
                if (elapsedSeconds > maxDuration)
                    maxDuration = elapsedSeconds;
            }
        }

        var totalDps = maxDuration > 0 ? totalValue / maxDuration : 0;
        return new TeamTotalStats(totalValue, totalDps, playerCount, npcCount, maxDuration);
    }
}