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
/// Note: Delta values are automatically recorded in UpdateDeltaValues(), 
/// so this recorder only needs to trigger the update.
/// </summary>
public sealed class PeriodicSampleRecorder : ISampleRecorder
{
    /// <summary>
    /// Creates a periodic sample recorder
    /// </summary>
    public PeriodicSampleRecorder()
    {
    }

    public void RecordSamples(IReadOnlyDictionary<long, PlayerStatistics> statistics, TimeSpan sectionDuration)
    {
        foreach (var playerStats in statistics.Values)
        {
            // Update delta values - this automatically records delta samples to time series
            playerStats.UpdateDeltaValues();
        }
    }
}

/// <summary>
/// Records delta samples for comparison and analysis
/// Useful for detecting burst patterns
/// Note: Delta values are automatically recorded in UpdateDeltaValues(), 
/// so this recorder only needs to trigger the update.
/// </summary>
public sealed class HybridSampleRecorder : ISampleRecorder
{
    public void RecordSamples(IReadOnlyDictionary<long, PlayerStatistics> statistics, TimeSpan sectionDuration)
    {
        foreach (var playerStats in statistics.Values)
        {
            // Update delta values - this automatically records delta samples to time series
            playerStats.UpdateDeltaValues();
        }
    }
}
