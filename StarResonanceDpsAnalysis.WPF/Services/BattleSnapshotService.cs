using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// 战斗快照服务 - 负责保存和加载战斗快照
/// </summary>
public class BattleSnapshotService
{
    private const int AbsoluteMinDurationSeconds = 10; // 绝对最小战斗时长(秒),低于此值的战斗永远不保存
    private readonly IConfigManager _configManager;
    private readonly ILogger<BattleSnapshotService> _logger;
    private readonly string _snapshotDirectory;

    public BattleSnapshotService(ILogger<BattleSnapshotService> logger, IConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        _snapshotDirectory = Path.Combine(Environment.CurrentDirectory, "BattleSnapshots");

        // 确保目录存在
        if (!Directory.Exists(_snapshotDirectory))
        {
            Directory.CreateDirectory(_snapshotDirectory);
        }

        // 启动时加载现有快照
        LoadSnapshots();
    }

    private int MaxSnapshots => _configManager.CurrentConfig.MaxHistoryCount;

    /// <summary>
    /// 当前战斗快照列表(最新的N条，N由配置决定)
    /// </summary>
    public List<BattleSnapshotData> CurrentSnapshots { get; } = new();

    /// <summary>
    /// 全程快照列表(最新的N条，N由配置决定)
    /// </summary>
    public List<BattleSnapshotData> TotalSnapshots { get; } = new();

    /// <summary>
    /// 保存当前战斗快照
    /// </summary>
    /// <param name="storage">数据存储</param>
    /// <param name="duration">战斗时长</param>
    /// <param name="minDurationSeconds">用户设置的最小时长(秒),0表示记录所有(默认记录所有)</param>
    /// <param name="forceUseFullData">强制使用FullDpsData(用于脱战时sectioned数据已被清空的情况)</param>
    public void SaveCurrentSnapshot(IDataStorage storage, TimeSpan duration, int minDurationSeconds = 0,
        bool forceUseFullData = false)
    {
        // ⭐ 硬性限制: 低于10秒的战斗永远不保存
        if (duration.TotalSeconds < AbsoluteMinDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足{Min}秒({Actual:F1}秒),跳过保存当前快照(硬性限制)",
                AbsoluteMinDurationSeconds, duration.TotalSeconds);
            return;
        }

