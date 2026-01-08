using System.Windows.Forms;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class DataStatisticsExtensions
{
    public static DataStatisticsViewModel FromSkillsToDamageTaken(this IReadOnlyList<SkillItemViewModel> skills,
        TimeSpan duration)
    {
        return CreateStatistics(skills, s => s.TakenDamage, duration);
    }

    public static void UpdateDamageTaken(
        this IReadOnlyList<SkillItemViewModel> skills,
        TimeSpan duration,
        DataStatisticsViewModel stats)
    {
        UpdateStatistics(skills, s => s.TakenDamage, duration, stats);
    }

    public static DataStatisticsViewModel FromSkillsToHealing(
        this IReadOnlyList<SkillItemViewModel> skills,
        TimeSpan duration)
    {
        return CreateStatistics(skills, s => s.Heal, duration);
    }

    public static void UpdateHealing(
        this IReadOnlyList<SkillItemViewModel> skills,
        TimeSpan duration,
        DataStatisticsViewModel stats)
    {
        UpdateStatistics(skills, s => s.Heal, duration, stats);
    }

    public static DataStatisticsViewModel FromSkillsToDamage(
        this IReadOnlyList<SkillItemViewModel> skills,
        TimeSpan duration)
    {
        return CreateStatistics(skills, s => s.Damage, duration);
    }

    public static void UpdateDamage(
        this IReadOnlyList<SkillItemViewModel> skills,
        TimeSpan durationMs,
        DataStatisticsViewModel stats)
    {
        UpdateStatistics(skills, s => s.Damage, durationMs, stats);
    }

    // Creates a new DataStatistics instance using the shared aggregation logic.
    private static DataStatisticsViewModel CreateStatistics(
        IReadOnlyList<SkillItemViewModel> skills,
        Func<SkillItemViewModel, SkillItemViewModel.SkillValue> totalSelector,
        TimeSpan duration)
    {
        var stats = new DataStatisticsViewModel();
        UpdateStatistics(skills, totalSelector, duration, stats);
        return stats;
    }

    // Shared aggregation logic for damage, healing and damage taken.
    private static void UpdateStatistics(
        IReadOnlyList<SkillItemViewModel> skills,
        Func<SkillItemViewModel, SkillItemViewModel.SkillValue> selector,
        TimeSpan duration,
        DataStatisticsViewModel stats)
    {
        stats.Total = skills.Sum(s => selector(s).TotalValue);
        stats.Hits = skills.Sum(s => selector(s).HitCount);
        stats.LuckyCount = skills.Sum(s => selector(s).LuckyCount);

        var totalCritHits = skills.Sum(s => selector(s).CritCount);
        stats.CritCount = totalCritHits;

        stats.NormalValue = skills.Sum(s => selector(s).NormalValue);
        stats.CritValue = skills.Sum(s => selector(s).CritValue);
        stats.LuckyValue = skills.Sum(s => selector(s).LuckyValue);

        if (duration.Ticks == 0)
        {
            stats.Average = Double.NaN;
            return;
        }
        stats.Average = (double)(stats.Total * TimeSpan.TicksPerSecond) / duration.Ticks;
    }
}
