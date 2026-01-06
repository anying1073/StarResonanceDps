using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics.Calculators;

/// <summary>
/// Calculates taken damage statistics
/// Following SRP: Only handles damage taken calculation
/// </summary>
public sealed class TakenDamageCalculator : IStatisticsCalculator
{
    public string StatisticTypeName => "TakenDamage";

    public void Calculate(BattleLog log, StatisticsContext context)
    {
        // Process damage taken by players
        if (!log.IsTargetPlayer || log.IsHeal)
            return;

        var fullStats = context.GetOrCreateFullStats(log.TargetUuid);
        var sectionStats = context.GetOrCreateSectionStats(log.TargetUuid);

        UpdateStatistics(log, fullStats);
        UpdateStatistics(log, sectionStats);

        // Also track NPC damage if attacker is not a player
        if (log.IsAttackerPlayer) return;
        var npcFull = context.GetOrCreateFullStats(log.AttackerUuid);
        var npcSection = context.GetOrCreateSectionStats(log.AttackerUuid);
        npcFull.IsNpc = true;
        npcSection.IsNpc = true;

        UpdateNpcAttackStats(log, npcFull);
        UpdateNpcAttackStats(log, npcSection);
    }

    public void ResetSection(StatisticsContext context)
    {
        // Section reset is handled by context
    }

    private void UpdateStatistics(BattleLog log, PlayerStatistics stats)
    {
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;
        var ticks = stats.LastTick - (stats.StartTick ?? 0);

        var values = stats.TakenDamage;
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

    private void UpdateNpcAttackStats(BattleLog log, PlayerStatistics stats)
    {
        // Update NPC's attack damage output
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;

        var values = stats.AttackDamage;
        values.Total += log.Value;
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
