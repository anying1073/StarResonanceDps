using System;

namespace StarResonanceDpsAnalysis.WPF.Models;

public class SkillLogItem
{
    public DateTime Timestamp { get; set; }
    public string Duration { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public long TotalValue { get; set; }
    public int Count { get; set; }
    public int CritCount { get; set; }
    public int LuckyCount { get; set; }
    public bool IsHeal { get; set; }
    
    // ? 新增：暴击伤害统计
    public long CritDamage { get; set; }
    public long LuckyDamage { get; set; }
    public long NormalDamage { get; set; }
    
    // Helper for display
    public bool IsMultiHit => Count > 1;
    public bool HasCrit => CritCount > 0;
    public bool HasLucky => LuckyCount > 0;
    
    // ? 计算属性：暴击率
    public double CritRate => Count > 0 ? (double)CritCount / Count * 100 : 0;
    
    // ? 计算属性：幸运一击率
    public double LuckyRate => Count > 0 ? (double)LuckyCount / Count * 100 : 0;
    
    // ? 新增：详细信息用于悬浮提示
    public string DetailInfo
    {
        get
        {
            if (Count <= 1)
                return $"{SkillName}\n伤害: {TotalValue:N0}";
            
            var avgDamage = TotalValue / Count;
            var info = $"{SkillName}\n" +
                       $"总伤害: {TotalValue:N0}\n" +
                       $"命中次数: {Count}\n" +
                       $"平均伤害: {avgDamage:N0}";
            
            if (HasCrit)
            {
                var avgCritDamage = CritCount > 0 ? CritDamage / CritCount : 0;
                info += $"\n暴击次数: {CritCount} ({CritRate:F1}%)";
                info += $"\n暴击伤害: {CritDamage:N0} (平均: {avgCritDamage:N0})";
            }
            
            if (HasLucky)
            {
                var avgLuckyDamage = LuckyCount > 0 ? LuckyDamage / LuckyCount : 0;
                info += $"\n幸运一击: {LuckyCount} ({LuckyRate:F1}%)";
                info += $"\n幸运伤害: {LuckyDamage:N0} (平均: {avgLuckyDamage:N0})";
            }
            
            return info;
        }
    }
}
