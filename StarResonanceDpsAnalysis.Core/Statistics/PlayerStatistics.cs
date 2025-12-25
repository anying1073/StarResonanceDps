namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Holds all statistics for a single player
/// Following SRP: Only responsible for holding player statistics data
/// </summary>
public sealed class PlayerStatistics(long uid)
{
    public long Uid { get; } = uid;

    // Statistics by type
    public StatisticValues AttackDamage { get; } = new();
    public StatisticValues TakenDamage { get; } = new();
    public StatisticValues Healing { get; } = new();

    // Skill breakdown
    public Dictionary<long, SkillStatistics> Skills { get; } = new();
    
    // ? NEW: Track skills that hit this player (for taken damage breakdown)
    public Dictionary<long, SkillStatistics> TakenDamageSkills { get; } = new();

    // Timing info
    public long? StartTick { get; set; }
    public long LastTick { get; set; }

    // NPC flag
    public bool IsNpc { get; set; }

    /// <summary>
    /// Get or create skill statistics
    /// </summary>
    public SkillStatistics GetOrCreateSkill(long skillId)
    {
        if (!Skills.TryGetValue(skillId, out var stats))
        {
            stats = new SkillStatistics(skillId);
            Skills[skillId] = stats;
        }

        return stats;
    }
    
    /// <summary>
    /// Get or create taken damage skill statistics
    /// </summary>
    public SkillStatistics GetOrCreateTakenSkill(long skillId)
    {
        if (!TakenDamageSkills.TryGetValue(skillId, out var stats))
        {
            stats = new SkillStatistics(skillId);
            TakenDamageSkills[skillId] = stats;
        }

        return stats;
    }

    public long ElapsedTicks()
    {
        return LastTick - StartTick ?? 0;
    }
}

/// <summary>
/// Statistics values for a specific metric (damage, healing, etc.)
/// </summary>
public sealed class StatisticValues
{
    public long Total { get; set; }
    public int HitCount { get; set; }
    public int CritCount { get; set; }
    public int LuckyCount { get; set; }
    public long NormalValue { get; set; }
    public long CritValue { get; set; }
    public long LuckyValue { get; set; }
    public double ValuePerSecond { get; set; }
}

/// <summary>
/// Statistics for a specific skill
/// </summary>
public sealed class SkillStatistics(long skillId)
{
    public long SkillId { get; } = skillId;
    public long TotalValue { get; set; }
    public int UseTimes { get; set; }
    public int CritTimes { get; set; }
    public int LuckyTimes { get; set; }
}