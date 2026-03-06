using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Implementation of team statistics UI management
/// Encapsulates team-level statistics calculation and display logic
/// </summary>
public class TeamStatsUIManager : ITeamStatsUIManager
{
    private readonly ILogger<TeamStatsUIManager> _logger;
    private readonly LocalizationManager _localizationManager;

    public TeamStatsUIManager(ILogger<TeamStatsUIManager> logger, LocalizationManager localizationManager)
    {
        _logger = logger;
        _localizationManager = localizationManager;
        TeamTotalLabel = GetTeamLabel(StatisticType.Damage);
    }

    public ulong TeamTotalDamage { get; private set; }
    public double TeamTotalDps { get; private set; }
    public string TeamTotalLabel { get; private set; }
    public bool ShowTeamTotal { get; set; }

    public event EventHandler<TeamStatsUpdatedEventArgs>? TeamStatsUpdated;

    public void UpdateTeamStats(TeamTotalStats teamStats, StatisticType statisticType, bool hasData)
    {
        if (!ShowTeamTotal) return;

        // Update label based on statistic type
        TeamTotalLabel = GetTeamLabel(statisticType);

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

    private string GetTeamLabel(StatisticType statisticType)
    {
        return statisticType switch
        {
            StatisticType.Damage => GetLocalizedString(ResourcesKeys.DpsStatistics_TeamLabel_Damage, "Team DPS"),
            StatisticType.Healing => GetLocalizedString(ResourcesKeys.DpsStatistics_TeamLabel_Healing, "Team HPS"),
            StatisticType.TakenDamage =>
                GetLocalizedString(ResourcesKeys.DpsStatistics_TeamLabel_TakenDamage, "Team DTPS"),
            StatisticType.NpcTakenDamage =>
                GetLocalizedString(ResourcesKeys.DpsStatistics_TeamLabel_NpcTakenDamage, "NPC DTPS"),
            _ => GetLocalizedString(ResourcesKeys.DpsStatistics_TeamLabel_Damage, "Team DPS")
        };
    }

    private string GetLocalizedString(string key, string defaultValue)
    {
        return _localizationManager.GetString(key, defaultValue: defaultValue);
    }
}
