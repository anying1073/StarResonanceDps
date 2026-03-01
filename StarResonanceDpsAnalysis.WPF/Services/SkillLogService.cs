using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace StarResonanceDpsAnalysis.WPF.Services;

public class SkillLogService : ISkillLogService, IDisposable
{
    private readonly IDataStorage _dataStorage;
    private readonly IConfigManager _configManager;
    private bool _disposed;

    public ObservableCollection<SkillLogItem> Logs { get; } = new();

    public SkillLogService(IDataStorage dataStorage, IConfigManager configManager)
    {
        _dataStorage = dataStorage;
        _configManager = configManager;
        _dataStorage.BattleLogCreated += OnBattleLogCreated;
        _dataStorage.NewSectionCreated += OnNewSectionCreated;
    }

    private void OnBattleLogCreated(BattleLog battleLog)
    {
        // ? 修改：使用配置的 UID 来判断是否是当前玩家的技能
        var currentPlayerUid = _configManager.CurrentConfig.Uid;
        
        // 如果 UID 未设置（为 0），则不记录任何技能
        if (currentPlayerUid == 0)
            return;
        
        // 只记录当前玩家的技能（通过 UID 匹配）
        if (battleLog.AttackerUuid != currentPlayerUid)
            return;

        // 获取技能名称
        var skillName = LocalizationManager.Instance.GetString($"JsonDictionary:Skills:{(int)battleLog.SkillID}");
        if (string.IsNullOrEmpty(skillName) || skillName == battleLog.SkillID.ToString())
            skillName = $"Unknown ({battleLog.SkillID})";

        // 将 Ticks 转换为 DateTime
        var timestamp = new DateTime(battleLog.TimeTicks, DateTimeKind.Utc).ToLocalTime();

        Application.Current?.Dispatcher.Invoke(() =>
        {
            // ? 修改：在 800 毫秒内查找相同的技能并合并
            SkillLogItem? existingItem = null;
            var recentCount = Math.Min(10, Logs.Count);
            for (int i = Logs.Count - 1; i >= Math.Max(0, Logs.Count - recentCount); i--)
            {
                var item = Logs[i];
                if (item.SkillName == skillName && 
                    (timestamp - item.Timestamp).TotalMilliseconds <= 800)
                {
                    existingItem = item;
                    break;
                }
            }

            if (existingItem != null && existingItem.IsHeal == battleLog.IsHeal)
            {
                // 合并到现有记录
                existingItem.TotalValue += battleLog.Value;
                existingItem.Count++;
                
                // ? 新增：统计不同类型的伤害
                if (battleLog.IsCritical)
                {
                    existingItem.CritCount++;
                    existingItem.CritDamage += battleLog.Value;
                }
                else if (battleLog.IsLucky)
                {
                    existingItem.LuckyCount++;
                    existingItem.LuckyDamage += battleLog.Value;
                }
                else
                {
                    existingItem.NormalDamage += battleLog.Value;
                }
                
                // 触发 UI 更新
                var index = Logs.IndexOf(existingItem);
                if (index >= 0)
                {
                    Logs[index] = new SkillLogItem
                    {
                        Timestamp = existingItem.Timestamp,
                        SkillName = existingItem.SkillName,
                        TotalValue = existingItem.TotalValue,
                        Count = existingItem.Count,
                        CritCount = existingItem.CritCount,
                        LuckyCount = existingItem.LuckyCount,
                        IsHeal = existingItem.IsHeal,
                        CritDamage = existingItem.CritDamage,
                        LuckyDamage = existingItem.LuckyDamage,
                        NormalDamage = existingItem.NormalDamage
                    };
                }
            }
            else
            {
                // 创建新记录
                var logItem = new SkillLogItem
                {
                    Timestamp = timestamp,
                    SkillName = skillName,
                    TotalValue = battleLog.Value,
                    Count = 1,
                    CritCount = battleLog.IsCritical ? 1 : 0,
                    LuckyCount = battleLog.IsLucky ? 1 : 0,
                    IsHeal = battleLog.IsHeal,
                    // ? 新增：初始化伤害统计
                    CritDamage = battleLog.IsCritical ? battleLog.Value : 0,
                    LuckyDamage = battleLog.IsLucky ? battleLog.Value : 0,
                    NormalDamage = (!battleLog.IsCritical && !battleLog.IsLucky) ? battleLog.Value : 0
                };

                Logs.Add(logItem);

                // 限制记录数量，避免内存占用过大
                if (Logs.Count > 500)
                {
                    Logs.RemoveAt(0);
                }
            }
        });
    }

    private void OnNewSectionCreated()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Logs.Clear();
        });
    }

    public void Clear()
    {
        Logs.Clear();
    }

    public void AddLog(SkillLogItem log)
    {
        Logs.Add(log);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dataStorage.BattleLogCreated -= OnBattleLogCreated;
        _dataStorage.NewSectionCreated -= OnNewSectionCreated;
    }
}
