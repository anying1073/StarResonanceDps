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
public sealed class DataStorageV2 : IDataStorage
{
    private bool _isServerConnected;

    // ===== Event Batching Support =====
    private readonly object _eventBatchLock = new();
    private bool _hasPendingBattleLogEvents;
    private bool _hasPendingDpsEvents;
    private bool _hasPendingDataEvents;
    private bool _hasPendingPlayerInfoEvents;
    private readonly List<BattleLog> _pendingBattleLogs = new(100);
    private readonly HashSet<long> _pendingPlayerUpdates = new();

    /// <summary>
    /// 当前玩家UUID
    /// </summary>
    public long CurrentPlayerUUID { get; set; }

    /// <summary>
    /// 玩家信息字典 (Key: UID)
    /// </summary>
    private Dictionary<long, PlayerInfo> PlayerInfoDatas { get; } = [];

    /// <summary>
    /// 最后一次战斗日志
    /// </summary>
    private BattleLog? LastBattleLog { get; set; }

    /// <summary>
    /// 全程玩家DPS字典 (Key: UID)
    /// </summary>
    private Dictionary<long, DpsData> FullDpsDatas { get; } = [];

    /// <summary>
    /// 阶段性玩家DPS字典 (Key: UID)
    /// </summary>
    private Dictionary<long, DpsData> SectionedDpsDatas { get; } = [];

    /// <summary>
    /// 强制新分段标记
    /// </summary>
    /// <remarks>
    /// 设置为 true 后将在下一次添加战斗日志时, 强制创建一个新的分段之后重置为 false
    /// </remarks>
    private bool ForceNewBattleSection { get; set; }

    /// <summary>
    /// 当前玩家信息
    /// </summary>
    public PlayerInfo CurrentPlayerInfo { get; private set; } = new();

    /// <summary>
    /// 只读玩家信息字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas => PlayerInfoDatas.AsReadOnly();

    /// <summary>
    /// 只读全程玩家DPS字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => FullDpsDatas.AsReadOnly();

    /// <summary>
    /// 只读全程玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList => FullDpsDatas.Values.ToList().AsReadOnly();

    /// <summary>
    /// 阶段性只读玩家DPS字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas => SectionedDpsDatas.AsReadOnly();

    /// <summary>
    /// 阶段性只读玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList => SectionedDpsDatas.Values.ToList().AsReadOnly();

    /// <summary>
    /// 战斗日志分段超时时间 (默认: 5000ms)
    /// </summary>
    public TimeSpan SectionTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

    /// <summary>
    /// 是否正在监听服务器
    /// </summary>
    public bool IsServerConnected
    {
        get => _isServerConnected;
        set
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
    /// 从文件加载缓存玩家信息
    /// </summary>
    public void LoadPlayerInfoFromFile()
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
    public void SavePlayerInfoToFile()
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
    /// 通过战斗日志构建玩家信息字典
    /// </summary>
    /// <param name="battleLogs">战斗日志</param>
    /// <returns></returns>
    public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs)
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
    public void ClearAllDpsData()
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

    /// <summary>
    /// 标记新的战斗日志分段 (清空阶段性Dps数据)
    /// </summary>
    public void ClearDpsData()
    {
        ForceNewBattleSection = true;

        PrivateClearDpsData();
    }

    /// <summary>
    /// 清除当前玩家信息
    /// </summary>
    public void ClearCurrentPlayerInfo()
    {
        CurrentPlayerInfo = new PlayerInfo();

        DataUpdated?.Invoke();
    }

    /// <summary>
    /// 清除所有玩家信息
    /// </summary>
    public void ClearPlayerInfos()
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
    public void ClearAllPlayerInfos()
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

    /// <summary>
    /// 服务器的监听连接状态变更事件
    /// </summary>
    public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;

    /// <summary>
    /// 玩家信息更新事件
    /// </summary>
    public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;

    /// <summary>
    /// 战斗日志新分段创建事件
    /// </summary>
    public event NewSectionCreatedEventHandler? NewSectionCreated;

    /// <summary>
    /// 战斗日志更新事件
    /// </summary>
    public event BattleLogCreatedEventHandler? BattleLogCreated;

    /// <summary>
    /// DPS数据更新事件
    /// </summary>
    public event DpsDataUpdatedEventHandler? DpsDataUpdated;

    /// <summary>
    /// 数据更新事件 (玩家信息或战斗日志更新时触发)
    /// </summary>
    public event DataUpdatedEventHandler? DataUpdated;

