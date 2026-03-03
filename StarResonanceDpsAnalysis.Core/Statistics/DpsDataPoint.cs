using System;
using Newtonsoft.Json;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Represents a single DPS/HPS/DTPS data point in time series
/// Immutable value object
/// </summary>
public readonly record struct DpsDataPoint
{
    [JsonConstructor]
    public DpsDataPoint(TimeSpan time, double value)
    {
        Time = time;
        Value = value;
    }

    public TimeSpan Time { get; init; }
    public double Value { get; init; }
}