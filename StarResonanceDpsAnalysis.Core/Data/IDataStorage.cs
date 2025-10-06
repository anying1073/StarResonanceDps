using System.Collections.ObjectModel;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.WPF.Data;

public interface IDataStorage : IDisposable
{
    PlayerInfo CurrentPlayerInfo { get; }

    ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas { get; }

    ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas { get; }

    IReadOnlyList<DpsData> ReadOnlyFullDpsDataList { get; }

    ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas { get; }

    IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList { get; }

    TimeSpan SectionTimeout { get; set; }

    bool IsServerConnected { get; }

    event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
    event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
    event NewSectionCreatedEventHandler? NewSectionCreated;
    event BattleLogCreatedEventHandler? BattleLogCreated;
    event DpsDataUpdatedEventHandler? DpsDataUpdated;
    event DataUpdatedEventHandler? DataUpdated;
    event ServerChangedEventHandler? ServerChanged;

    void LoadPlayerInfoFromFile();
    void SavePlayerInfoToFile();
    Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs);
    void ClearAllDpsData();
    void ClearDpsData();
    void ClearCurrentPlayerInfo();
    void ClearPlayerInfos();
    void ClearAllPlayerInfos();
}

public delegate void ServerConnectionStateChangedEventHandler(bool serverConnectionState);
public delegate void PlayerInfoUpdatedEventHandler(PlayerInfo info);
public delegate void NewSectionCreatedEventHandler();
public delegate void BattleLogCreatedEventHandler(BattleLog battleLog);
public delegate void DpsDataUpdatedEventHandler();
public delegate void DataUpdatedEventHandler();
public delegate void ServerChangedEventHandler(string currentServer, string prevServer);
