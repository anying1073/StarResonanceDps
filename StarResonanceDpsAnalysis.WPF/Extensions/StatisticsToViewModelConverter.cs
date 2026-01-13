using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

/// <summary>
/// Converts PlayerStatistics (from new architecture) to ViewModels for WPF
/// </summary>
public static class StatisticsToViewModelConverter
{
    /// <summary>
    /// Convert StatisticValues to DataStatistics (WPF model)
    /// </summary>
    public static DataStatisticsViewModel ToDataStatistics(this StatisticValues stats, TimeSpan duration)
    {
        var durationSeconds = duration.TotalSeconds;
        return new DataStatisticsViewModel
        {
            Total = stats.Total,
            Hits = stats.HitCount,
            CritCount = stats.CritCount,
            LuckyCount = stats.LuckyCount + stats.CritAndLuckyCount,
            Average = durationSeconds > 0 ? stats.Total / durationSeconds : double.NaN,
            NormalValue = stats.NormalValue,
            CritValue = stats.CritValue,
            LuckyValue = stats.LuckyValue + stats.CritAndLuckyValue
        };
    }

    public static SkillViewModelCollection
        ToSkillItemVmList(this PlayerStatistics playerStats)
    {
        var damageSkills = BuildSkillList(playerStats.AttackDamage);
        var healingSkills = BuildSkillList(playerStats.Healing);
        var takenSkills = BuildSkillList(playerStats.TakenDamage);

        return new SkillViewModelCollection(damageSkills, healingSkills, takenSkills);
    }

    /// <summary>
    /// Generic method to build skill list from skill statistics
    /// </summary>
    private static List<SkillItemViewModel> BuildSkillList(
        StatisticValues stat)
    {
        var skills = stat.Skills;
        var totalValue = stat.Total;
        var result = new List<SkillItemViewModel>(skills.Count);

        foreach (var (skillId, skillStats) in skills)
        {
            var totalLucky = skillStats.LuckyTimes + skillStats.CritAndLuckyTimes;
            var luckyValue = skillStats.LuckValue + skillStats.CritAndLuckyValue;
            var normalValue = skillStats.TotalValue - skillStats.CritValue - luckyValue;
            var skillVm = new SkillItemViewModel
            {
                SkillId = skillId,
                SkillName = EmbeddedSkillConfig.GetName((int)skillId),
                TotalValue = skillStats.TotalValue,
                HitCount = skillStats.UseTimes,
                CritCount = skillStats.CritTimes,
                LuckyCount = totalLucky,
                Average = skillStats.UseTimes > 0 ? skillStats.TotalValue / (double)skillStats.UseTimes : 0,
                CritRate = MathExtension.Rate(skillStats.CritTimes, skillStats.UseTimes),
                LuckyRate = MathExtension.Rate(totalLucky, skillStats.UseTimes),
                CritValue = skillStats.CritValue,
                LuckyValue = luckyValue,
                NormalValue = normalValue,
                RateToTotal = MathExtension.Rate(skillStats.TotalValue, totalValue)
            };

            result.Add(skillVm);
        }

        var ret = result.OrderByDescending(vm => vm.TotalValue).ToList();
        var count = ret.Count;
        switch (count)
        {
            case 1:
                ret[0].RateToMax = 1;
                break;
            case > 1:
            {
                for (var i = count - 1; i >= 0; i--)
                {
                    ret[i].RateToMax = MathExtension.Rate(ret[i].TotalValue, ret[0].TotalValue);
                }

                break;
            }
        }

        return ret;
    }
}