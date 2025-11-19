using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Tools;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Core.Data;

/// <summary>
/// 数据存储
/// </summary>
public sealed partial class DataStorageV2(ILogger<DataStorageV2> logger) : IDataStorage
{
    // ===== Event Batching Support =====
    private readonly object _eventBatchLock = new();
    private readonly List<BattleLog> _pendingBattleLogs = new(100);
    private readonly HashSet<long> _pendingPlayerUpdates = new();
    private readonly object _sectionTimeoutLock = new();
    // ===== Thread Safety Support =====
    private readonly object _battleLogProcessLock = new();
    private bool _disposed;
    private bool _hasPendingBattleLogEvents;
    private bool _hasPendingDataEvents;
    private bool _hasPendingDpsEvents;
    private bool _hasPendingPlayerInfoEvents;
    private bool _isServerConnected;
    private DateTime _lastLogWallClockAtUtc = DateTime.MinValue;

    // ===== Section timeout monitor =====
    private Timer? _sectionTimeoutTimer;
    private bool _timeoutSectionClearedOnce; // avoid repeated clear/events until next log arrives

    /// <summary>
    /// 玩家信息字典 (Key: UID)
    /// </summary>
    private Dictionary<long, PlayerInfo> PlayerInfoData { get; } = [];

    /// <summary>
    /// 最后一次战斗日志
    /// </summary>
    private BattleLog? LastBattleLog { get; set; }

    /// <summary>
    /// 全程玩家DPS字典 (Key: UID)
    /// </summary>
    private Dictionary<long, DpsData> FullDpsData { get; } = [];

    /// <summary>
    /// 阶段性玩家DPS字典 (Key: UID)
    /// </summary>
    private Dictionary<long, DpsData> SectionedDpsData { get; } = [];

    /// <summary>
    /// 强制新分段标记
    /// </summary>
    /// <remarks>
    /// 设置为 true 后将在下一次添加战斗日志时, 强制创建一个新的分段之后重置为 false
    /// </remarks>
    private bool ForceNewBattleSection { get; set; }

    /// <summary>
    /// 当前玩家UUID
    /// </summary>
    public long CurrentPlayerUUID { get; set; }

    /// <summary>
    /// 当前玩家信息
    /// </summary>
    public PlayerInfo CurrentPlayerInfo { get; private set; } = new();

    /// <summary>
    /// 只读玩家信息字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas => PlayerInfoData.AsReadOnly();

    /// <summary>
    /// 只读全程玩家DPS字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => FullDpsData.AsReadOnly();

    /// <summary>
    /// 只读全程玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList => FullDpsData.Values.ToList().AsReadOnly();

    /// <summary>
    /// 阶段性只读玩家DPS字典 (Key: UID)
    /// </summary>
    public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas => SectionedDpsData.AsReadOnly();

    /// <summary>
    /// 阶段性只读玩家DPS列表; 注意! 频繁读取该属性可能会导致性能问题!
    /// </summary>
    public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList => SectionedDpsData.Values.ToList().AsReadOnly();

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
            if (_isServerConnected == value) return;
            _isServerConnected = value;

            // ensure background timeout monitor is running when connected
            if (value) EnsureSectionMonitorStarted();

