namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Statistics for a specific skill
/// </summary>
public sealed class SkillStatistics(long skillId)
{
    public long SkillId { get; } = skillId;
    public long TotalValue { get; set; }
    public int UseTimes { get; set; }
    public int CritTimes { get; set; }
    public long CritValue { get; set; }
    public int LuckyTimes { get; set; }
    public long LuckValue { get; set; }
    public int CritAndLuckyTimes { get; set; }
    public long CritAndLuckyValue { get; set; }
}