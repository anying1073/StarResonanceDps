namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Global configuration for statistics system
/// This allows UI layer to configure core statistics behavior without tight coupling
/// </summary>
public static class StatisticsConfiguration
{
    private static int _timeSeriesSampleCapacity = 300;
    
    /// <summary>
    /// Maximum number of time series samples to store
    /// Default: 300 samples
    /// Range: 50-1000 samples
    /// </summary>
    public static int TimeSeriesSampleCapacity
    {
        get => _timeSeriesSampleCapacity;
        set => _timeSeriesSampleCapacity = Math.Clamp(value, 50, 1000);
    }
}
