using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics.Calculators;

/// <summary>
/// Calculates attack damage statistics
/// Following SRP: Only handles attack damage calculation
/// Following OCP: Can be extended without modifying other calculators
/// </summary>
public sealed class AttackDamageCalculator : IStatisticsCalculator
{
    public string StatisticTypeName => "Damage";

    public void Calculate(BattleLog log, StatisticsContext context)
    {
        // Only process if attacker is player and target is not player (attacking NPCs)
        if (!log.IsAttackerPlayer || log.IsTargetPlayer || log.IsHeal)
            return;

        var fullStats = context.GetOrCreateFullStats(log.AttackerUuid);
        var sectionStats = context.GetOrCreateSectionStats(log.AttackerUuid);

        UpdateStatistics(log, fullStats);
        UpdateStatistics(log, sectionStats);
    }

    public void ResetSection(StatisticsContext context)
    {
        // Section reset is handled by context
    }

    private void UpdateStatistics(BattleLog log, PlayerStatistics stats)
    {
        // Update timing
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;
        var ticks = stats.LastTick - (stats.StartTick ?? 0);

        // Update totals
        var values = stats.AttackDamage;
        values.Total += log.Value;
        values.ValuePerSecond = ticks > 0 ? (double)values.Total * TimeSpan.TicksPerMillisecond / ticks : double.NaN;
        values.HitCount++;

        // Update skill breakdown
        var skill = stats.GetOrCreateSkill(log.SkillID);
        skill.TotalValue += log.Value;
        skill.UseTimes++;

        // Handle different hit types (aligned with PlayerStat.cs logic)
        if (log.IsCritical && log.IsLucky)
        {
            // Both crit and lucky: increment both counters and add to CritValue
            values.CritAndLuckyCount++;
            values.CritAndLuckyValue += log.Value;
            skill.CritAndLuckyTimes++;
            skill.CritAndLuckyValue += log.Value;
        }
        else if (log.IsCritical)
        {
            // Only crit
            values.CritCount++;
            values.CritValue += log.Value;
            skill.CritTimes++;
            skill.CritValue += log.Value;
        }
        else if (log.IsLucky)
        {
            // Only lucky
            values.LuckyCount++;
            values.LuckyValue += log.Value;
            skill.LuckyTimes++;
            skill.LuckValue += log.Value;
        }
        else
        {
            // Normal hit
            values.NormalValue += log.Value;
        }
    }
}
