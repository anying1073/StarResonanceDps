using System.Collections.ObjectModel;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;

namespace StarResonanceDpsAnalysis.Core.Data;

public interface IDataStorage : IDisposable
{
    PlayerInfo CurrentPlayerInfo { get; }

    ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas { get; }

    TimeSpan SectionTimeout { get; set; }

    bool IsServerConnected { get; set; }
    long CurrentPlayerUUID { get; set; }

    event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
    event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
    event NewSectionCreatedEventHandler? NewSectionCreated;
    event BattleLogCreatedEventHandler? BattleLogCreated;
    event DpsDataUpdatedEventHandler? DpsDataUpdated;
    event DataUpdatedEventHandler? DataUpdated;
    event ServerChangedEventHandler? ServerChanged;
    event SectionEndedEventHandler? SectionEnded;

    void LoadPlayerInfoFromFile();
    void SavePlayerInfoToFile();
    Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs);
    void ClearAllDpsData();
    void ClearDpsData();
    void ClearCurrentPlayerInfo();
    void ClearPlayerInfos();
    void ClearAllPlayerInfos();
    void RaiseServerChanged(string currentServerStr, string prevServer);
    void SetPlayerLevel(long playerUid, int tmpLevel);
    bool EnsurePlayer(long playerUid);
    void SetPlayerHP(long playerUid, long hp);
    void SetPlayerMaxHP(long playerUid, long maxHp);
    void SetPlayerName(long playerUid, string playerName);
    void SetPlayerCombatPower(long playerUid, int combatPower);
    void SetPlayerProfessionID(long playerUid, int professionId);

    /// <summary>
    /// 添加战斗日志 (会自动创建日志分段)
    /// Public method for backwards compatibility - fires events immediately
    /// </summary>
    /// <param name="log">战斗日志</param>
    void AddBattleLog(BattleLog log);

    void SetPlayerRankLevel(long playerUid, int readInt32);
    void SetPlayerCritical(long playerUid, int readInt32);
    void SetPlayerLucky(long playerUid, int readInt32);
    void SetPlayerElementFlag(long playerUid, int readInt32);
    void SetPlayerReductionLevel(long playerUid, int readInt32);
    void SetPlayerEnergyFlag(long playerUid, int readInt32);
    void SetNpcTemplateId(long playerUid, int templateId);
    void SetPlayerSeasonLevel(long playerUid, int seasonLevel);
    void SetPlayerSeasonStrength(long playerUid, int seasonStrength);

    /// <summary>
    /// Get battle logs for a specific player
    /// </summary>
    /// <param name="uid">Player UID</param>
    /// <param name="fullSession">true for full session logs, false for current section only</param>
    /// <returns>Battle logs where player is either attacker or target</returns>
    IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession);

    /// <summary>
    /// Get all battle logs
    /// </summary>
    /// <param name="fullSession">true for full session logs, false for current section only</param>
    /// <returns>All battle logs</returns>
    IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession);

    /// <summary>
    /// Get PlayerStatistics directly (for WPF)
    /// </summary>
    IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession);

    /// <summary>
    /// Returns the number of statistics records available for the current session.
    /// </summary>
    /// <param name="fullSession">true to include all statistics from the entire session; false to include only statistics from the current
    /// segment.</param>
    /// <returns>The total number of statistics records. Returns 0 if no statistics are available.</returns>
    int GetStatisticsCount(bool fullSession);

    event Action? BeforeSectionCleared;
}

public delegate void ServerConnectionStateChangedEventHandler(bool serverConnectionState);
public delegate void PlayerInfoUpdatedEventHandler(PlayerInfo info);
public delegate void NewSectionCreatedEventHandler();
public delegate void SectionEndedEventHandler();
public delegate void BattleLogCreatedEventHandler(BattleLog battleLog);
public delegate void DpsDataUpdatedEventHandler();
public delegate void DataUpdatedEventHandler();
public delegate void ServerChangedEventHandler(string currentServer, string prevServer);

public static class DataStorageHelper
{
    /// <summary>
    /// Check if there is any data in the storage
    /// </summary>
    /// <param name="storage">Data storage</param>
    /// <param name="full">Target statistic scope, if null then check both full and section</param>
    /// <returns></returns>
    public static bool HasData(this IDataStorage storage, bool? full = null)
    {
        if (full == null)
        {
            return storage.GetStatisticsCount(true) > 0 || storage.GetStatisticsCount(false) > 0;
        }

        return storage.GetStatisticsCount((bool)full) > 0;
    }

}
