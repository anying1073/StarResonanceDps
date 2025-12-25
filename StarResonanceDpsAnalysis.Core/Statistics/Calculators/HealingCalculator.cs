using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics.Calculators;

/// <summary>
/// Calculates healing statistics
/// Following SRP: Only handles healing calculation
/// </summary>
public sealed class HealingCalculator : IStatisticsCalculator
{
    public string StatisticTypeName => "Healing";
    
    public void Calculate(BattleLog log, StatisticsContext context)
    {
        // Only process healing to players
        if (!log.IsTargetPlayer || !log.IsHeal)
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
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;
        var ticks = stats.LastTick - (stats.StartTick ?? 0);
        
        var values = stats.Healing;
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
        
        var skill = stats.GetOrCreateSkill(log.SkillID);
        skill.TotalValue += log.Value;
        skill.UseTimes++;
        if (log.IsCritical) skill.CritTimes++;
        if (log.IsLucky) skill.LuckyTimes++;
    }
}
