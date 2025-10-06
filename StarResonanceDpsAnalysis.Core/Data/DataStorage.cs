using System.Collections.ObjectModel;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Core.Data;

/// <summary>
/// 数据存储
/// </summary>
public static class DataStorage
{
    private static bool _isServerConnected;

    /// <summary>
    /// 当前玩家UUID
    /// </summary>
    internal static long CurrentPlayerUUID { get; set; }

    /// <summary>
    /// 当前玩家信息
    /// </summary>
    public static PlayerInfo CurrentPlayerInfo { get; private set; } = new();

    /// <summary>
    /// 玩家信息字典 (Key: UID)
    /// </summary>
    private static Dictionary<long, PlayerInfo> PlayerInfoDatas { get; } = [];

    /// <summary>
    /// 只读玩家信息字典 (Key: UID)
    /// </summary>
    public static ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas => PlayerInfoDatas.AsReadOnly();

    /// <summary>
    /// 最后一次战斗日志
    /// </summary>
    private static BattleLog? LastBattleLog { get; set; }

    /// <summary>
    /// 全程玩家DPS字典 (Key: UID)
    /// </summary>
    private static Dictionary<long, DpsData> FullDpsDatas { get; } = [];

    /// <summary>
    /// 只读全程玩家DPS字典 (Key: UID)
    /// </summary>
    public static ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => FullDpsDatas.AsReadOnly();

    /// <summary>
    /// 只读全程玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public static IReadOnlyList<DpsData> ReadOnlyFullDpsDataList => FullDpsDatas.Values.ToList().AsReadOnly();

    /// <summary>
    /// 阶段性玩家DPS字典 (Key: UID)
    /// </summary>
    private static Dictionary<long, DpsData> SectionedDpsDatas { get; } = [];

    /// <summary>
    /// 阶段性只读玩家DPS字典 (Key: UID)
    /// </summary>
    public static ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas => SectionedDpsDatas.AsReadOnly();

    /// <summary>
    /// 阶段性只读玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public static IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList => SectionedDpsDatas.Values.ToList().AsReadOnly();

    /// <summary>
    /// 战斗日志分段超时时间 (默认: 5000ms)
    /// </summary>
    public static TimeSpan SectionTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

    /// <summary>
    /// 强制新分段标记
    /// </summary>
    /// <remarks>
    /// 设置为 true 后将在下一次添加战斗日志时, 强制创建一个新的分段之后重置为 false
    /// </remarks>
    private static bool ForceNewBattleSection { get; set; }

