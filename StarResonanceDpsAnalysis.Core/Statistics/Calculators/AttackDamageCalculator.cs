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

        if (log.IsCritical && log.IsLucky)
        {
            values.CritCount++;
            values.LuckyCount++;
            values.CritValue += log.Value;
            values.LuckyValue += log.Value;
        }
        else if (log.IsCritical)
        {
            values.CritCount++;
            values.CritValue += log.Value;
        }
        else if (log.IsLucky)
        {
            values.LuckyCount++;
            values.LuckyValue += log.Value;
        }
        else
        {
            values.NormalValue += log.Value;
        }

        // Update skill breakdown
        var skill = stats.GetOrCreateSkill(log.SkillID);
        skill.TotalValue += log.Value;
        skill.UseTimes++;
        if (log.IsCritical) skill.CritTimes++;
        if (log.IsLucky) skill.LuckyTimes++;
    }
}
