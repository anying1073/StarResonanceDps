using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Manages UI state for team statistics
/// Single Responsibility: Calculate and track team-level statistics display
/// </summary>
public interface ITeamStatsUIManager
{
    /// <summary>
    /// Current team total damage value
    /// </summary>
    ulong TeamTotalDamage { get; }
    
    /// <summary>
    /// Current team total DPS
    /// </summary>
    double TeamTotalDps { get; }
    
    /// <summary>
    /// Label for team total (changes based on statistic type)
    /// </summary>
    string TeamTotalLabel { get; }
    
    /// <summary>
    /// Whether team totals should be displayed
    /// </summary>
    bool ShowTeamTotal { get; set; }
    
    /// <summary>
    /// Update team statistics for the given statistic type
    /// </summary>
    /// <param name="teamStats">Calculated team statistics</param>
    /// <param name="statisticType">Current statistic type</param>
    /// <param name="hasData">Whether there is data to display</param>
    void UpdateTeamStats(TeamTotalStats teamStats, StatisticType statisticType, bool hasData);
    
    /// <summary>
    /// Reset team statistics to zero
    /// </summary>
    void ResetTeamStats();
    
    /// <summary>
    /// Event raised when team stats are updated
    /// </summary>
    event EventHandler<TeamStatsUpdatedEventArgs>? TeamStatsUpdated;
}

/// <summary>
/// Event args for team stats updates
/// </summary>
public class TeamStatsUpdatedEventArgs : EventArgs
{
    public ulong TotalDamage { get; init; }
    public double TotalDps { get; init; }
    public string Label { get; init; } = string.Empty;
    public StatisticType StatisticType { get; init; }
}
