using System.Collections.ObjectModel;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog.Events;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;
using StarResonanceDpsAnalysis.WPF.Views;
// for Window in ITopmostService

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

#pragma warning disable CS0067
public sealed class DpsStatisticsDesignTimeViewModel : DpsStatisticsViewModel
{
    public DpsStatisticsDesignTimeViewModel() : base(
        NullLogger<DpsStatisticsViewModel>.Instance,
        new DesignDataStorage(),
        new DesignConfigManager(),
        new DesignWindowManagementService(),
        new DesignAppControlService(),
        Dispatcher.CurrentDispatcher,
        new DebugFunctions(NullLogger<DebugFunctions>.Instance,
            new DesignOptionsMonitor(),
            null!,
            LocalizationManager.Instance,
            null!,
            new DesignDataStorage()),
        new DesignBattleHistoryService(),
        LocalizationManager.Instance,
        new MessageDialogService(null!),
        new DesignTimerService(),
        new DesignDataProcessor(),
        new DesignTeamStatsManager(),
        new DataSourceEngine(new DesignDataStorage(), new DesignDataProcessor(), new DesignBattleHistoryService(),
            NullLogger<DataSourceEngine>.Instance, new DesignTimerService()),
        new DesignResetCoordinator())
    {
        // Initialize AppConfig
        AppConfig = new AppConfig { DebugEnabled = true };

        // Populate with a few sample entries so designer shows something.
        try
        {
            for (var i = 0; i < 15; i++)
            {
                CurrentStatisticData.AddTestItem();
            }
        }
        catch
        {
            /* swallow design-time exceptions */
        }
    }

    #region Stub Implementations

    // ⭐ NEW: Design-time combat state manager

    // ⭐ NEW: Design-time team stats manager
    private sealed class DesignTeamStatsManager : ITeamStatsUIManager
    {
        private readonly LocalizationManager _localizationManager = LocalizationManager.Instance;

        public ulong TeamTotalDamage => 1000000;
        public double TeamTotalDps => 50000;

        public string TeamTotalLabel => _localizationManager.GetString(
            ResourcesKeys.DpsStatistics_TeamLabel_Damage,
            defaultValue: "Team DPS");

        public bool ShowTeamTotal { get; set; }

        public event EventHandler<TeamStatsUpdatedEventArgs>? TeamStatsUpdated;

        public void UpdateTeamStats(TeamTotalStats teamStats, StatisticType statisticType, bool hasData)
        {
        }

        public void ResetTeamStats()
        {
        }
    }

    // ⭐ NEW: Design-time timer service
    private sealed class DesignTimerService : IDpsTimerService
    {
        public TimeSpan SectionDuration => TimeSpan.FromMinutes(5);
        public TimeSpan TotalDuration => TimeSpan.FromMinutes(10);
        public bool IsRunning => true;

        public event EventHandler<TimeSpan>? DurationChanged;

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Reset()
        {
        }

        public void StartNewSection()
        {
        }

        public void StopSection()
        {
        }
    }

    // ⭐ NEW: Design-time data processor
    private sealed class DesignDataProcessor : IDpsDataProcessor
    {
        public StatisticDictionary PreProcessData(
            RawDict data,
            bool includeNpcData)
        {
            return [];
        }

        public TeamTotalStats CalculateTeamTotal(IReadOnlyDictionary<long, DpsDataProcessed> data)
        {
            return new TeamTotalStats(0, 0, 0, 0, 0);
        }
    }

    // ⭐ NEW: Design-time reset coordinator
    private sealed class DesignResetCoordinator : IResetCoordinator
    {
        public void ResetCurrentSection()
        {
        }

        public void ResetAll()
        {
        }

        public void Reset(ScopeTime scope)
        {
        }

        public void ResetWithHistory(ScopeTime scope, bool saveHistory, TimeSpan battleDuration, int minimalDuration)
        {
        }
    }

    private sealed class DesignBattleHistoryService : BattleHistoryService
    {
        public DesignBattleHistoryService() : base(
            NullLogger<BattleHistoryService>.Instance,
            new DesignConfigManager(),
            new DesignDataStorage()) // 新增：传入配置管理器
        {
        }
    }

    private sealed class DesignAppControlService : IApplicationControlService
    {
        public void Shutdown()
        {
        }
    }

    private sealed class DesignWindowManagementService : IWindowManagementService
    {
        public AboutView AboutView => throw new NotSupportedException();
        public BossTrackerView BossTrackerView => throw new NotSupportedException();
        public DamageReferenceView DamageReferenceView => throw new NotSupportedException();
        public DpsStatisticsView DpsStatisticsView => throw new NotSupportedException();
        public MainView MainView => throw new NotSupportedException();
        public ModuleSolveView ModuleSolveView => throw new NotSupportedException();
        public PersonalDpsView PersonalDpsView => throw new NotSupportedException();
        public SettingsView SettingsView => throw new NotSupportedException();
        public SkillBreakdownView SkillBreakdownView => throw new NotSupportedException();
        public SkillLogView SkillLogView => throw new NotSupportedException();
    }

