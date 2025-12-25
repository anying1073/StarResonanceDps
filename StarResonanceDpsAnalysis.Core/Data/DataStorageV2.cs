using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Tools;
using StarResonanceDpsAnalysis.Core.Statistics;

namespace StarResonanceDpsAnalysis.Core.Data;

/// <summary>
/// 数据存储 - 完全基于 StatisticsAdapter 的新架构
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
    
    // ===== Statistics Engine =====
    private readonly StatisticsAdapter _statisticsAdapter = new(logger);
    
    private bool _disposed;
    private bool _hasPendingBattleLogEvents;
    private bool _hasPendingDataEvents;
    private bool _hasPendingDpsEvents;
    private bool _hasPendingPlayerInfoEvents;
    private bool _isServerConnected;
    private DateTime _lastLogWallClockAtUtc = DateTime.MinValue;

    // ===== Section timeout monitor =====
    private Timer? _sectionTimeoutTimer;
    private bool _timeoutSectionClearedOnce;

    /// <summary>
    /// 玩家信息字典 (Key: UID)
    /// </summary>
    private Dictionary<long, PlayerInfo> PlayerInfoData { get; } = [];

    /// <summary>
    /// 最后一次战斗日志
    /// </summary>
    private BattleLog? LastBattleLog { get; set; }

    /// <summary>
    /// 强制新分段标记
    /// </summary>
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
            logger.LogInformation("Player info cache file not exist, write new file");
        }

        var list = PlayerInfoData.Values.ToList();
        PlayerInfoCacheWriter.WriteToFile([.. list]);
    }

    /// <summary>
    /// 通过战斗日志构建玩家信息字典
    /// </summary>
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
    public bool EnsurePlayer(long uid)
    {
        if (PlayerInfoData.ContainsKey(uid))
        {
            return true;
        }

        PlayerInfoData[uid] = new PlayerInfo { UID = uid };
        TriggerPlayerInfoUpdatedImmediate(uid);

        return false;
    }

    /// <summary>
    /// 添加战斗日志 - fires events immediately
    /// </summary>
    public void AddBattleLog(BattleLog log)
    {
        ProcessBattleLogCore(log, out var sectionFlag);

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
        if (last == DateTime.MinValue) return;

        var now = DateTime.UtcNow;
        if (now - last <= SectionTimeout) return;

        var sectionStats = _statisticsAdapter.GetStatistics(fullSession: false);
        if (sectionStats.Count == 0)
        {
            _timeoutSectionClearedOnce = true;
            return;
        }

        try
        {
            BeforeSectionCleared?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during BeforeSectionCleared event");
            ExceptionHelper.ThrowIfDebug(ex);
        }

        try
        {
            _statisticsAdapter.ResetSection();
            RaiseNewSectionCreated();
        }
        finally
        {
            _timeoutSectionClearedOnce = true;
        }
    }

    private void TriggerPlayerInfoUpdated(long uid)
    {
        lock (_eventBatchLock)
        {
            _pendingPlayerUpdates.Add(uid);
            _hasPendingPlayerInfoEvents = true;
            _hasPendingDataEvents = true;
        }
    }

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
    /// Internal method for queue processing - does NOT fire events immediately
    /// </summary>
    internal void AddBattleLogInternal(BattleLog log)
    {
        ProcessBattleLogCore(log, out _);

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
    /// Core battle log processing logic - 完全委托给 StatisticsAdapter
    /// </summary>
    private void ProcessBattleLogCore(BattleLog log, out bool sectionFlag)
    {
        lock (_battleLogProcessLock)
        {
            sectionFlag = CheckAndHandleSectionTimeout(log);

            // ✅ 完全使用 StatisticsAdapter 处理统计数据
            _statisticsAdapter.ProcessLog(log);
            
            // 只负责玩家信息管理
            EnsurePlayer(log.AttackerUuid);
            EnsurePlayer(log.TargetUuid);
            
            if (log.IsAttackerPlayer)
            {
                TrySetSpecBySkillId(log.AttackerUuid, log.SkillID);
            }

            UpdateLastLogState(log);
        }
    }

    private bool CheckAndHandleSectionTimeout(BattleLog log)
    {
        if (LastBattleLog == null)
            return false;

        var timeSinceLastLog = log.TimeTicks - LastBattleLog.TimeTicks;

        if (timeSinceLastLog > SectionTimeout.Ticks || ForceNewBattleSection)
        {
            var sectionStats = _statisticsAdapter.GetStatistics(fullSession: false);
            
            if (sectionStats.Count > 0)
            {
                try
                {
                    BeforeSectionCleared?.Invoke();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred during BeforeSectionCleared event");
                    ExceptionHelper.ThrowIfDebug(ex);
                }
            }

            _statisticsAdapter.ResetSection();
            
            ForceNewBattleSection = false;
            return true;
        }

        return false;
    }

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

    public void SetPlayerName(long uid, string name)
    {
        PlayerInfoData[uid].Name = name;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerProfessionID(long uid, int professionId)
    {
        PlayerInfoData[uid].ProfessionID = professionId;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerCombatPower(long uid, int combatPower)
    {
        PlayerInfoData[uid].CombatPower = combatPower;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerLevel(long uid, int level)
    {
        PlayerInfoData[uid].Level = level;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerRankLevel(long uid, int rankLevel)
    {
        PlayerInfoData[uid].RankLevel = rankLevel;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerCritical(long uid, int critical)
    {
        PlayerInfoData[uid].Critical = critical;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerLucky(long uid, int lucky)
    {
        PlayerInfoData[uid].Lucky = lucky;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerElementFlag(long playerUid, int readInt32)
    {
        PlayerInfoData[playerUid].ElementFlag = readInt32;
        TriggerPlayerInfoUpdatedImmediate(playerUid);
    }

    public void SetPlayerReductionLevel(long playerUid, int readInt32)
    {
        PlayerInfoData[playerUid].ReductionLevel = readInt32;
        TriggerPlayerInfoUpdatedImmediate(playerUid);
    }

    public void SetPlayerEnergyFlag(long playerUid, int readInt32)
    {
        PlayerInfoData[playerUid].EnergyFlag = readInt32;
        TriggerPlayerInfoUpdatedImmediate(playerUid);
    }

    public void SetNpcTemplateId(long playerUid, int templateId)
    {
        PlayerInfoData[playerUid].NpcTemplateId = templateId;
        TriggerPlayerInfoUpdatedImmediate(playerUid);
    }

    public void SetPlayerSeasonLevel(long playerUid, int seasonLevel)
    {
        PlayerInfoData[playerUid].SeasonLevel = seasonLevel;
        TriggerPlayerInfoUpdatedImmediate(playerUid);
    }

    public void SetPlayerSeasonStrength(long playerUid, int seasonStrength)
    {
        PlayerInfoData[playerUid].SeasonStrength = seasonStrength;
        TriggerPlayerInfoUpdatedImmediate(playerUid);
    }

    public void SetPlayerHP(long uid, long hp)
    {
        PlayerInfoData[uid].HP = hp;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    public void SetPlayerMaxHP(long uid, long maxHp)
    {
        PlayerInfoData[uid].MaxHP = maxHp;
        TriggerPlayerInfoUpdatedImmediate(uid);
    }

    #endregion

    #region Clear data

    /// <summary>
    /// 清除所有DPS数据 - 完全委托给 StatisticsAdapter
    /// </summary>
    public void ClearAllDpsData()
    {
        ForceNewBattleSection = true;
        
        // ✅ 完全使用 StatisticsAdapter
        _statisticsAdapter.ClearAll();
        RaiseDpsDataUpdated();
        RaiseDataUpdated();
    }

    /// <summary>
    /// 标记新的战斗日志分段 - 完全委托给 StatisticsAdapter
    /// </summary>
    public void ClearDpsData()
    {
        ForceNewBattleSection = true;
        
        // ✅ 完全使用 StatisticsAdapter
        _statisticsAdapter.ResetSection();

        RaiseDpsDataUpdated();
        RaiseDataUpdated();
    }

    public void ClearCurrentPlayerInfo()
    {
        CurrentPlayerInfo = new PlayerInfo();
        RaiseDataUpdated();
    }

    public void ClearPlayerInfos()
    {
        PlayerInfoData.Clear();
        RaiseDataUpdated();
    }

    public void ClearAllPlayerInfos()
    {
        CurrentPlayerInfo = new PlayerInfo();
        PlayerInfoData.Clear();
        RaiseDataUpdated();
    }

    #endregion
    
    #region Battle Log Access
    
    /// <summary>
    /// Get battle logs for a specific player
    /// </summary>
    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession)
    {
        return _statisticsAdapter.GetBattleLogsForPlayer(uid, fullSession);
    }
    
    /// <summary>
    /// Get all battle logs (for snapshots, etc.)
    /// </summary>
    public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession)
    {
        return _statisticsAdapter.GetBattleLogs(fullSession);
    }
    
    /// <summary>
    /// Get PlayerStatistics directly (for WPF)
    /// </summary>
    public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession)
    {
        return _statisticsAdapter.GetStatistics(fullSession);
    }

    public int GetStatisticsCount(bool fullSession)
    {
        return _statisticsAdapter.GetStatisticsCount(fullSession);
    }

    #endregion
}

// Events partial class remains unchanged
public partial class DataStorageV2
{
    #region Events

    public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
    public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
    public event NewSectionCreatedEventHandler? NewSectionCreated;
    public event BattleLogCreatedEventHandler? BattleLogCreated;
    public event DpsDataUpdatedEventHandler? DpsDataUpdated;
    public event DataUpdatedEventHandler? DataUpdated;
    public event ServerChangedEventHandler? ServerChanged;
    public event Action? BeforeSectionCleared;

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