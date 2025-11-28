using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class DataStatisticsExtensions
{
    public static DataStatistics FromSkillsToDamageTaken(this IReadOnlyList<SkillItemViewModel> skills,
        ulong durationMs)
    {
        var stats = new DataStatistics();
        skills.UpdateDamageTaken(durationMs, stats);
        return stats;
    }

    public static void UpdateDamageTaken(this IReadOnlyList<SkillItemViewModel> skills, ulong durationMs,
        DataStatistics stats)
    {
        stats.Total = skills.Sum(s => s.TotalTakenDamage);
        stats.Hits = skills.Sum(s => s.HitCount);

        var totalCritHits = skills.Sum(s => s.CritCount);
        stats.CritRate = stats.Hits > 0 ? (double)totalCritHits / stats.Hits : 0;

        if (durationMs > 0)
        {
            var durationSeconds = durationMs / 1000.0;
            stats.Average = (long)(stats.Total / durationSeconds);
        }
    }

    public static DataStatistics FromSkillsToHealing(this IReadOnlyList<SkillItemViewModel> skills,
        ulong durationMs)
    {
        var stats = new DataStatistics();
        skills.UpdateHealing(durationMs, stats);
        return stats;
    }

    public static void UpdateHealing(this IReadOnlyList<SkillItemViewModel> skills, ulong durationMs, DataStatistics stats)
    {
        stats.Total = skills.Sum(s => s.TotalHeal);
        stats.Hits = skills.Sum(s => s.HitCount);

        var totalCritHits = skills.Sum(s => s.CritCount);
        stats.CritRate = stats.Hits > 0 ? (double)totalCritHits / stats.Hits : 0;

        if (durationMs > 0)
        {
            var durationSeconds = durationMs / 1000.0;
            stats.Average = (long)(stats.Total / durationSeconds);
        }
    }

    public static DataStatistics FromSkillsToDamage(this IReadOnlyList<SkillItemViewModel> skills, ulong durationMs)
    {
        var stats = new DataStatistics();
        skills.UpdateDamage(durationMs, stats);
        return stats;
    }

    public static void UpdateDamage(this IReadOnlyList<SkillItemViewModel> skills, ulong durationMs, DataStatistics stats)
    {
        stats.Total = skills.Sum(s => s.TotalDamage);
        stats.Hits = skills.Sum(s => s.HitCount);

        var totalCritHits = skills.Sum(s => s.CritCount);
        stats.CritRate = stats.Hits > 0 ? (double)totalCritHits / stats.Hits : 0;

        if (durationMs > 0)
        {
            var durationSeconds = durationMs / 1000.0;
            stats.Average = (long)(stats.Total / durationSeconds);
        }
    }
}