        // ⭐ 用户设置的过滤条件(可选)
        if (minDurationSeconds > 0 && duration.TotalSeconds < minDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足用户设置的{UserMin}秒({Actual:F1}秒),跳过保存当前快照(用户设置)",
                minDurationSeconds, duration.TotalSeconds);
            return;
        }

        try
        {
            // ⭐ 关键修复: 如果forceUseFullData=true,则使用FullDpsData创建快照
            var snapshot = forceUseFullData
                ? CreateSnapshotFromFullData(storage, duration, ScopeType.Current)
                : CreateSnapshot(storage, duration, ScopeType.Current);

            // 保存到磁盘
            SaveSnapshotToDisk(snapshot);

            // 添加到内存列表(插入到开头)
            CurrentSnapshots.Insert(0, snapshot);

            // ⭐ 只保留最新的8条,超出的释放内存并删除磁盘文件
            while (CurrentSnapshots.Count > MaxSnapshots)
            {
                var oldest = CurrentSnapshots[CurrentSnapshots.Count - 1];
                CurrentSnapshots.RemoveAt(CurrentSnapshots.Count - 1);

                // 删除对应的磁盘文件
                TryDeleteSnapshotFile(oldest.FilePath);

                _logger.LogDebug("移除旧快照: {Time}, 文件已删除", oldest.StartedAt);
            }

            _logger.LogInformation("保存当前战斗快照成功: {Time}, 时长: {Duration:F1}秒, 数据源: {Source}, 当前保存数量: {Count}/{Max}",
                snapshot.StartedAt, duration.TotalSeconds, forceUseFullData ? "FullData" : "SectionedData",
                CurrentSnapshots.Count, MaxSnapshots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存当前战斗快照失败");
        }
    }

    /// <summary>
    /// 强制从FullDpsData创建快照(用于脱战时sectioned数据已被清空的场景)
    /// </summary>
    private BattleSnapshotData CreateSnapshotFromFullData(IDataStorage storage, TimeSpan duration, ScopeType scopeType)
    {
        var now = DateTime.Now;
        var players = new Dictionary<long, SnapshotPlayerData>();

        // ⭐ 强制使用FullDpsData
        var dpsList = storage.GetStatistics(true);

        ulong teamTotalDamage = 0;
        ulong teamTotalHealing = 0;
        ulong teamTotalTaken = 0;

        foreach (var dpsData in dpsList.Values)
        {
            storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.Uid, out var playerInfo);

            var damage = (ulong)Math.Max(0, dpsData.TakenDamage.Total);
            var healing = (ulong)Math.Max(0, dpsData.Healing.Total);
            var taken = (ulong)Math.Max(0, dpsData.TakenDamage.Total);

            teamTotalDamage += damage;
            teamTotalHealing += healing;
            teamTotalTaken += taken;

            var elapsedTicks = dpsData.ElapsedTicks();
            var elapsedSeconds =
                elapsedTicks > 0 ? TimeSpan.FromTicks(elapsedTicks).TotalSeconds : duration.TotalSeconds;

            // ⭐ 保存技能数据
            var damageSkills = BuildSkillSnapshot(dpsData.AttackDamage.Skills, SkillType.Damage);
            var healingSkills = BuildSkillSnapshot(dpsData.Healing.Skills, SkillType.Heal);
            var takenSkills = BuildSkillSnapshot(dpsData.TakenDamage.Skills, SkillType.TakenDamage);

            players[dpsData.Uid] = new SnapshotPlayerData
            {
                Uid = dpsData.Uid,
                Nickname = playerInfo?.Name ?? $"UID: {dpsData.Uid}",
                CombatPower = playerInfo?.CombatPower ?? 0,
                Profession = playerInfo?.Class.ToString() ?? "Unknown",
                SubProfession = playerInfo?.SubProfessionName ?? "",
                TotalDamage = damage,
                TotalDps = elapsedSeconds > 0 ? damage / elapsedSeconds : 0,
                TotalHealing = healing,
                TotalHps = elapsedSeconds > 0 ? healing / elapsedSeconds : 0,
                TakenDamage = taken,
                IsNpc = dpsData.IsNpc,

                // ⭐ 保存技能列表
                DamageSkills = damageSkills,
                HealingSkills = healingSkills,
                TakenSkills = takenSkills
            };
        }

        return new BattleSnapshotData
        {
            ScopeType = scopeType,
            StartedAt = now.AddTicks(-duration.Ticks),
            EndedAt = now,
            Duration = duration,
            TeamTotalDamage = teamTotalDamage,
            TeamTotalHealing = teamTotalHealing,
            TeamTotalTakenDamage = teamTotalTaken,
            Players = players
        };
    }

    /// <summary>
    /// 保存全程快照
    /// </summary>
    /// <param name="storage">数据存储</param>
    /// <param name="duration">战斗时长</param>
    /// <param name="minDurationSeconds">用户设置的最小时长(秒),0表示记录所有(默认记录所有)</param>
    public void SaveTotalSnapshot(IDataStorage storage, TimeSpan duration, int minDurationSeconds = 0)
    {
        // ? 硬性限制: 低于10秒的战斗永远不保存
        if (duration.TotalSeconds < AbsoluteMinDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足{Min}秒({Actual:F1}秒),跳过保存全程快照(硬性限制)",
                AbsoluteMinDurationSeconds, duration.TotalSeconds);
            return;
        }

        // ? 用户设置的过滤条件(可选)
        if (minDurationSeconds > 0 && duration.TotalSeconds < minDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足用户设置的{UserMin}秒({Actual:F1}秒),跳过保存全程快照(用户设置)",
                minDurationSeconds, duration.TotalSeconds);
            return;
        }

        try
        {
            var snapshot = CreateSnapshot(storage, duration, ScopeType.Total);

            // 保存到磁盘
            SaveSnapshotToDisk(snapshot);

            // 添加到内存列表(插入到开头)
            TotalSnapshots.Insert(0, snapshot);

            // ? 只保留最新的8条,超出的释放内存并删除磁盘文件
            while (TotalSnapshots.Count > MaxSnapshots)
            {
                var oldest = TotalSnapshots[TotalSnapshots.Count - 1];
                TotalSnapshots.RemoveAt(TotalSnapshots.Count - 1);

                // 删除对应的磁盘文件
                TryDeleteSnapshotFile(oldest.FilePath);

                _logger.LogDebug("移除旧快照: {Time}, 文件已删除", oldest.StartedAt);
            }

            _logger.LogInformation("保存全程快照成功: {Time}, 时长: {Duration:F1}秒, 当前保存数量: {Count}/{Max}",
                snapshot.StartedAt, duration.TotalSeconds, TotalSnapshots.Count, MaxSnapshots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存全程快照失败");
        }
    }

    /// <summary>
    /// 创建快照
    /// </summary>
    private BattleSnapshotData CreateSnapshot(IDataStorage storage, TimeSpan duration, ScopeType scopeType)
    {
        var now = DateTime.Now;
        var players = new Dictionary<long, SnapshotPlayerData>();

        // 根据类型选择数据源
        var dpsList = storage.GetStatistics(scopeType == ScopeType.Total);

        ulong teamTotalDamage = 0;
        ulong teamTotalHealing = 0;
        ulong teamTotalTaken = 0;

        foreach (var dpsData in dpsList.Values)
        {
            storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.Uid, out var playerInfo);

            var damage = (ulong)Math.Max(0, dpsData.AttackDamage.Total);
            var healing = (ulong)Math.Max(0, dpsData.Healing.Total);
            var taken = (ulong)Math.Max(0, dpsData.TakenDamage.Total);

            teamTotalDamage += damage;
            teamTotalHealing += healing;
            teamTotalTaken += taken;

            var elapsedTicks = dpsData.ElapsedTicks();
            var elapsedSeconds =
                elapsedTicks > 0 ? TimeSpan.FromTicks(elapsedTicks).TotalSeconds : duration.TotalSeconds;

            // ? 新增: 保存技能数据
            var damageSkills = BuildSkillSnapshot(dpsData.AttackDamage.Skills, SkillType.Damage);
            var healingSkills = BuildSkillSnapshot(dpsData.Healing.Skills, SkillType.Heal);
            var takenSkills = BuildSkillSnapshot(dpsData.TakenDamage.Skills, SkillType.TakenDamage);

            players[dpsData.Uid] = new SnapshotPlayerData
            {
                Uid = dpsData.Uid,
                Nickname = playerInfo?.Name ?? $"UID: {dpsData.Uid}",
                CombatPower = playerInfo?.CombatPower ?? 0,
                Profession = playerInfo?.Class.ToString() ?? "Unknown",
                SubProfession = playerInfo?.SubProfessionName ?? "",
                TotalDamage = damage,
                TotalDps = elapsedSeconds > 0 ? damage / elapsedSeconds : 0,
                TotalHealing = healing,
                TotalHps = elapsedSeconds > 0 ? healing / elapsedSeconds : 0,
                TakenDamage = taken,
                IsNpc = dpsData.IsNpc,

                // ? 新增: 保存技能列表
                DamageSkills = damageSkills,
                HealingSkills = healingSkills,
                TakenSkills = takenSkills
            };
        }

        return new BattleSnapshotData
        {
            ScopeType = scopeType,
            StartedAt = now.AddTicks(-duration.Ticks),
            EndedAt = now,
            Duration = duration,
            TeamTotalDamage = teamTotalDamage,
            TeamTotalHealing = teamTotalHealing,
            TeamTotalTakenDamage = teamTotalTaken,
            Players = players
        };
    }

    /// <summary>
    /// 从技能列表构建伤害技能快照
    /// </summary>
    private List<SnapshotSkillData> BuildSkillSnapshot(IDictionary<long, SkillStatistics> skills, SkillType targetType)
    {
        var result = new List<SnapshotSkillData>();

        foreach (var skill in skills.Values)
        {
            // 根据技能ID判断类型
            var skillType = EmbeddedSkillConfig.GetTypeOf((int)skill.SkillId);
            if (skillType != targetType)
                continue;

            result.Add(new SnapshotSkillData
            {
                SkillId = skill.SkillId,
                SkillName = EmbeddedSkillConfig.GetName((int)skill.SkillId),
                TotalValue = (ulong)Math.Max(0, skill.TotalValue),
                UseTimes = skill.UseTimes,
                CritTimes = skill.CritTimes,
                LuckyTimes = skill.LuckyTimes
            });
        }

        return result;
    }

    /// <summary>
    /// 从战斗日志构建承伤技能快照
    /// </summary>
    private List<SnapshotSkillData> BuildTakenSkillSnapshot(IReadOnlyList<BattleLog> logs, long targetUid)
    {
        var skillDict = new Dictionary<long, SnapshotSkillData>();

        foreach (var log in logs)
        {
            // 只统计目标是当前玩家的伤害
            if (log.IsHeal || log.TargetUuid != targetUid)
                continue;

            if (!skillDict.TryGetValue(log.SkillID, out var skillData))
            {
                skillData = new SnapshotSkillData
                {
                    SkillId = log.SkillID,
                    SkillName = EmbeddedSkillConfig.GetName((int)log.SkillID)
                };
                skillDict[log.SkillID] = skillData;
            }

            skillData.TotalValue += (ulong)Math.Max(0, log.Value);
            skillData.UseTimes++;
            if (log.IsCritical) skillData.CritTimes++;
            if (log.IsLucky) skillData.LuckyTimes++;
        }

        return skillDict.Values.OrderByDescending(s => s.TotalValue).ToList();
    }

    /// <summary>
    /// 保存快照到磁盘
    /// </summary>
    private void SaveSnapshotToDisk(BattleSnapshotData snapshot)
    {
        var fileName = $"{snapshot.ScopeType}_{snapshot.StartedAt:yyyy-MM-dd_HH-mm-ss}.json";
        var filePath = Path.Combine(_snapshotDirectory, fileName);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(filePath, json);
        snapshot.FilePath = filePath;
    }

    /// <summary>
    /// 从磁盘加载快照
    /// </summary>
    private void LoadSnapshots()
    {
        try
        {
            if (!Directory.Exists(_snapshotDirectory))
            {
                return;
            }

            var files = Directory.GetFiles(_snapshotDirectory, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var snapshot = JsonSerializer.Deserialize<BattleSnapshotData>(json);

                    if (snapshot != null)
                    {
                        snapshot.FilePath = file;

                        if (snapshot.ScopeType == ScopeType.Current)
                        {
                            if (CurrentSnapshots.Count < MaxSnapshots)
                            {
                                CurrentSnapshots.Add(snapshot);
                            }
                            else
                            {
                                // ? 超出限制,删除文件并释放内存
                                File.Delete(file);
                                _logger.LogDebug("启动时删除超出限制的旧快照文件: {File}", file);
                            }
                        }
                        else
                        {
                            if (TotalSnapshots.Count < MaxSnapshots)
                            {
                                TotalSnapshots.Add(snapshot);
                            }
                            else
                            {
                                // ? 超出限制,删除文件并释放内存
                                File.Delete(file);
                                _logger.LogDebug("启动时删除超出限制的旧快照文件: {File}", file);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "加载快照文件失败: {File}", file);
                    // 损坏的文件直接删除
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }
            }

            _logger.LogInformation("加载快照完成: 当前={Current}/{MaxCurrent}, 全程={Total}/{MaxTotal}",
                CurrentSnapshots.Count, MaxSnapshots, TotalSnapshots.Count, MaxSnapshots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载快照失败");
        }
    }

    /// <summary>
    /// 尝试删除快照文件
    /// </summary>
    private void TryDeleteSnapshotFile(string filePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("成功删除快照文件: {File}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除快照文件失败: {File}", filePath);
        }
    }
}