    /// <summary>
    /// 是否正在监听服务器
    /// </summary>
    public static bool IsServerConnected
    {
        get => _isServerConnected;
        internal set
        {
            if (_isServerConnected != value)
            {
                _isServerConnected = value;

                try
                {
                    ServerConnectionStateChanged?.Invoke(value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"An error occurred during trigger event(ServerConnectionStateChanged) => {ex.Message}\r\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// 服务器的监听连接状态变更事件
    /// </summary>
    public static event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;

    /// <summary>
    /// 玩家信息更新事件
    /// </summary>
    public static event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;

    /// <summary>
    /// 战斗日志新分段创建事件
    /// </summary>
    public static event NewSectionCreatedEventHandler? NewSectionCreated;

    /// <summary>
    /// 战斗日志更新事件
    /// </summary>
    public static event BattleLogCreatedEventHandler? BattleLogCreated;

    /// <summary>
    /// DPS数据更新事件
    /// </summary>
    public static event DpsDataUpdatedEventHandler? DpsDataUpdated;

    /// <summary>
    /// 数据更新事件 (玩家信息或战斗日志更新时触发)
    /// </summary>
    public static event DataUpdatedEventHandler? DataUpdated;

    /// <summary>
    /// 服务器变更事件 (地图变更)
    /// </summary>
    public static event ServerChangedEventHandler? ServerChanged;

    /// <summary>
    /// 从文件加载缓存玩家信息
    /// </summary>
    /// <param name="relativeFilePath"></param>
    public static void LoadPlayerInfoFromFile()
    {
        var playerInfoCaches = PlayerInfoCacheReader.ReadFile();

        foreach (var playerInfoCache in playerInfoCaches.PlayerInfos)
        {
            if (!PlayerInfoDatas.TryGetValue(playerInfoCache.UID, out var playerInfo))
            {
                playerInfo = new PlayerInfo();
            }

            playerInfo.UID = playerInfoCache.UID;
            playerInfo.ProfessionID ??= playerInfoCache.ProfessionID;
            playerInfo.CombatPower ??= playerInfoCache.CombatPower;
            playerInfo.Critical ??= playerInfoCache.Critical;
            playerInfo.Lucky ??= playerInfoCache.Lucky;
            playerInfo.MaxHP ??= playerInfoCache.MaxHP;

            if (string.IsNullOrEmpty(playerInfo.Name))
            {
                playerInfo.Name = playerInfoCache.Name;
            }

            if (string.IsNullOrEmpty(playerInfo.SubProfessionName))
            {
                playerInfo.SubProfessionName = playerInfoCache.SubProfessionName;
            }

            PlayerInfoDatas[playerInfo.UID] = playerInfo;
        }
    }

    /// <summary>
    /// 保存缓存玩家信息到文件
    /// </summary>
    /// <param name="relativeFilePath"></param>
    public static void SavePlayerInfoToFile()
    {
        try
        {
            LoadPlayerInfoFromFile();
        }
        catch (Exception)
        {
            // 无缓存或缓存篡改直接无视重新保存新文件
        }

        var list = PlayerInfoDatas.Values.ToList();
        PlayerInfoCacheWriter.WriteToFile([.. list]);
    }

    /// <summary>
    /// 检查或创建玩家信息
    /// </summary>
    /// <param name="uid"></param>
    /// <returns>是否已经存在; 是: true, 否: false</returns>
    /// <remarks>
    /// 如果传入的 UID 已存在, 则不会进行任何操作;
    /// 否则会创建一个新的 PlayerInfo 并触发 PlayerInfoUpdated 事件
    /// </remarks>
    internal static bool TestCreatePlayerInfoByUID(long uid)
    {
        /*
         * 因为修改 PlayerInfo 必须触发 PlayerInfoUpdated 事件,
         * 所以不能用 GetOrCreate 的方式来返回 PlayerInfo 对象,
         * 否则会造成外部使用 PlayerInfo 对象后没有触发事件的问题
         * * * * * * * * * * * * * * * * * * * * * * * * * * */

        if (PlayerInfoDatas.ContainsKey(uid))
        {
            return true;
        }

        PlayerInfoDatas[uid] = new PlayerInfo { UID = uid };

        TriggerPlayerInfoUpdated(uid);

        return false;
    }

    /// <summary>
    /// 触发玩家信息更新事件
    /// </summary>
    /// <param name="uid">UID</param>
    private static void TriggerPlayerInfoUpdated(long uid)
    {
        try
        {
            PlayerInfoUpdated?.Invoke(PlayerInfoDatas[uid]);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(PlayerInfoUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 检查或创建玩家战斗日志列表
    /// </summary>
    /// <param name="uid">UID</param>
    /// <returns>是否已经存在; 是: true, 否: false</returns>
    /// <remarks>
    /// 如果传入的 UID 已存在, 则不会进行任何操作;
    /// 否则会创建一个新的对应 UID 的 List<BattleLog>
    /// </remarks>
    internal static (DpsData fullData, DpsData sectionedData) GetOrCreateDpsDataByUID(long uid)
    {
        var fullDpsDataFlag = FullDpsDatas.TryGetValue(uid, out var fullDpsData);
        if (!fullDpsDataFlag)
        {
            fullDpsData = new DpsData { UID = uid };
        }

        var sectionedDpsDataFlag = SectionedDpsDatas.TryGetValue(uid, out var sectionedDpsData);
        if (!sectionedDpsDataFlag)
        {
            sectionedDpsData = new DpsData { UID = uid };
        }

        SectionedDpsDatas[uid] = sectionedDpsData!;
        FullDpsDatas[uid] = fullDpsData!;

        return (fullDpsData!, sectionedDpsData!);
    }

    /// <summary>
    /// 添加战斗日志 (会自动创建日志分段)
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="log">战斗日志</param>
    internal static void AddBattleLog(BattleLog log)
    {
        // 当前封包时间
        var tt = new TimeSpan(log.TimeTicks);
        // 是否创建新战斗分段标记
        var sectionFlag = false;
        if (LastBattleLog != null)
        {
            // 如果 战斗超时 或 强制创建新战斗分段 时, 创建新分段
            var prevTt = new TimeSpan(LastBattleLog.Value.TimeTicks);
            if (tt - prevTt > SectionTimeout || ForceNewBattleSection)
            {
                PrivateClearDpsData();

                sectionFlag = true;

                ForceNewBattleSection = false;
            }
        }

        // 如果目标是玩家
        if (log.IsTargetPlayer)
        {
            // 如果是治疗数据包
            if (log.IsHeal)
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);

                // 尝试通过技能ID设置对应副职
                TrySetSubProfessionBySkillId(log.AttackerUuid, log.SkillID);

                // 叠加治疗量
                fullData.TotalHeal += log.Value;
                sectionedData.TotalHeal += log.Value;
            }
            else
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);

                // 叠加受击伤害
                fullData.TotalTakenDamage += log.Value;
                sectionedData.TotalTakenDamage += log.Value;
            }
        }
        // 如果目标不是玩家
        else
        {
            // 不是 治疗数据包 且 攻击者是玩家
            if (!log.IsHeal && log.IsAttackerPlayer)
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);

                // 尝试通过技能ID设置对应副职
                TrySetSubProfessionBySkillId(log.AttackerUuid, log.SkillID);

                // 叠加输出伤害
                fullData.TotalAttackDamage += log.Value;
                sectionedData.TotalAttackDamage += log.Value;
            }

            // 提升局部, 统一局部变量名
            {
                // 设置通用基础信息
                var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);

                // 叠加受击伤害
                fullData.TotalTakenDamage += log.Value;
                sectionedData.TotalTakenDamage += log.Value;

                // 将Dps数据记录为NPC数据
                fullData.IsNpcData = true;
                sectionedData.IsNpcData = true;
            }
        }

        // 最后一个日志赋值
        LastBattleLog = log;

        // 如果创建新战斗分段
        if (sectionFlag)
        {
            try
            {
                // 触发新战斗分段创建事件
                NewSectionCreated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"An error occurred during trigger event(NewSectionCreated) => {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        try
        {
            // 触发战斗日志创建事件
            BattleLogCreated?.Invoke(log);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(BattleLogCreated) => {ex.Message}\r\n{ex.StackTrace}");
        }

        try
        {
            // 触发DPS数据更新事件
            DpsDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DpsDataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }

        try
        {
            // 触发数据更新事件
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 设置通用基础信息
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="log"></param>
    /// <returns></returns>
    private static (DpsData fullData, DpsData sectionedData) SetLogInfos(long uid, BattleLog log)
    {
        // 检查或创建玩家信息
        TestCreatePlayerInfoByUID(uid);

        // 检查或创建玩家战斗日志列表
        var (fullData, sectionedData) = GetOrCreateDpsDataByUID(uid);

        fullData.StartLoggedTick ??= log.TimeTicks;
        fullData.LastLoggedTick = log.TimeTicks;

        fullData.UpdateSkillData(log.SkillID, skillData =>
        {
            skillData.TotalValue += log.Value;
            skillData.UseTimes += 1;
            skillData.CritTimes += log.IsCritical ? 1 : 0;
            skillData.LuckyTimes += log.IsLucky ? 1 : 0;
        });

        sectionedData.StartLoggedTick ??= log.TimeTicks;
        sectionedData.LastLoggedTick = log.TimeTicks;

        sectionedData.UpdateSkillData(log.SkillID, skillData =>
        {
            skillData.TotalValue += log.Value;
            skillData.UseTimes += 1;
            skillData.CritTimes += log.IsCritical ? 1 : 0;
            skillData.LuckyTimes += log.IsLucky ? 1 : 0;
        });

        return (fullData, sectionedData);
    }

    private static void TrySetSubProfessionBySkillId(long uid, long skillId)
    {
        if (!PlayerInfoDatas.TryGetValue(uid, out var playerInfo))
        {
            return;
        }

        var subProfessionName = skillId.GetSubProfessionBySkillId();
        var spec = skillId.GetClassSpecBySkillId();
        if (!string.IsNullOrEmpty(subProfessionName))
        {
            playerInfo.SubProfessionName = subProfessionName;
            playerInfo.Spec = spec;
            TriggerPlayerInfoUpdated(uid);
        }
    }

    /// <summary>
    /// 通过战斗日志构建玩家信息字典
    /// </summary>
    /// <param name="battleLogs">战斗日志</param>
    /// <returns></returns>
    public static Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs)
    {
        var playerDic = new Dictionary<long, PlayerInfoFileData>();
        foreach (var log in battleLogs)
        {
            if (!playerDic.ContainsKey(log.AttackerUuid) &&
                PlayerInfoDatas.TryGetValue(log.AttackerUuid, out var attackerPlayerInfo))
            {
                playerDic.Add(log.AttackerUuid, attackerPlayerInfo);
            }

            if (!playerDic.ContainsKey(log.TargetUuid) &&
                PlayerInfoDatas.TryGetValue(log.TargetUuid, out var targetPlayerInfo))
            {
                playerDic.Add(log.TargetUuid, targetPlayerInfo);
            }
        }

        return playerDic;
    }

    /// <summary>
    /// 清除所有DPS数据 (包括全程和阶段性)
    /// </summary>
    public static void ClearAllDpsData()
    {
        ForceNewBattleSection = true;
        SectionedDpsDatas.Clear();
        FullDpsDatas.Clear();

        try
        {
            DpsDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DpsDataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private static void PrivateClearDpsData()
    {
        SectionedDpsDatas.Clear();

        try
        {
            DpsDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DpsDataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 标记新的战斗日志分段 (清空阶段性Dps数据)
    /// </summary>
    public static void ClearDpsData()
    {
        ForceNewBattleSection = true;

        PrivateClearDpsData();
    }

    /// <summary>
    /// 清除当前玩家信息
    /// </summary>
    public static void ClearCurrentPlayerInfo()
    {
        CurrentPlayerInfo = new PlayerInfo();

        DataUpdated?.Invoke();
    }

    /// <summary>
    /// 清除所有玩家信息
    /// </summary>
    public static void ClearPlayerInfos()
    {
        PlayerInfoDatas.Clear();

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 清除所有数据 (包括缓存历史)
    /// </summary>
    public static void ClearAllPlayerInfos()
    {
        CurrentPlayerInfo = new PlayerInfo();
        PlayerInfoDatas.Clear();

        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    internal static void InvokeServerChangedEvent(string currentServer, string prevServer)
    {
        try
        {
            ServerChanged?.Invoke(currentServer, prevServer);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(ServerChanged) => {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    #region SetPlayerProperties

    /// <summary>
    /// 设置玩家名称
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="name">玩家名称</param>
    internal static void SetPlayerName(long uid, string name)
    {
        PlayerInfoDatas[uid].Name = name;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家职业ID
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="professionId">职业ID</param>
    internal static void SetPlayerProfessionID(long uid, int professionId)
    {
        PlayerInfoDatas[uid].ProfessionID = professionId;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家战力
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="fightPoint">战力</param>
    internal static void SetPlayerCombatPower(long uid, int combatPower)
    {
        PlayerInfoDatas[uid].CombatPower = combatPower;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家等级
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="level">等级</param>
    internal static void SetPlayerLevel(long uid, int level)
    {
        PlayerInfoDatas[uid].Level = level;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家 RankLevel
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="rankLevel">RankLevel</param>
    /// <remarks>
    /// 暂不清楚 RankLevel 的具体含义...
    /// </remarks>
    internal static void SetPlayerRankLevel(long uid, int rankLevel)
    {
        PlayerInfoDatas[uid].RankLevel = rankLevel;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家暴击
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="critical">暴击值</param>
    internal static void SetPlayerCritical(long uid, int critical)
    {
        PlayerInfoDatas[uid].Critical = critical;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家幸运
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="lucky">幸运值</param>
    internal static void SetPlayerLucky(long uid, int lucky)
    {
        PlayerInfoDatas[uid].Lucky = lucky;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家当前HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="hp">当前HP</param>
    internal static void SetPlayerHP(long uid, long hp)
    {
        PlayerInfoDatas[uid].HP = hp;

        TriggerPlayerInfoUpdated(uid);
    }

    /// <summary>
    /// 设置玩家最大HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="maxHp">最大HP</param>
    internal static void SetPlayerMaxHP(long uid, long maxHp)
    {
        PlayerInfoDatas[uid].MaxHP = maxHp;

        TriggerPlayerInfoUpdated(uid);
    }

    #endregion
}