            RaiseServerConnectionStateChanged(value);
        }
    }


    /// <summary>
    /// 从文件加载缓存玩家信息
    /// </summary>
    public void LoadPlayerInfoFromFile()
    {
        PlayerInfoCacheFileV3_0_0 playerInfoCaches;
        try
        {
            playerInfoCaches = PlayerInfoCacheReader.ReadFile();
        }
        catch (FileNotFoundException)
        {
            logger.LogInformation("Player info cache file not exist, abort load");
            return;
        }

        foreach (var playerInfoCache in playerInfoCaches.PlayerInfos)
        {
            if (!PlayerInfoData.TryGetValue(playerInfoCache.UID, out var playerInfo))
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

            PlayerInfoData[playerInfo.UID] = playerInfo;
        }
    }

    /// <summary>
    /// 保存缓存玩家信息到文件
    /// </summary>
    public void SavePlayerInfoToFile()
    {
        try
        {
            LoadPlayerInfoFromFile();
        }
        catch (FileNotFoundException)
        {
            // 无缓存或缓存篡改直接无视重新保存新文件
            // File not exist, ignore and write new file
            logger.LogInformation("Player info cache file not exist, write new file");
        }

        var list = PlayerInfoData.Values.ToList();
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
                PlayerInfoData.TryGetValue(log.AttackerUuid, out var attackerPlayerInfo))
            {
                playerDic.Add(log.AttackerUuid, attackerPlayerInfo);
            }

            if (!playerDic.ContainsKey(log.TargetUuid) &&
                PlayerInfoData.TryGetValue(log.TargetUuid, out var targetPlayerInfo))
            {
                playerDic.Add(log.TargetUuid, targetPlayerInfo);
            }
        }

        return playerDic;
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
    public bool EnsurePlayer(long uid)
    {
        /*
         * 因为修改 PlayerInfo 必须触发 PlayerInfoUpdated 事件,
         * 所以不能用 GetOrCreate 的方式来返回 PlayerInfo 对象,
         * 否则会造成外部使用 PlayerInfo 对象后没有触发事件的问题
         * * * * * * * * * * * * * * * * * * * * * * * * * * */

        if (PlayerInfoData.ContainsKey(uid))
        {
            return true;
        }

        PlayerInfoData[uid] = new PlayerInfo { UID = uid };

        TriggerPlayerInfoUpdatedImmediate(uid);

        return false;
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
            RaiseNewSectionCreated();
        }

        RaiseBattleLogCreated(log);
        RaiseDpsDataUpdated();
        RaiseDataUpdated();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _sectionTimeoutTimer?.Dispose();
        }
        catch (Exception ex)
        {
            // ignored
            logger.LogError(ex, "An error occurred during Dispose");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }


    private void EnsureSectionMonitorStarted()
    {
        if (_sectionTimeoutTimer != null) return;
        try
        {
            _sectionTimeoutTimer = new Timer(static s => ((DataStorageV2)s!).SectionTimeoutTick(), this,
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during EnsureSectionMonitorStarted");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }

    private void SectionTimeoutTick()
    {
        CheckSectionTimeout();
    }

    private void CheckSectionTimeout()
    {
        if (_disposed) return;
        DateTime last;
        bool alreadyCleared;
        lock (_sectionTimeoutLock)
        {
            last = _lastLogWallClockAtUtc;
            alreadyCleared = _timeoutSectionClearedOnce;
        }

        if (alreadyCleared) return;
        if (last == DateTime.MinValue) return; // no logs yet

        var now = DateTime.UtcNow;
        if (now - last <= SectionTimeout) return;

        // Timeout reached: clear section and notify
        try
        {
            PrivateClearDpsData(); // raises DpsDataUpdated & DataUpdated
            RaiseNewSectionCreated();
        }
        finally
        {
            lock (_sectionTimeoutLock)
            {
                _timeoutSectionClearedOnce = true;
            }
        }
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
            PlayerInfoUpdated?.Invoke(PlayerInfoData[uid]);
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
    /// 否则会创建一个新的对应 UID 的 <![CDATA[List<BattleLog>]]>
    /// </remarks>
    public (DpsData fullData, DpsData sectionedData) GetOrCreateDpsDataByUid(long uid)
    {
        var fullDpsDataFlag = FullDpsData.TryGetValue(uid, out var fullDpsData);
        if (!fullDpsDataFlag)
        {
            fullDpsData = new DpsData { UID = uid };
        }

        var sectionedDpsDataFlag = SectionedDpsData.TryGetValue(uid, out var sectionedDpsData);
        if (!sectionedDpsDataFlag)
        {
            sectionedDpsData = new DpsData { UID = uid };
        }

        SectionedDpsData[uid] = sectionedDpsData!;
        FullDpsData[uid] = fullDpsData!;

        return (fullDpsData!, sectionedDpsData!);
    }

    /// <summary>
    /// Internal method for queue processing - does NOT fire events immediately
    /// Used by BattleLogQueue for batched processing
    /// </summary>
    internal void AddBattleLogInternal(BattleLog log)
    {
        // Process the core logic without firing events
        ProcessBattleLogCore(log, out _);

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
                RaiseBattleLogCreated(log);
            }
        }

        if (hasPlayerInfo && playerUpdates.Count > 0)
        {
            foreach (var uid in playerUpdates)
            {
                if (PlayerInfoData.TryGetValue(uid, out var info))
                {
                    RaisePlayerInfoUpdated(info);
                }
            }
        }

        if (hasDps)
        {
            RaiseDpsDataUpdated();
        }

        if (hasData)
        {
            RaiseDataUpdated();
        }
    }

    /// <summary>
    /// Core battle log processing logic (extracted to avoid duplication)
    /// Processes data without firing events
    /// </summary>
    /// <param name="log">The battle log to process</param>
    /// <param name="sectionFlag">Output parameter indicating if a new section was created due to timeout</param>
    /// <remarks>
    /// This method handles the core business logic for processing battle logs:
    /// <list type="bullet">
    /// <item><description>Checks if a timeout occurred since the last log and creates a new section if needed</description></item>
    /// <item><description>Routes the log to the appropriate processor based on log type (player target, player attack, or NPC)</description></item>
    /// <item><description>Updates internal state tracking for section timeout monitoring</description></item>
    /// </list>
    /// Thread-safe: This method uses internal locking to ensure concurrent calls are properly synchronized.
    /// </remarks>
    private void ProcessBattleLogCore(BattleLog log, out bool sectionFlag)
    {
        // Thread-safety: Lock to prevent concurrent modification of shared state
        lock (_battleLogProcessLock)
        {
            sectionFlag = CheckAndHandleSectionTimeout(log);

            ProcessLogByType(log);

            UpdateLastLogState(log);
        }
    }

    /// <summary>
    /// Checks if the time since the last battle log exceeds the section timeout threshold
 /// and creates a new section if necessary
    /// </summary>
    /// <param name="log">The current battle log being processed</param>
    /// <returns>True if a new section was created; otherwise, false</returns>
    /// <remarks>
    /// A new section is created when:
    /// <list type="bullet">
    /// <item><description>The time difference between logs exceeds <see cref="SectionTimeout"/></description></item>
    /// <item><description>The <see cref="ForceNewBattleSection"/> flag is set</description></item>
    /// </list>
    /// When a new section is created, the sectioned DPS data is cleared without firing events.
    /// </remarks>
    private bool CheckAndHandleSectionTimeout(BattleLog log)
    {
        if (LastBattleLog == null)
            return false;

        // Compare ticks directly instead of creating TimeSpan objects for better performance
        var timeSinceLastLog = log.TimeTicks - LastBattleLog.Value.TimeTicks;

        if (timeSinceLastLog > SectionTimeout.Ticks || ForceNewBattleSection)
        {
            PrivateClearDpsDataNoEvents();
            ForceNewBattleSection = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Routes the battle log to the appropriate processing method based on log characteristics
    /// </summary>
    /// <param name="log">The battle log to process</param>
    /// <remarks>
    /// Processing logic:
    /// <list type="bullet">
    /// <item><description>If target is a player: Processes as player target (heal or damage taken)</description></item>
    /// <item><description>If attacker is a player and not healing: Processes as player attack (damage dealt)</description></item>
    /// <item><description>Otherwise: Processes as NPC damage taken</description></item>
    /// </list>
    /// </remarks>
    private void ProcessLogByType(BattleLog log)
    {
        if (log.IsTargetPlayer)
        {
            ProcessPlayerTargetLog(log);
        }
        else if (log.IsAttackerPlayer && !log.IsHeal)
        {
            ProcessPlayerAttackLog(log);
        }
        else
        {
            ProcessNpcLog(log);
        }
    }

    /// <summary>
    /// Processes battle logs where the target is a player
    /// </summary>
    /// <param name="log">The battle log to process</param>
    /// <remarks>
    /// Handles two scenarios:
    /// <list type="bullet">
    /// <item><description>Healing: Updates heal statistics for the attacker (healer)</description></item>
    /// <item><description>Damage: Updates damage taken statistics for the target</description></item>
    /// </list>
    /// For healing, also attempts to determine the healer's sub-profession based on skill ID.
    /// </remarks>
    private void ProcessPlayerTargetLog(BattleLog log)
    {
        if (log.IsHeal)
        {
            var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);
            TrySetSpecBySkillId(log.AttackerUuid, log.SkillID);
            UpdateDpsData(fullData, sectionedData, log, DpsType.Heal);
        }
        else
        {
            var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);
            UpdateDpsData(fullData, sectionedData, log, DpsType.TakenDamage);
        }
    }

    /// <summary>
    /// Processes battle logs where the attacker is a player dealing damage
    /// </summary>
    /// <param name="log">The battle log to process</param>
    /// <remarks>
    /// Updates the attacker's damage dealt statistics and attempts to determine
    /// their sub-profession based on the skill ID used.
    /// </remarks>
    private void ProcessPlayerAttackLog(BattleLog log)
    {
        var (fullData, sectionedData) = SetLogInfos(log.AttackerUuid, log);
        TrySetSpecBySkillId(log.AttackerUuid, log.SkillID);
        UpdateDpsData(fullData, sectionedData, log, DpsType.AttackDamage);
    }

    /// <summary>
    /// Processes battle logs for NPC-related damage
    /// </summary>
    /// <param name="log">The battle log to process</param>
    /// <remarks>
    /// Marks the data as NPC-related and updates damage taken statistics.
    /// This typically handles cases where damage is dealt to NPCs.
    /// </remarks>
    private void ProcessNpcLog(BattleLog log)
    {
        var (fullData, sectionedData) = SetLogInfos(log.TargetUuid, log);
        fullData.IsNpcData = true;
        sectionedData.IsNpcData = true;
        UpdateDpsData(fullData, sectionedData, log, DpsType.TakenDamage);
    }

    /// <summary>
    /// Specifies the type of DPS statistic to update
    /// </summary>
    private enum DpsType
    {
        /// <summary>
        /// Damage dealt by attacking
        /// </summary>
        AttackDamage,

        /// <summary>
        /// Damage taken from attacks
        /// </summary>
        TakenDamage,

        /// <summary>
        /// Healing provided
        /// </summary>
        Heal
    }

    /// <summary>
    /// Updates DPS statistics for both full-session and sectioned data
    /// </summary>
    /// <param name="fullData">Full-session DPS data to update</param>
    /// <param name="sectionedData">Sectioned DPS data to update</param>
    /// <param name="log">The battle log containing the values to add</param>
    /// <param name="type">The type of statistic to update</param>
    /// <remarks>
    /// Updates the appropriate totals based on the DPS type and adds the log
    /// to both the full-session and sectioned battle log collections.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if an invalid DpsType is provided</exception>
    private void UpdateDpsData(DpsData fullData, DpsData sectionedData, BattleLog log, DpsType type)
    {
        switch (type)
        {
            case DpsType.AttackDamage:
                fullData.AddTotalAttackDamage(log.Value);
                sectionedData.AddTotalAttackDamage(log.Value);
                break;
            case DpsType.TakenDamage:
                fullData.AddTotalTakenDamage(log.Value);
                sectionedData.AddTotalTakenDamage(log.Value);
                break;
            case DpsType.Heal:
                fullData.AddTotalHeal(log.Value);
                sectionedData.AddTotalHeal(log.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        fullData.AddBattleLog(log);
        sectionedData.AddBattleLog(log);
    }

    /// <summary>
    /// Updates internal state tracking after processing a battle log
    /// </summary>
    /// <param name="log">The battle log that was processed</param>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item><description>Updates the last processed battle log reference</description></item>
    /// <item><description>Resets the section timeout monitoring state</description></item>
    /// <item><description>Ensures the timeout monitor timer is running</description></item>
    /// </list>
    /// The wall clock timestamp is used for timeout detection rather than the log's game timestamp,
    /// allowing the system to detect when no new logs are arriving in real-time.
    /// </remarks>
    private void UpdateLastLogState(BattleLog log)
    {
        LastBattleLog = log;

        lock (_sectionTimeoutLock)
        {
            _lastLogWallClockAtUtc = DateTime.UtcNow;
            _timeoutSectionClearedOnce = false;
        }

        EnsureSectionMonitorStarted();
    }

    /// <summary>
    /// Private method to clear DPS data without firing events
    /// Used internally by event batching
    /// </summary>
    private void PrivateClearDpsDataNoEvents()
    {
        SectionedDpsData.Clear();
    }

    /// <summary>
    /// 设置通用基础信息
    /// </summary>
    private (DpsData fullData, DpsData sectionedData) SetLogInfos(long uid, BattleLog log)
    {
        // 检查或创建玩家信息
        EnsurePlayer(uid);

        // 检查或创建玩家战斗日志列表
        var (fullData, sectionedData) = GetOrCreateDpsDataByUid(uid);

        Update(log, fullData);
        Update(log, sectionedData);

        return (fullData, sectionedData);

        static void Update(BattleLog battleLog, DpsData dpsData)
        {
            dpsData.StartLoggedTick ??= battleLog.TimeTicks;
            dpsData.LastLoggedTick = battleLog.TimeTicks;

            dpsData.UpdateSkillData(battleLog.SkillID, skillData =>
            {
                skillData.IncrementTotalValue(battleLog.Value);
                skillData.IncrementUseTimes();
                if (battleLog.IsCritical) skillData.IncrementCritTimes();
                if (battleLog.IsLucky) skillData.IncrementLuckyTimes();
            });
        }
    }

    private void PrivateClearDpsData()
    {
        SectionedDpsData.Clear();

        RaiseDpsDataUpdated();
        RaiseDataUpdated();
    }

    #region SetPlayerProperties

    private void TrySetSpecBySkillId(long uid, long skillId)
    {
        if (!PlayerInfoData.TryGetValue(uid, out var playerInfo))
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
    /// 设置玩家名称
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="name">玩家名称</param>
    public void SetPlayerName(long uid, string name)
    {
        PlayerInfoData[uid].Name = name;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家职业ID
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="professionId">职业ID</param>
    public void SetPlayerProfessionID(long uid, int professionId)
    {
        PlayerInfoData[uid].ProfessionID = professionId;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家战力
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="combatPower">战力</param>
    public void SetPlayerCombatPower(long uid, int combatPower)
    {
        PlayerInfoData[uid].CombatPower = combatPower;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家等级
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="level">等级</param>
    public void SetPlayerLevel(long uid, int level)
    {
        PlayerInfoData[uid].Level = level;

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
        PlayerInfoData[uid].RankLevel = rankLevel;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家暴击
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="critical">暴击值</param>
    public void SetPlayerCritical(long uid, int critical)
    {
        PlayerInfoData[uid].Critical = critical;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家幸运
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="lucky">幸运值</param>
    public void SetPlayerLucky(long uid, int lucky)
    {
        PlayerInfoData[uid].Lucky = lucky;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家当前HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="hp">当前HP</param>
    public void SetPlayerHP(long uid, long hp)
    {
        PlayerInfoData[uid].HP = hp;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    /// <summary>
    /// 设置玩家最大HP
    /// </summary>
    /// <param name="uid">UID</param>
    /// <param name="maxHp">最大HP</param>
    public void SetPlayerMaxHP(long uid, long maxHp)
    {
        PlayerInfoData[uid].MaxHP = maxHp;

        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    #endregion

    #region Clear data

    /// <summary>
    /// 清除所有DPS数据 (包括全程和阶段性)
    /// </summary>
    public void ClearAllDpsData()
    {
        ForceNewBattleSection = true;
        SectionedDpsData.Clear();
        FullDpsData.Clear();

        RaiseDpsDataUpdated();
        RaiseDataUpdated();
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

        RaiseDataUpdated();
    }

    /// <summary>
    /// 清除所有玩家信息
    /// </summary>
    public void ClearPlayerInfos()
    {
        PlayerInfoData.Clear();

        RaiseDataUpdated();
    }

    /// <summary>
    /// 清除所有数据 (包括缓存历史)
    /// </summary>
    public void ClearAllPlayerInfos()
    {
        CurrentPlayerInfo = new PlayerInfo();
        PlayerInfoData.Clear();

        RaiseDataUpdated();
    }


    #endregion
}

public partial class DataStorageV2
{
    #region Events

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

    public void RaiseServerChanged(string currentServer, string prevServer)
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

    private void RaiseDataUpdated()
    {
        try
        {
            DataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during trigger event(DataUpdated)");
            Console.WriteLine(
                $"An error occurred during trigger event(DataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }

    private void RaiseDpsDataUpdated()
    {
        try
        {
            DpsDataUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during trigger event(DpsDataUpdated)");
            Console.WriteLine(
                $"An error occurred during trigger event(DpsDataUpdated) => {ex.Message}\r\n{ex.StackTrace}");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }

    private void RaiseServerConnectionStateChanged(bool value)
    {
        try
        {
            ServerConnectionStateChanged?.Invoke(value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during trigger event(ServerConnectionStateChanged)");
            Console.WriteLine(
                $"An error occurred during trigger event(ServerConnectionStateChanged) => {ex.Message}\r\n{ex.StackTrace}");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }

    private void RaisePlayerInfoUpdated(PlayerInfo info)
    {
        try
        {
            PlayerInfoUpdated?.Invoke(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during trigger event(PlayerInfoUpdated)");
            Console.WriteLine(
                $"An error occurred during trigger event(PlayerInfoUpdated) => {ex.Message}\r\n{ex.StackTrace}");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }

    private void RaiseBattleLogCreated(BattleLog log)
    {
        try
        {
            BattleLogCreated?.Invoke(log);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during trigger event(BattleLogCreated)");
            Console.WriteLine(
                $"An error occurred during trigger event(BattleLogCreated) => {ex.Message}\r\n{ex.StackTrace}");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }

    private void RaiseNewSectionCreated()
    {
        try
        {
            NewSectionCreated?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during trigger event(NewSectionCreated)");
            Console.WriteLine(
                $"An error occurred during trigger event(NewSectionCreated) => {ex.Message}\r\n{ex.StackTrace}");
            ExceptionHelper.ThrowIfDebug(ex);
        }
    }

    #endregion
}