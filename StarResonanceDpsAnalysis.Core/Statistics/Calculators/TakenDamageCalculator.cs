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
        if (!log.IsAttackerPlayer)
        {
            var npcFull = context.GetOrCreateFullStats(log.AttackerUuid);
            var npcSection = context.GetOrCreateSectionStats(log.AttackerUuid);
            npcFull.IsNpc = true;
            npcSection.IsNpc = true;
            
            UpdateNpcAttackStats(log, npcFull);
            UpdateNpcAttackStats(log, npcSection);
        }
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
        
        // ? Update taken damage skill breakdown (what skill hit this player)
        var takenSkill = stats.GetOrCreateTakenSkill(log.SkillID);
        takenSkill.TotalValue += log.Value;
        takenSkill.UseTimes++;
        if (log.IsCritical) takenSkill.CritTimes++;
        if (log.IsLucky) takenSkill.LuckyTimes++;
    }
    
    private void UpdateNpcAttackStats(BattleLog log, PlayerStatistics stats)
    {
        // Update NPC's attack damage output
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;
        
        var values = stats.AttackDamage;
        values.Total += log.Value;
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
        
        // ? Also track NPC's attack skills
        var skill = stats.GetOrCreateSkill(log.SkillID);
        skill.TotalValue += log.Value;
        skill.UseTimes++;
        if (log.IsCritical) skill.CritTimes++;
        if (log.IsLucky) skill.LuckyTimes++;
    }
}
