using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Implementation of team statistics UI management
/// Encapsulates team-level statistics calculation and display logic
/// </summary>
public class TeamStatsUIManager : ITeamStatsUIManager
{
    private readonly ILogger<TeamStatsUIManager> _logger;

    public TeamStatsUIManager(ILogger<TeamStatsUIManager> logger)
    {
        _logger = logger;
    }

    public ulong TeamTotalDamage { get; private set; }
    public double TeamTotalDps { get; private set; }
    public string TeamTotalLabel { get; private set; } = "团队DPS";
    public bool ShowTeamTotal { get; set; }

    public event EventHandler<TeamStatsUpdatedEventArgs>? TeamStatsUpdated;

    public void UpdateTeamStats(TeamTotalStats teamStats, StatisticType statisticType, bool hasData)
    {
        if (!ShowTeamTotal) return;

        // Update label based on statistic type
        TeamTotalLabel = statisticType switch
        {
            StatisticType.Damage => "团队DPS",
            StatisticType.Healing => "团队治疗",
            StatisticType.TakenDamage => "团队承伤",
            StatisticType.NpcTakenDamage => "NPC承伤",
            _ => "团队DPS"
        };

        // Only update if there's new data
        if (teamStats.TotalValue > 0 || hasData)
        {
            TeamTotalDamage = teamStats.TotalValue;
            TeamTotalDps = teamStats.TotalDps;

            // Raise event for UI binding
            TeamStatsUpdated?.Invoke(this, new TeamStatsUpdatedEventArgs
            {
                TotalDamage = TeamTotalDamage,
                TotalDps = TeamTotalDps,
                Label = TeamTotalLabel,
                StatisticType = statisticType
            });

            //// Log details only when there's data to avoid log spam
            //if (hasData)
            //{
            //    _logger.LogDebug(
            //        "TeamStats [{Type}]: Total={Total:N0}, DPS={Dps:N0}, " +
            //        "Players={Players}, NPCs={NPCs}, Duration={Duration:F1}s",
            //        statisticType,
            //        teamStats.TotalValue,
            //        teamStats.TotalDps,
            //        teamStats.PlayerCount,
            //        teamStats.NpcCount,
            //        teamStats.MaxDuration);
            //}
        }
    }

    public void ResetTeamStats()
    {
        TeamTotalDamage = 0;
        TeamTotalDps = 0;
        
        _logger.LogDebug("Team stats reset to zero");
    }
}
