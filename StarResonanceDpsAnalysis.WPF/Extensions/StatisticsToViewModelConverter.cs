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

    /// <summary>
    /// Build skill lists directly from PlayerStatistics (no battle log iteration needed!)
    /// </summary>
    public static (List<SkillItemViewModel> damage, List<SkillItemViewModel> healing, List<SkillItemViewModel> taken)
        BuildSkillListsFromPlayerStats(PlayerStatistics playerStats)
    {
        var damageSkills = BuildSkillList(
            playerStats.AttackDamage.Skills,
            playerStats.AttackDamage.Total);

        var healingSkills = BuildSkillList(
            playerStats.Healing.Skills,
            playerStats.Healing.Total);

        var takenSkills = BuildSkillList(
            playerStats.TakenDamage.Skills,
            playerStats.TakenDamage.Total);

        return (damageSkills, healingSkills, takenSkills);
    }

    /// <summary>
    /// Generic method to build skill list from skill statistics
    /// </summary>
    private static List<SkillItemViewModel> BuildSkillList(
        IReadOnlyDictionary<long, SkillStatistics> skills,
        long totalValue)
    {
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
                CritRate = GetRate(skillStats.CritTimes, skillStats.UseTimes),
                LuckyRate = GetRate(totalLucky, skillStats.UseTimes),
                CritValue = skillStats.CritValue,
                LuckyValue = luckyValue,
                NormalValue = normalValue,
                RateToTotal = GetRate(skillStats.TotalValue, totalValue)
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
                    ret[i].RateToMax = GetRate(ret[i].TotalValue, ret[0].TotalValue) * 1;
                }

                break;
            }
        }

        return ret;
    }

    /// <summary>
    /// Calculate rate (returns 0 if divider is 0)
    /// </summary>
    private static double GetRate(double value, double divider)
    {
        return divider > 0 ? value / divider : 0;
    }

    /// <summary>
    /// Calculate percentage (returns 0 if divider is 0)
    /// </summary>
    /// <param name="value"></param>
    /// <param name="divider"></param>
    /// <returns></returns>
    private static double GetPercentage(double value , double divider)
    {
        return GetRate(value, divider) * 100;
    }
}