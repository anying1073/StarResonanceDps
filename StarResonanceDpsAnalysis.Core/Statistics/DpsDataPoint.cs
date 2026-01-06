namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Represents a single DPS/HPS/DTPS data point in time series
/// Immutable value object following DDD principles
/// </summary>
public readonly record struct DpsDataPoint(TimeSpan Time, double Value);