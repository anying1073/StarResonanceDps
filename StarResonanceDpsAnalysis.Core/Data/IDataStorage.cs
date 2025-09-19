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

    event DataStorage.ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
    event DataStorage.PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
    event DataStorage.NewSectionCreatedEventHandler? NewSectionCreated;
    event DataStorage.BattleLogCreatedEventHandler? BattleLogCreated;
    event DataStorage.DpsDataUpdatedEventHandler? DpsDataUpdated;
    event DataStorage.DataUpdatedEventHandler? DataUpdated;
    event DataStorage.ServerChangedEventHandler? ServerChanged;

    void LoadPlayerInfoFromFile();
    void SavePlayerInfoToFile();
    Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs);
    void ClearAllDpsData();
    void ClearDpsData();
    void ClearCurrentPlayerInfo();
    void ClearPlayerInfos();
    void ClearAllPlayerInfos();
}