    /// <summary>
    /// 服务器变更事件 (地图变更)
    /// </summary>
    public event ServerChangedEventHandler? ServerChanged;

    /// <summary>
    /// 检查或创建玩家信息
    /// </summary>
    /// <param name="uid"></param>
    /// <returns>是否已经存在; 是: true, 否: false</returns>
    /// <remarks>
    /// 如果传入的 UID 已存在, 则不会进行任何操作;
    /// 否则会创建一个新的 PlayerInfo 并触发 PlayerInfoUpdated 事件
    /// </remarks>
    public bool EnsurePlayer(long uid)
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

        TriggerPlayerInfoUpdatedImmediate(uid);

        return false;
    }

    /// <summary>
    /// 触发玩家信息更新事件
    /// </summary>
    /// <param name="uid">UID</param>
    private void TriggerPlayerInfoUpdated(long uid)
    {
        lock (_eventBatchLock)
        {
            _pendingPlayerUpdates.Add(uid);
            _hasPendingPlayerInfoEvents = true;
            _hasPendingDataEvents = true;
        }
    }

    /// <summary>
    /// Immediately fire player info updated event (used when not in batch mode)
    /// </summary>
    private void TriggerPlayerInfoUpdatedImmediate(long uid)
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
    public (DpsData fullData, DpsData sectionedData) GetOrCreateDpsDataByUid(long uid)
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
    /// Internal method for queue processing - does NOT fire events immediately
    /// Used by BattleLogQueue for batched processing
    /// </summary>
    internal void AddBattleLogInternal(BattleLog log)
    {
        // Process the core logic without firing events
        ProcessBattleLogCore(log, out var sectionFlag);

        // Queue events instead of firing immediately
        lock (_eventBatchLock)
        {
            _pendingBattleLogs.Add(log);
            _hasPendingBattleLogEvents = true;
            _hasPendingDpsEvents = true;
            _hasPendingDataEvents = true;
        }
    }

    /// <summary>
    /// Flush all pending batched events
    /// Called by BattleLogQueue after processing a batch
    /// </summary>
    internal void FlushPendingEvents()
    {
        List<BattleLog> logsToFire;
        HashSet<long> playerUpdates;
        bool hasBattle, hasDps, hasData, hasPlayerInfo;

        lock (_eventBatchLock)
        {
            if (!_hasPendingBattleLogEvents && !_hasPendingDpsEvents &&
                !_hasPendingDataEvents && !_hasPendingPlayerInfoEvents)
                return;

            hasBattle = _hasPendingBattleLogEvents;
            hasDps = _hasPendingDpsEvents;
            hasData = _hasPendingDataEvents;
            hasPlayerInfo = _hasPendingPlayerInfoEvents;

            logsToFire = new List<BattleLog>(_pendingBattleLogs);
            playerUpdates = new HashSet<long>(_pendingPlayerUpdates);

            _pendingBattleLogs.Clear();
            _pendingPlayerUpdates.Clear();
            _hasPendingBattleLogEvents = false;
            _hasPendingDpsEvents = false;
            _hasPendingDataEvents = false;
            _hasPendingPlayerInfoEvents = false;
        }

        // Fire events outside of lock
        if (hasBattle && logsToFire.Count > 0)
        {
            foreach (var log in logsToFire)
            {
                try
                {
                    BattleLogCreated?.Invoke(log);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"An error occurred during trigger event(BattleLogCreated) => {ex.Message}\r\n{ex.StackTrace}");
                }
            }
        }

        if (hasPlayerInfo && playerUpdates.Count > 0)
        {
            foreach (var uid in playerUpdates)
            {
                if (PlayerInfoDatas.TryGetValue(uid, out var info))
                {
                    try
                    {
                        PlayerInfoUpdated?.Invoke(info);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"An error occurred during trigger event(PlayerInfoUpdated) => {ex.Message}\r\n{ex.StackTrace}");
                    }
                }
            }
        }

        if (hasDps)
        {
            try
            {
                DpsDataUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"An error occurred during trigger event(DpsDataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        if (hasData)
        {
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
    }

    /// <summary>
    /// Core battle log processing logic (extracted to avoid duplication)
    /// Processes data without firing events
    /// </summary>
    private void ProcessBattleLogCore(BattleLog log, out bool sectionFlag)
    {
        var tt = new TimeSpan(log.TimeTicks);
        sectionFlag = false;

        if (LastBattleLog != null)
        {
            var prevTt = new TimeSpan(LastBattleLog.Value.TimeTicks);
            if (tt - prevTt > SectionTimeout || ForceNewBattleSection)
            {
                PrivateClearDpsDataNoEvents();
                sectionFlag = true;
                ForceNewBattleSection = false;
            }
        }

        if (log.IsTargetPlayer)
        {
            if (log.IsHeal)
            {
                var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);
                TrySetSubProfessionBySkillId(log.AttackerUuid, log.SkillID);
                fullData.TotalHeal += log.Value;
                sectionedData.TotalHeal += log.Value;
            }
            else
            {
                var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);
                fullData.TotalTakenDamage += log.Value;
                sectionedData.TotalTakenDamage += log.Value;
            }
        }
        else
        {
            if (!log.IsHeal && log.IsAttackerPlayer)
            {
                var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);
                TrySetSubProfessionBySkillId(log.AttackerUuid, log.SkillID);
                fullData.TotalAttackDamage += log.Value;
                sectionedData.TotalAttackDamage += log.Value;
            }

            {
                var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);
                fullData.TotalTakenDamage += log.Value;
                sectionedData.TotalTakenDamage += log.Value;
                fullData.IsNpcData = true;
                sectionedData.IsNpcData = true;
            }
        }

        LastBattleLog = log;
    }

    /// <summary>
    /// Private method to clear DPS data without firing events
    /// Used internally by event batching
    /// </summary>
    private void PrivateClearDpsDataNoEvents()
    {
        SectionedDpsDatas.Clear();
    }

    /// <summary>
    /// 添加战斗日志 (会自动创建日志分段)
    /// Public method for backwards compatibility - fires events immediately
    /// </summary>
    /// <param name="log">战斗日志</param>
    public void AddBattleLog(BattleLog log)
    {
        ProcessBattleLogCore(log, out var sectionFlag);

        // Fire events immediately for backwards compatibility
        if (sectionFlag)
        {
            try
            {
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
            BattleLogCreated?.Invoke(log);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"An error occurred during trigger event(BattleLogCreated) => {ex.Message}\r\n{ex.StackTrace}");
        }

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

    #region SetPlayerProperties

    /// <summary>
    /// 设置玩家名称
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="name">玩家名称</param>
    public void SetPlayerName(long uid, string name)
    {
        PlayerInfoDatas[uid].Name = name;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家职业ID
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="professionId">职业ID</param>
    public void SetPlayerProfessionID(long uid, int professionId)
    {
        PlayerInfoDatas[uid].ProfessionID = professionId;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家战力
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="combatPower"></param>
    /// <param name="fightPoint">战力</param>
    public void SetPlayerCombatPower(long uid, int combatPower)
    {
        PlayerInfoDatas[uid].CombatPower = combatPower;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家等级
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="level">等级</param>
    public void SetPlayerLevel(long uid, int level)
    {
        PlayerInfoDatas[uid].Level = level;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家 RankLevel
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="rankLevel">RankLevel</param>
    /// <remarks>
    /// 暂不清楚 RankLevel 的具体含义...
    /// </remarks>
    public void SetPlayerRankLevel(long uid, int rankLevel)
    {
        PlayerInfoDatas[uid].RankLevel = rankLevel;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家暴击
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="critical">暴击值</param>
    public void SetPlayerCritical(long uid, int critical)
    {
        PlayerInfoDatas[uid].Critical = critical;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家幸运
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="lucky">幸运值</param>
    public void SetPlayerLucky(long uid, int lucky)
    {
        PlayerInfoDatas[uid].Lucky = lucky;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家当前HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="hp">当前HP</param>
    public void SetPlayerHP(long uid, long hp)
    {
        PlayerInfoDatas[uid].HP = hp;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家最大HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="maxHp">最大HP</param>
    public void SetPlayerMaxHP(long uid, long maxHp)
    {
        PlayerInfoDatas[uid].MaxHP = maxHp;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    #endregion

    /// <summary>
    /// 设置通用基础信息
    /// </summary>
    private (DpsData fullData, DpsData sectionedData) SetLogInfos(long uid, BattleLog log)
    {
        // 检查或创建玩家信息
        EnsurePlayer(uid);

        // 检查或创建玩家战斗日志列表
        var (fullData, sectionedData) = GetOrCreateDpsDataByUid(uid);

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

    private void TrySetSubProfessionBySkillId(long uid, long skillId)
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

    private void PrivateClearDpsData()
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

    public void NotifyServerChanged(string currentServer, string prevServer)
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

    public void Dispose()
    {
        // No resources to dispose currently
    }
}