    private sealed class DesignDataStorage : IDataStorage
    {
        public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => ReadOnlySectionedDpsDatas;
        public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList { get; } = [];

        public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas { get; } =
            new(new Dictionary<long, DpsData>());

        public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList { get; } = [];
        public bool IsServerConnected => false;
        public int SampleRecordingInterval { get; set; }
        public PlayerInfo CurrentPlayerInfo { get; } = new();

        public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas { get; } =
            new(new Dictionary<long, PlayerInfo>());

        public TimeSpan SectionTimeout { get; set; } = TimeSpan.FromSeconds(5);
        bool IDataStorage.IsServerConnected { get; set; }
        public long CurrentPlayerUUID { get; set; }

        public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
        public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
        public event NewSectionCreatedEventHandler? NewSectionCreated;
        public event BattleLogCreatedEventHandler? BattleLogCreated;
        public event DpsDataUpdatedEventHandler? DpsDataUpdated;
        public event DataUpdatedEventHandler? DataUpdated;
        public event ServerChangedEventHandler? ServerChanged;
        public event SectionEndedEventHandler? SectionEnded;

        public void LoadPlayerInfoFromFile()
        {
        }

        public void SavePlayerInfoToFile()
        {
        }

        public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs)
        {
            return [];
        }

        public void ClearAllDpsData()
        {
        }

        public void ClearDpsData()
        {
        }

        public void ClearCurrentPlayerInfo()
        {
        }

        public void ClearPlayerInfos()
        {
        }

        public void ClearAllPlayerInfos()
        {
        }

        public void RaiseServerChanged(string currentServerStr, string prevServer)
        {
        }

        public void SetPlayerLevel(long playerUid, int tmpLevel)
        {
        }

        public bool EnsurePlayer(long playerUid)
        {
            return true;
        }

        public void SetPlayerHP(long playerUid, long hp)
        {
        }

        public void SetPlayerMaxHP(long playerUid, long maxHp)
        {
        }

        public void SetPlayerCombatState(long uid, bool combatState)
        {
        }

        public void SetPlayerName(long playerUid, string playerName)
        {
        }

        public void SetPlayerCombatPower(long playerUid, int combatPower)
        {
        }

        public void SetPlayerProfessionID(long playerUid, int professionId)
        {
        }

        public void AddBattleLog(BattleLog log)
        {
        }

        public void SetPlayerRankLevel(long playerUid, int readInt32)
        {
        }

        public void SetPlayerCritical(long playerUid, int readInt32)
        {
        }

        public void SetPlayerLucky(long playerUid, int readInt32)
        {
        }

        public void SetPlayerElementFlag(long playerUid, int readInt32)
        {
        }

        public void SetPlayerReductionLevel(long playerUid, int readInt32)
        {
        }

        public void SetPlayerEnergyFlag(long playerUid, int readInt32)
        {
        }

        public void SetNpcTemplateId(long playerUid, int templateId)
        {
        }

        public void SetPlayerSeasonLevel(long playerUid, int seasonLevel)
        {
        }

        public void SetPlayerSeasonStrength(long playerUid, int seasonStrength)
        {
        }

        public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession)
        {
            return [];
        }

        public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession)
        {
            return [];
        }

        public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession)
        {
            return null!;
        }

        public int GetStatisticsCount(bool fullSession)
        {
            return 0;
        }

        public event Action? BeforeSectionCleared;

        public void SetPlayerCombatStateTime(long uid, long time)
        {
        }

        public void Dispose()
        {
        }

        public void RecordSamples(TimeSpan sectionDuration)
        {
        }
    }

    private sealed class DesignConfigManager : IConfigManager
    {
        public event EventHandler<AppConfig>? ConfigurationUpdated;
        public AppConfig CurrentConfig => new() { DebugEnabled = true };

        public Task SaveAsync(AppConfig? config)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DesignLogObservable : IObservable<LogEvent>
    {
        public IDisposable Subscribe(IObserver<LogEvent> observer)
        {
            return new DummyDisp();
        }

        private sealed class DummyDisp : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class DesignOptionsMonitor : IOptionsMonitor<AppConfig>
    {
        public AppConfig CurrentValue { get; } = new() { DebugEnabled = true };

        public AppConfig Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<AppConfig, string?> listener)
        {
            listener(CurrentValue, null);
            return new DummyDisp();
        }

        private sealed class DummyDisp : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    #endregion
}
#pragma warning restore