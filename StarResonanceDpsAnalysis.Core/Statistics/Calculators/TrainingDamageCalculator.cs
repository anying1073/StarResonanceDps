using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics.Calculators;

public sealed class TrainingDamageCalculator : IStatisticsCalculator
{
    public HashSet<long> TargetDummyUids { get; set; } = [];

    public void Calculate(BattleLog log, StatisticsContext context)
    {
        if (!log.IsAttackerPlayer || log.IsTargetPlayer || log.IsHeal)
            return;

        if (TargetDummyUids.Count > 0 && !TargetDummyUids.Contains(log.TargetUuid))
            return;

        context.CombatStarted = true;

        var fullStats = context.GetOrCreateFullStats(log.AttackerUuid);
        var sectionStats = context.GetOrCreateSectionStats(log.AttackerUuid);

        UpdateStatistics(log, fullStats);
        UpdateStatistics(log, sectionStats);
    }

    public string StatisticTypeName => nameof(TrainingDamageCalculator);

    private static void UpdateStatistics(BattleLog log, PlayerStatistics stats)
    {
        stats.StartTick ??= log.TimeTicks;
        stats.LastTick = log.TimeTicks;
        var ticks = stats.ElapsedTicks();

        var values = stats.AttackDamage;
        values.Total += log.Value;
        values.ValuePerSecond = ticks > 0 ? (double)values.Total * TimeSpan.TicksPerSecond / ticks : 0;

        var skill = stats.GetOrCreateSkill(log.SkillID);
        skill.TotalValue += log.Value;
        skill.UseTimes++;

        if (log.IsCritical && log.IsLucky)
        {
            values.CritAndLuckyCount++;
            values.CritAndLuckyValue += log.Value;
            skill.CritAndLuckyTimes++;
            skill.CritAndLuckyValue += log.Value;
        }
        else if (log.IsCritical)
        {
            values.CritCount++;
            values.CritValue += log.Value;
            skill.CritTimes++;
            skill.CritValue += log.Value;
        }
        else if (log.IsLucky)
        {
            values.LuckyCount++;
            values.LuckyValue += log.Value;
            skill.LuckyTimes++;
            skill.LuckValue += log.Value;
        }
        else
        {
            values.NormalValue += log.Value;
        }

        if (!log.IsLucky)
        {
            values.HitCount++;
        }
    }
}