using System.Collections.ObjectModel;
using System.Xml;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;

namespace StarResonanceDpsAnalysis.Core.Data;

/// <summary>
/// 为了避免重复创建对象而实例化的存储类, 仅用于存储数据
/// 镜像 DataStorage 的公有成员/方法并将调用转发到静态 DataStorage
/// </summary>
public class InstantizedDataStorage : IDataStorage, IDisposable
{
    private readonly object _battleLogCreatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _battleLogCreatedMap = new();

    private readonly object _dataUpdatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _dataUpdatedMap = new();

    private readonly object _dpsDataUpdatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _dpsDataUpdatedMap = new();

    private readonly object _newSectionCreatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _newSectionCreatedMap = new();

    private readonly object _playerInfoUpdatedLock = new();
    private readonly Dictionary<Delegate, Delegate> _playerInfoUpdatedMap = new();

    private readonly object _serverChangedLock = new();
    private readonly Dictionary<Delegate, Delegate> _serverChangedMap = new();

    // Event handler mappings for proper unsubscribe
    private readonly object _serverConnLock = new();
    private readonly Dictionary<Delegate, Delegate> _serverConnMap = new();
    private bool _disposed;

    // Properties (forwarding to DataStorage)
    public PlayerInfo CurrentPlayerInfo => DataStorage.CurrentPlayerInfo;

    public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas => DataStorage.ReadOnlyPlayerInfoDatas;

    public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => DataStorage.ReadOnlyFullDpsDatas;

    public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList => DataStorage.ReadOnlyFullDpsDataList;

    public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas => DataStorage.ReadOnlySectionedDpsDatas;

    public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList => DataStorage.ReadOnlySectionedDpsDataList;

    public TimeSpan SectionTimeout
    {
        get => DataStorage.SectionTimeout;
        set => DataStorage.SectionTimeout = value;
    }

    // DataStorage.IsServerConnected has public getter and internal setter; expose getter only.
    public bool IsServerConnected
    {
        get => DataStorage.IsServerConnected;
        set => DataStorage.IsServerConnected = value;
    }

    public long CurrentPlayerUUID { get; set; }

    // Dispose: detach all wrappers from DataStorage static events
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // ServerConnection
        lock (_serverConnLock)
        {
            foreach (var wrapper in _serverConnMap.Values)
            {
                DataStorage.ServerConnectionStateChanged -=
                    (ServerConnectionStateChangedEventHandler)wrapper!;
            }

            _serverConnMap.Clear();
        }

        // PlayerInfoUpdated
        lock (_playerInfoUpdatedLock)
        {
            foreach (var wrapper in _playerInfoUpdatedMap.Values)
            {
                DataStorage.PlayerInfoUpdated -= (PlayerInfoUpdatedEventHandler)wrapper!;
            }

            _playerInfoUpdatedMap.Clear();
        }

        // NewSectionCreated
        lock (_newSectionCreatedLock)
        {
            foreach (var wrapper in _newSectionCreatedMap.Values)
            {
                DataStorage.NewSectionCreated -= (NewSectionCreatedEventHandler)wrapper!;
            }

            _newSectionCreatedMap.Clear();
        }

        // BattleLogCreated
        lock (_battleLogCreatedLock)
        {
            foreach (var wrapper in _battleLogCreatedMap.Values)
            {
                DataStorage.BattleLogCreated -= (BattleLogCreatedEventHandler)wrapper!;
            }

            _battleLogCreatedMap.Clear();
        }

        // DpsDataUpdated
        lock (_dpsDataUpdatedLock)
        {
            foreach (var wrapper in _dpsDataUpdatedMap.Values)
            {
                DataStorage.DpsDataUpdated -= (DpsDataUpdatedEventHandler)wrapper!;
            }

            _dpsDataUpdatedMap.Clear();
        }

        // DataUpdated
        lock (_dataUpdatedLock)
        {
            foreach (var wrapper in _dataUpdatedMap.Values)
            {
                DataStorage.DataUpdated -= (DataUpdatedEventHandler)wrapper!;
            }

            _dataUpdatedMap.Clear();
        }

