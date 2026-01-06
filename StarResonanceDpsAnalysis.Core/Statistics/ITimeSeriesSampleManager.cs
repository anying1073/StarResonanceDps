namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Interface for managing time series samples
/// ISP: Interface Segregation - small, focused interface
/// </summary>
public interface ITimeSeriesSampleManager
{
    void AddSample(TimeSpan time, double value);
    IReadOnlyList<DpsDataPoint> GetSamples();
    void Clear();
}