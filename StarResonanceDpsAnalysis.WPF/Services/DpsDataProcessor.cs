using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

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

    public StatisticDictionary PreProcessData(
        IReadOnlyDictionary<long, PlayerStatistics> data,
        bool includeNpcData)
    {
        var result = new StatisticDictionary();

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
                        playerStats, damageValue, durationTicks, playerStats.Uid, playerStats.AttackDamage.ValuePerSecond);
                }
            }

            // Process Healing - only players
            var healingValue = (ulong)Math.Max(0, playerStats.Healing.Total);
            if (healingValue > 0 && !playerStats.IsNpc)
            {
                result[StatisticType.Healing][playerStats.Uid] = new DpsDataProcessed(
                    playerStats, healingValue, durationTicks, playerStats.Uid, playerStats.Healing.ValuePerSecond);
            }

            // Process TakenDamage
            var takenDamageValue = (ulong)Math.Max(0, playerStats.TakenDamage.Total);
            if (takenDamageValue > 0)
            {
                if (playerStats.IsNpc)
                {
                    result[StatisticType.NpcTakenDamage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, takenDamageValue, durationTicks, playerStats.Uid, playerStats.TakenDamage.ValuePerSecond);
                }
                else
                {
                    result[StatisticType.TakenDamage][playerStats.Uid] = new DpsDataProcessed(
                        playerStats, takenDamageValue, durationTicks, playerStats.Uid, playerStats.TakenDamage.ValuePerSecond);
                }
            }
        }

        return result;
    }

    public TeamTotalStats CalculateTeamTotal(IReadOnlyDictionary<long, DpsDataProcessed> data)
    {
        ulong totalValue = 0;
        double maxDuration = 0;
        var playerCount = 0;
        var npcCount = 0;

        foreach (var d in data.Values)
        {
            var dpsData = d.OriginalData;
            if (dpsData.IsNpc) npcCount++;
            else playerCount++;

            totalValue += d.Value;

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