/// <summary>
/// 快照数据模型
/// </summary>
public class BattleSnapshotData
{
    public ScopeType ScopeType { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public ulong TeamTotalDamage { get; set; }
    public ulong TeamTotalHealing { get; set; }
    public ulong TeamTotalTakenDamage { get; set; }
    public Dictionary<long, SnapshotPlayerData> Players { get; set; } = new();

    /// <summary>
    /// 文件路径(不序列化)
    /// </summary>
    [JsonIgnore]
    public string FilePath { get; set; } = "";

    /// <summary>
    /// 显示标签
    /// </summary>
    [JsonIgnore]
    public string DisplayLabel =>
        $"{(ScopeType == ScopeType.Current ? "当前" : "全程")} {StartedAt:HH:mm:ss} ({Duration:mm\\:ss})";
}

/// <summary>
/// 快照玩家数据
/// </summary>
public class SnapshotPlayerData
{
    public long Uid { get; set; }
    public string Nickname { get; set; } = "";
    public int CombatPower { get; set; }
    public string Profession { get; set; } = "";
    public string SubProfession { get; set; } = "";
    public ulong TotalDamage { get; set; }
    public double TotalDps { get; set; }
    public ulong TotalHealing { get; set; }
    public double TotalHps { get; set; }
    public ulong TakenDamage { get; set; }
    public bool IsNpc { get; set; }

    public List<SnapshotSkillData> DamageSkills { get; set; } = new();
    public List<SnapshotSkillData> HealingSkills { get; set; } = new();
    public List<SnapshotSkillData> TakenSkills { get; set; } = new();
}

/// <summary>
/// ? 新增: 快照技能数据
/// </summary>
public class SnapshotSkillData
{
    public long SkillId { get; set; }
    public string SkillName { get; set; } = "";
    public ulong TotalValue { get; set; }
    public int UseTimes { get; set; }
    public int CritTimes { get; set; }
    public int LuckyTimes { get; set; }
}

/// <summary>
/// 快照类型
/// </summary>
public enum ScopeType
{
    Current,
    Total
}