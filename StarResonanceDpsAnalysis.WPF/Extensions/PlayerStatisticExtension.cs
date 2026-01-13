using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class PlayerStatisticExtension
{
    public static SkillViewModelCollection
        ToSkillItemVmList(this PlayerStatistics playerStats)
    {
        var damageSkills = BuildSkillList(playerStats.AttackDamage.Skills);
        var healingSkills = BuildSkillList(playerStats.Healing.Skills);
        var takenSkills = BuildSkillList(playerStats.TakenDamage.Skills);

        return new SkillViewModelCollection(damageSkills, healingSkills, takenSkills);

        static List<SkillItemViewModel> BuildSkillList(IReadOnlyDictionary<long, SkillStatistics> skills)
        {
            return skills.Values
                //.OrderByDescending(s => s.TotalValue)
                .Select(s => new SkillItemViewModel
                {
                    SkillId = s.SkillId,
                    SkillName = EmbeddedSkillConfig.TryGet(s.SkillId.ToString(), out var def)
                        ? def.Name
                        : s.SkillId.ToString(),
                    TotalValue = s.TotalValue,
                    HitCount = s.UseTimes,
                    CritCount = s.CritTimes,
                    LuckyCount = s.LuckyTimes,
                    Average = s.UseTimes > 0 ? Math.Round((double)s.TotalValue / s.UseTimes) : 0,
                    CritRate = s.UseTimes > 0 ? (double)s.CritTimes / s.UseTimes : 0,
                    CritValue = s.CritValue,
                    LuckyValue = s.LuckValue
                })
                .ToList();
        }
    }
}