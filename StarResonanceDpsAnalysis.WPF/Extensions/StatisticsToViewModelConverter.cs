using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using System.Collections.ObjectModel;

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
            LuckyValue = stats.LuckyValue + stats.CritAndLuckyValue,
        };
    }

    /// <summary>
    /// Build skill lists directly from PlayerStatistics (no battle log iteration needed!)
    /// </summary>
    public static (List<SkillItemViewModel> damage, List<SkillItemViewModel> healing, List<SkillItemViewModel> takenDamage)
        BuildSkillListsFromPlayerStats(PlayerStatistics playerStats)
    {
        var damageSkills = BuildSkillList(
            playerStats.AttackDamage.Skills,
            playerStats.AttackDamage.Total,
            (vm, value) => vm.Damage = value);

        var healingSkills = BuildSkillList(
            playerStats.Healing.Skills,
            playerStats.Healing.Total,
            (vm, value) => vm.Heal = value);

        var takenSkills = BuildSkillList(
            playerStats.TakenDamage.Skills,
            playerStats.TakenDamage.Total,
            (vm, value) => vm.TakenDamage = value);

        return (damageSkills, healingSkills, takenSkills);
    }

    /// <summary>
    /// Generic method to build skill list from skill statistics
    /// </summary>
    private static List<SkillItemViewModel> BuildSkillList(
        IReadOnlyDictionary<long, SkillStatistics> skills,
        long totalValue,
        Action<SkillItemViewModel, SkillItemViewModel.SkillValue> setProperty)
    {
        var result = new List<SkillItemViewModel>(skills.Count);

        foreach (var (skillId, skillStats) in skills)
        {
            var skillVm = new SkillItemViewModel
            {
                SkillId = skillId,
                SkillName = EmbeddedSkillConfig.GetName((int)skillId)
            };

            var skillValue = CreateSkillValue(skillStats, totalValue);
            setProperty(skillVm, skillValue);

            result.Add(skillVm);
        }

        return result.OrderByDescending(GetTotalValue).ToList();
    }

    /// <summary>
    /// Create SkillValue from SkillStatistics
    /// </summary>
    private static SkillItemViewModel.SkillValue CreateSkillValue(SkillStatistics stats, long parentTotal)
    {
        var totalLucky = stats.LuckyTimes + stats.CritAndLuckyTimes;
        var luckyValue = stats.LuckValue + stats.CritAndLuckyValue;
        var normalValue = stats.TotalValue - stats.CritValue - luckyValue;

        return new SkillItemViewModel.SkillValue
        {
            TotalValue = stats.TotalValue,
            HitCount = stats.UseTimes,
            CritCount = stats.CritTimes,
            LuckyCount = totalLucky,
            Average = stats.UseTimes > 0 ? stats.TotalValue / (double)stats.UseTimes : 0,
            CritRate = GetRate(stats.CritTimes, stats.UseTimes),
            LuckyRate = GetRate(totalLucky, stats.UseTimes),
            CritValue = stats.CritValue,
            LuckyValue = luckyValue,
            NormalValue = normalValue,
            PercentToTotal = GetRate(stats.TotalValue, parentTotal)
        };
    }

    /// <summary>
    /// Calculate rate (returns 0 if divider is 0)
    /// </summary>
    private static double GetRate(double value, double divider) => 
        divider > 0 ? value / divider : 0;

    /// <summary>
    /// Get total value from skill view model based on which property is set
    /// </summary>
    private static long GetTotalValue(SkillItemViewModel vm) =>
        vm.Damage?.TotalValue ?? vm.Heal?.TotalValue ?? vm.TakenDamage?.TotalValue ?? 0;
}