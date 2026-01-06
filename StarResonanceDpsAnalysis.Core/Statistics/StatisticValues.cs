using System.Collections.Concurrent;
using System.Threading;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Statistics values for a specific metric (damage, healing, etc.)
/// Thread-safe for concurrent reads and writes using Interlocked operations
/// </summary>
public sealed class StatisticValues
{
    public long Total { get; set; }
    public int HitCount { get; set; }
    public int CritCount { get; set; }
    public int LuckyCount { get; set; }
    public int CritAndLuckyCount { get; set; }
    public long NormalValue { get; set; }
    public long CritValue { get; set; }
    public long LuckyValue { get; set; }
    public long CritAndLuckyValue { get; set; }
    
    /// <summary>
    /// Average value per second over entire duration (cumulative)
    /// </summary>
    public double ValuePerSecond { get; set; }
    
    // Thread-safe delta value using Interlocked for atomic double operations
    private long _deltaValuePerSecondBits;
    
    /// <summary>
    /// Instantaneous change in value per second since last measurement (delta/burst detection)
    /// Thread-safe: Uses Interlocked operations for atomic read/write of double values
    /// </summary>
    public double DeltaValuePerSecond
    {
        get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _deltaValuePerSecondBits));
        set => Interlocked.Exchange(ref _deltaValuePerSecondBits, BitConverter.DoubleToInt64Bits(value));
    }
    
    public ConcurrentDictionary<long, SkillStatistics> Skills { get; } = new();
}