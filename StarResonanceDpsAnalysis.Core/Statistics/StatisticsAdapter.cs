using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics.Calculators;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Adapter that converts new statistics format to legacy DpsData format
/// Allows gradual migration without breaking existing code
/// </summary>
public sealed class StatisticsAdapter
{
    private readonly StatisticsEngine _engine;
    private readonly ILogger? _logger;

    public StatisticsAdapter(ILogger? logger = null)
    {
        _logger = logger;
        _engine = new StatisticsEngine();

        // Register all calculators (OCP: easily add new ones)
        _engine.RegisterCalculator(new AttackDamageCalculator());
        _engine.RegisterCalculator(new TakenDamageCalculator());
        _engine.RegisterCalculator(new HealingCalculator());
    }

    /// <summary>
    /// Process a battle log
    /// </summary>
    public void ProcessLog(BattleLog log)
    {
        try
        {
            _engine.ProcessBattleLog(log);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing battle log");
        }
    }

    /// <summary>
    /// Reset section statistics and battle logs
    /// </summary>
    public void ResetSection()
    {
        _engine.ResetSection();
    }

    /// <summary>
    /// Clear all statistics and battle logs (both full and section)
    /// </summary>
    public void ClearAll()
    {
        _engine.ClearAll();
    }

    /// <summary>
    /// Get battle logs
    /// </summary>
    public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession)
    {
        return fullSession
            ? _engine.GetFullBattleLogs()
            : _engine.GetSectionBattleLogs();
    }

    /// <summary>
    /// ? NEW: Get battle logs for a specific player (filtered by attacker or target)
    /// </summary>
    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession)
    {
        var allLogs = fullSession
            ? _engine.GetFullBattleLogs()
            : _engine.GetSectionBattleLogs();

        return allLogs
            .Where(log => log.AttackerUuid == uid || log.TargetUuid == uid)
            .ToList();
    }

    /// <summary>
    /// Convert new statistics to legacy DpsData format
    /// </summary>
    public Dictionary<long, DpsData> ToLegacyFormat(bool fullSession)
    {
        var stats = fullSession
            ? _engine.GetFullStatistics()
            : _engine.GetSectionStatistics();

        // ? Convert dictionary to array BEFORE iterating to avoid "Collection was modified" exception
        // Even though stats is a snapshot dictionary, we need to freeze it as an array
        var statsSnapshot = stats.ToArray();

        // ? Get battle logs snapshot ONCE to avoid concurrent modification
        var battleLogs = fullSession
            ? _engine.GetFullBattleLogs()
            : _engine.GetSectionBattleLogs();

        // ? Convert to array to avoid "Collection was modified" exception
        var battleLogsSnapshot = battleLogs.ToArray();

        var result = new Dictionary<long, DpsData>();

        // ? Iterate over the frozen snapshot array instead of the dictionary
        foreach (var (uid, playerStats) in statsSnapshot)
        {
            var dpsData = new DpsData
            {
                UID = uid,
                StartLoggedTick = playerStats.StartTick,
                LastLoggedTick = playerStats.LastTick,
                TotalAttackDamage = playerStats.AttackDamage.Total,
                TotalTakenDamage = playerStats.TakenDamage.Total,
                TotalHeal = playerStats.Healing.Total,
                IsNpcData = playerStats.IsNpc
            };

            // ? Filter logs for this player
            var playerLogs = battleLogsSnapshot
                .Where(log => log.AttackerUuid == uid || log.TargetUuid == uid)
                .ToArray();

            // ? Use AddBattleLogRange - updates BOTH mutable and immutable
            if (playerLogs.Length > 0)
            {
                dpsData.AddBattleLogRange(playerLogs);
            }

            // ? CRITICAL FIX: Snapshot the Skills dictionary before iterating
            // PlayerStatistics.Skills is a mutable dictionary that can be modified by other threads
            var skillsSnapshot = playerStats.Skills.ToArray();

            // Convert skill statistics
            foreach (var (skillId, skillStats) in skillsSnapshot)
            {
                dpsData.UpdateSkillData(skillId, skill =>
                {
                    skill.TotalValue = (long)skillStats.TotalValue;
                    skill.UseTimes = skillStats.UseTimes;
                    skill.CritTimes = skillStats.CritTimes;
                    skill.LuckyTimes = skillStats.LuckyTimes;
                });
            }

            result[uid] = dpsData;
        }

        return result;
    }

    /// <summary>
    /// Get raw statistics (new format)
    /// </summary>
    public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession)
    {
        return fullSession
            ? _engine.GetFullStatistics()
            : _engine.GetSectionStatistics();
    }

    public int GetStatisticsCount(bool fullSession)
    {
        return fullSession
            ? _engine.GetFullStatisticsCount()
            : _engine.GetSectionStatisticsCount();
    }
}
