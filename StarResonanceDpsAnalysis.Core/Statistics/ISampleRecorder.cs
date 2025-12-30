namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Interface for recording DPS/HPS/DTPS samples
/// ISP: Interface Segregation - focused interface for sample recording
/// </summary>
public interface ISampleRecorder
{
    /// <summary>
    /// Record samples for all players in the statistics
    /// </summary>
    /// <param name="statistics">Player statistics dictionary</param>
    /// <param name="sectionDuration">Time elapsed since section start</param>
    void RecordSamples(IReadOnlyDictionary<long, PlayerStatistics> statistics, TimeSpan sectionDuration);
}

/// <summary>
/// Records samples periodically for all players
/// SRP: Single Responsibility - only handles periodic sample recording
/// </summary>
public sealed class PeriodicSampleRecorder : ISampleRecorder
{
    public void RecordSamples(IReadOnlyDictionary<long, PlayerStatistics> statistics, TimeSpan sectionDuration)
    {
        foreach (var playerStats in statistics.Values)
        {
            // Get current DPS/HPS/DTPS values
            var dps = playerStats.AttackDamage.ValuePerSecond;
            var hps = playerStats.Healing.ValuePerSecond;
            var dtps = playerStats.TakenDamage.ValuePerSecond;

            // Only record valid values (not NaN)
            if (!double.IsNaN(dps))
            {
                playerStats.AddDpsSample(sectionDuration, dps);
            }

            if (!double.IsNaN(hps))
            {
                playerStats.AddHpsSample(sectionDuration, hps);
            }

            if (!double.IsNaN(dtps))
            {
                playerStats.AddDtpsSample(sectionDuration, dtps);
            }
        }
    }
}