        // ServerChanged
        lock (_serverChangedLock)
        {
            foreach (var wrapper in _serverChangedMap.Values)
            {
                DataStorage.ServerChanged -= (ServerChangedEventHandler)wrapper!;
            }

            _serverChangedMap.Clear();
        }

        GC.SuppressFinalize(this);
    }

    // Events: add/remove will subscribe/unsubscribe corresponding static events.
    public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged
    {
        add
        {
            if (value is null) return;
            lock (_serverConnLock)
            {
                if (_serverConnMap.ContainsKey(value)) return;
                ServerConnectionStateChangedEventHandler wrapper = s => value(s);
                _serverConnMap.Add(value, wrapper);
                DataStorage.ServerConnectionStateChanged += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_serverConnLock)
            {
                if (_serverConnMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.ServerConnectionStateChanged -=
                        (ServerConnectionStateChangedEventHandler)wrapper!;
                    _serverConnMap.Remove(value);
                }
            }
        }
    }

    public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated
    {
        add
        {
            if (value is null) return;
            lock (_playerInfoUpdatedLock)
            {
                if (_playerInfoUpdatedMap.ContainsKey(value)) return;
                PlayerInfoUpdatedEventHandler wrapper = p => value(p);
                _playerInfoUpdatedMap.Add(value, wrapper);
                DataStorage.PlayerInfoUpdated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_playerInfoUpdatedLock)
            {
                if (_playerInfoUpdatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.PlayerInfoUpdated -= (PlayerInfoUpdatedEventHandler)wrapper!;
                    _playerInfoUpdatedMap.Remove(value);
                }
            }
        }
    }

    public event NewSectionCreatedEventHandler? NewSectionCreated
    {
        add
        {
            if (value is null) return;
            lock (_newSectionCreatedLock)
            {
                if (_newSectionCreatedMap.ContainsKey(value)) return;
                NewSectionCreatedEventHandler wrapper = () => value();
                _newSectionCreatedMap.Add(value, wrapper);
                DataStorage.NewSectionCreated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_newSectionCreatedLock)
            {
                if (_newSectionCreatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.NewSectionCreated -= (NewSectionCreatedEventHandler)wrapper!;
                    _newSectionCreatedMap.Remove(value);
                }
            }
        }
    }

    public event BattleLogCreatedEventHandler? BattleLogCreated
    {
        add
        {
            if (value is null) return;
            lock (_battleLogCreatedLock)
            {
                if (_battleLogCreatedMap.ContainsKey(value)) return;
                BattleLogCreatedEventHandler wrapper = b => value(b);
                _battleLogCreatedMap.Add(value, wrapper);
                DataStorage.BattleLogCreated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_battleLogCreatedLock)
            {
                if (_battleLogCreatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.BattleLogCreated -= (BattleLogCreatedEventHandler)wrapper!;
                    _battleLogCreatedMap.Remove(value);
                }
            }
        }
    }

    public event DpsDataUpdatedEventHandler? DpsDataUpdated
    {
        add
        {
            if (value is null) return;
            lock (_dpsDataUpdatedLock)
            {
                if (_dpsDataUpdatedMap.ContainsKey(value)) return;
                DpsDataUpdatedEventHandler wrapper = () => value();
                _dpsDataUpdatedMap.Add(value, wrapper);
                DataStorage.DpsDataUpdated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_dpsDataUpdatedLock)
            {
                if (_dpsDataUpdatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.DpsDataUpdated -= (DpsDataUpdatedEventHandler)wrapper!;
                    _dpsDataUpdatedMap.Remove(value);
                }
            }
        }
    }

    public event DataUpdatedEventHandler? DataUpdated
    {
        add
        {
            if (value is null) return;
            lock (_dataUpdatedLock)
            {
                if (_dataUpdatedMap.ContainsKey(value)) return;
                DataUpdatedEventHandler wrapper = () => value();
                _dataUpdatedMap.Add(value, wrapper);
                DataStorage.DataUpdated += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_dataUpdatedLock)
            {
                if (_dataUpdatedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.DataUpdated -= (DataUpdatedEventHandler)wrapper!;
                    _dataUpdatedMap.Remove(value);
                }
            }
        }
    }

    public event ServerChangedEventHandler? ServerChanged
    {
        add
        {
            if (value is null) return;
            lock (_serverChangedLock)
            {
                if (_serverChangedMap.ContainsKey(value)) return;
                ServerChangedEventHandler wrapper = (cur, prev) => value(cur, prev);
                _serverChangedMap.Add(value, wrapper);
                DataStorage.ServerChanged += wrapper;
            }
        }
        remove
        {
            if (value is null) return;
            lock (_serverChangedLock)
            {
                if (_serverChangedMap.TryGetValue(value, out var wrapper))
                {
                    DataStorage.ServerChanged -= (ServerChangedEventHandler)wrapper!;
                    _serverChangedMap.Remove(value);
                }
            }
        }
    }

    // Public methods (forward to DataStorage)
    public void LoadPlayerInfoFromFile()
    {
        DataStorage.LoadPlayerInfoFromFile();
    }

    public void SavePlayerInfoToFile()
    {
        DataStorage.SavePlayerInfoToFile();
    }

    public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs)
    {
        return DataStorage.BuildPlayerDicFromBattleLog(battleLogs);
    }

    public void ClearAllDpsData()
    {
        DataStorage.ClearAllDpsData();
    }

    public void ClearDpsData()
    {
        DataStorage.ClearDpsData();
    }

    public void ClearCurrentPlayerInfo()
    {
        DataStorage.ClearCurrentPlayerInfo();
    }

    public void ClearPlayerInfos()
    {
        DataStorage.ClearPlayerInfos();
    }

    public void ClearAllPlayerInfos()
    {
        DataStorage.ClearAllPlayerInfos();
    }

    public void RaiseServerChanged(string currentServerStr, string prevServer)
    {
        DataStorage.InvokeServerChangedEvent(currentServerStr, prevServer);
    }

    public void SetPlayerLevel(long playerUid, int tmpLevel)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].Level = tmpLevel;
    }

    public bool EnsurePlayer(long playerUid)
    {
        return DataStorage.TestCreatePlayerInfoByUID(playerUid);
    }

    public void SetPlayerHP(long playerUid, long hp)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].HP = hp;
    }

    public void SetPlayerMaxHP(long playerUid, long maxHp)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].MaxHP = maxHp;
    }

    public void SetPlayerName(long playerUid, string playerName)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].Name = playerName;
    }

    public void SetPlayerCombatPower(long playerUid, int combatPower)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].CombatPower = combatPower;
    }

    public void SetPlayerProfessionID(long playerUid, int professionId)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].ProfessionID = professionId;
    }

    public void AddBattleLog(BattleLog log)
    {
        DataStorage.AddBattleLog(log);
    }

    public void SetPlayerRankLevel(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].RankLevel = readInt32;
    }

    public void SetPlayerCritical(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].Critical = readInt32;
    }

    public void SetPlayerLucky(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].Lucky = readInt32;
    }

    public void SetPlayerElementFlag(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].ElementFlag = readInt32;
    }

    public void SetPlayerReductionLevel(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].ReductionLevel = readInt32;
    }

    public void SetPlayerEnergyFlag(long playerUid, int readInt32)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].EnergyFlag = readInt32;
    }

    public void SetNpcTemplateId(long playerUid,int templateId)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].NpcTemplateId = templateId;
    }

    public void SetPlayerSeasonLevel(long playerUid, int seasonLevel)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].SeasonLevel = seasonLevel;
    }

    public void SetPlayerSeasonStrength(long playerUid, int seasonStrength)
    {
        EnsurePlayer(playerUid);
        DataStorage.ReadOnlyPlayerInfoDatas[playerUid].SeasonStrength = seasonStrength;
    }

    public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession)
    {
        throw new NotSupportedException();
    }

    public int GetStatisticsCount(bool fullSession)
    {
        throw new NotSupportedException();
    }
}