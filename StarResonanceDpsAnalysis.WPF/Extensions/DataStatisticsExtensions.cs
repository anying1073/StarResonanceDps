using System.Collections.Generic;
using System.Linq;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class DataStatisticsExtensions
{
    public static DataStatistics FromSkillsToDamageTaken(this IReadOnlyList<SkillItemViewModel> skills,
        ulong durationMs)
    {
        var stats = new DataStatistics
        {
            Total = skills.Sum(s => s.TotalTakenDamage),
            Hits = skills.Sum(s => s.HitCount)
        };

        var totalCritHits = skills.Sum(s => s.CritCount);
        stats.CritRate = stats.Hits > 0 ? (double)totalCritHits / stats.Hits : 0;

        if (durationMs > 0)
        {
            var durationSeconds = durationMs / 1000.0;
            stats.Average = (long)(stats.Total / durationSeconds);
        }

        return stats;
    }

    public static DataStatistics FromSkillsToHealing(this IReadOnlyList<SkillItemViewModel> skills,
        ulong durationMs)
    {
        var stats = new DataStatistics
        {
            Total = skills.Sum(s => s.TotalHeal),
            Hits = skills.Sum(s => s.HitCount)
        };

        var totalCritHits = skills.Sum(s => s.CritCount);
        stats.CritRate = stats.Hits > 0 ? (double)totalCritHits / stats.Hits : 0;

        if (durationMs > 0)
        {
            var durationSeconds = durationMs / 1000.0;
            stats.Average = (long)(stats.Total / durationSeconds);
        }

        return stats;
    }

    public static DataStatistics FromSkillsToDamage(this IReadOnlyList<SkillItemViewModel> skills, ulong durationMs)
    {
        var stats = new DataStatistics
        {
            Total = skills.Sum(s => s.TotalDamage),
            Hits = skills.Sum(s => s.HitCount)
        };

        var totalCritHits = skills.Sum(s => s.CritCount);
        stats.CritRate = stats.Hits > 0 ? (double)totalCritHits / stats.Hits : 0;

        if (durationMs > 0)
        {
            var durationSeconds = durationMs / 1000.0;
            stats.Average = (long)(stats.Total / durationSeconds);
        }

        return stats;
    }
}
