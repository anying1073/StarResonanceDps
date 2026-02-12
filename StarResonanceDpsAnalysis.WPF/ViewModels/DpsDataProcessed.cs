using StarResonanceDpsAnalysis.Core.Statistics;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Helper struct for pre-processed DPS data to avoid redundant calculations
/// Immutable by design for thread-safety and performance
/// </summary>
public readonly record struct DpsDataProcessed(
    PlayerStatistics OriginalData,
    ulong Value,
    long DurationTicks,
    long Uid,
    double ValuePerSecond);