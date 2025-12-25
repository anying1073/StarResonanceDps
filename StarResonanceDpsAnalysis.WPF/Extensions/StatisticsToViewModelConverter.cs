using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

/// <summary>
/// Converts PlayerStatistics (from new architecture) to ViewModels for WPF
/// </summary>
public static class StatisticsToViewModelConverter
{
    /// <summary>
    /// ✅ NEW: Convert StatisticValues to DataStatistics (WPF model)
    /// </summary>
    public static DataStatistics ToDataStatistics(this StatisticValues stats, TimeSpan duration)
    {
        var durationSeconds = duration.TotalSeconds;
        return new DataStatistics
        {
            Total = stats.Total,
            Hits = stats.HitCount,
            CritCount = stats.CritCount,
            LuckyCount = stats.LuckyCount,
            Average = durationSeconds > 0 ? stats.Total / durationSeconds : 0,
            NormalValue = stats.NormalValue,
            CritValue = stats.CritValue,
            LuckyValue = stats.LuckyValue
        };
    }

    /// <summary>
    /// Build skill lists directly from PlayerStatistics (no battle log iteration needed!)
    /// </summary>
    public static (List<SkillItemViewModel> damage, List<SkillItemViewModel> healing, List<SkillItemViewModel> takenDamage)
        BuildSkillListsFromPlayerStats(PlayerStatistics playerStats)
    {
        var damageSkills = new List<SkillItemViewModel>();
        var healingSkills = new List<SkillItemViewModel>();
        var takenSkills = new List<SkillItemViewModel>();

        // ✅ Process attack/heal skills from playerStats.Skills
        foreach (var (skillId, skillStats) in playerStats.Skills)
        {
            var skillType = EmbeddedSkillConfig.GetTypeOf((int)skillId);
            var skillName = EmbeddedSkillConfig.GetName((int)skillId);

            var skillVm = new SkillItemViewModel
            {
                SkillId = skillId,
                SkillName = skillName
            };

            var value = new SkillItemViewModel.SkillValue
            {
                TotalValue = skillStats.TotalValue,
                HitCount = skillStats.UseTimes,
                CritCount = skillStats.CritTimes,
                LuckyCount = skillStats.LuckyTimes,
                Average = skillStats.UseTimes > 0
                    ? skillStats.TotalValue / (double)skillStats.UseTimes
                    : 0,
                CritRate = skillStats.UseTimes > 0
                    ? skillStats.CritTimes / (double)skillStats.UseTimes
                    : 0,
                // Calculate values
                CritValue = 0, // Not stored separately in SkillStatistics
                LuckyValue = 0,
                NormalValue = skillStats.TotalValue // Approximate
            };

            switch (skillType)
            {
                case SkillType.Damage:
                    skillVm.Damage = value;
                    damageSkills.Add(skillVm);
                    break;
                case SkillType.Heal: // ✅ Fixed: Heal not Healing
                    skillVm.Heal = value;
                    healingSkills.Add(skillVm);
                    break;
                // Other types can be added here
            }
        }

        // ✅ Process taken damage skills from playerStats.TakenDamageSkills
        foreach (var (skillId, skillStats) in playerStats.TakenDamageSkills)
        {
            var skillName = EmbeddedSkillConfig.GetName((int)skillId);

            var skillVm = new SkillItemViewModel
            {
                SkillId = skillId,
                SkillName = skillName,
                TakenDamage = new SkillItemViewModel.SkillValue
                {
                    TotalValue = skillStats.TotalValue,
                    HitCount = skillStats.UseTimes,
                    CritCount = skillStats.CritTimes,
                    LuckyCount = skillStats.LuckyTimes,
                    Average = skillStats.UseTimes > 0
                        ? skillStats.TotalValue / (double)skillStats.UseTimes
                        : 0,
                    CritRate = skillStats.UseTimes > 0
                        ? skillStats.CritTimes / (double)skillStats.UseTimes
                        : 0,
                    CritValue = 0,
                    LuckyValue = 0,
                    NormalValue = skillStats.TotalValue
                }
            };

            takenSkills.Add(skillVm);
        }

        // Sort by total value descending
        damageSkills = damageSkills.OrderByDescending(s => s.Damage.TotalValue).ToList();
        healingSkills = healingSkills.OrderByDescending(s => s.Heal.TotalValue).ToList();
        takenSkills = takenSkills.OrderByDescending(s => s.TakenDamage.TotalValue).ToList();

        return (damageSkills, healingSkills, takenSkills);
    }
}