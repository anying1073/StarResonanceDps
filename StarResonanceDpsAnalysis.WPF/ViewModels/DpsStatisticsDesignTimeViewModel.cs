using System.Collections.ObjectModel;
using System.Windows; // for Window in ITopmostService
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
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Views;

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
        new DebugFunctions(
            Dispatcher.CurrentDispatcher,
            NullLogger<DebugFunctions>.Instance,
            new DesignLogObservable(),
            new DesignOptionsMonitor(),
            null!,
            LocalizationManager.Instance),
        new DesignBattleSnapshotService(),
        LocalizationManager.Instance,
        new MessageDialogService(null!),
        // ⭐ NEW: Add design-time service implementations
        new DesignTimerService(),
        new DesignDataProcessor(),
        new DesignUpdateCoordinator(),
        new DesignCombatStateManager(),
        new DesignTeamStatsManager(),
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
    private sealed class DesignCombatStateManager : ICombatSectionStateManager
    {
        public bool AwaitingSectionStart { get; set; }
        public bool SectionTimedOut { get; set; }
        public TimeSpan LastSectionElapsed { get; set; }
        public TimeSpan SectionStartElapsed { get; set; }
        public TimeSpan TotalCombatDuration { get; set; }
        public bool SkipNextSnapshotSave { get; set; }
        
        public void ResetSectionState() { }
        public void ResetAllState() { }
        public void MarkSectionStarted() { }
        public void MarkSectionEnded(TimeSpan finalDuration) { }
        public void AccumulateSectionDuration() { }
    }
    
    // ⭐ NEW: Design-time team stats manager
    private sealed class DesignTeamStatsManager : ITeamStatsUIManager
    {
        public ulong TeamTotalDamage => 1000000;
        public double TeamTotalDps => 50000;
        public string TeamTotalLabel => "团队DPS";
        public bool ShowTeamTotal { get; set; }
        
        public event EventHandler<TeamStatsUpdatedEventArgs>? TeamStatsUpdated;
        
        public void UpdateTeamStats(TeamTotalStats teamStats, StatisticType statisticType, bool hasData) { }
        public void ResetTeamStats() { }
    }
    
    // ⭐ NEW: Design-time timer service
    private sealed class DesignTimerService : IDpsTimerService
    {
        public TimeSpan BattleDuration => TimeSpan.FromMinutes(5);
        public TimeSpan TotalCombatDuration => TimeSpan.FromMinutes(10);
        public bool IsRunning => true;
        
        public event EventHandler<TimeSpan>? DurationChanged;
        
        public void Start() { }
        public void Stop() { }
        public void Reset() { }
        public void StartNewSection() { }
        public TimeSpan EndSection() => TimeSpan.FromMinutes(5);
        public TimeSpan GetSectionDuration() => TimeSpan.FromMinutes(5);
        public TimeSpan GetSectionElapsed() => TimeSpan.FromMinutes(5);
    }
    
    // ⭐ NEW: Design-time data processor
    private sealed class DesignDataProcessor : IDpsDataProcessor
    {
        public Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> PreProcessData(
            IReadOnlyDictionary<long, PlayerStatistics> data,
            bool includeNpcData)
        {
            return new Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>>
            {
                [StatisticType.Damage] = new(),
                [StatisticType.Healing] = new(),
                [StatisticType.TakenDamage] = new(),
                [StatisticType.NpcTakenDamage] = new()
            };
        }
        
        public TeamTotalStats CalculateTeamTotal(
            IReadOnlyDictionary<long, PlayerStatistics> data,
            StatisticType statisticType)
        {
            return new TeamTotalStats(0, 0, 0, 0, 0);
        }
    }
    
    // ⭐ NEW: Design-time update coordinator
    private sealed class DesignUpdateCoordinator : IDpsUpdateCoordinator
    {
        public Config.DpsUpdateMode UpdateMode => Config.DpsUpdateMode.Passive;
        public int UpdateInterval => 1000;
        public bool IsUpdateEnabled => false;
        
        public event EventHandler? UpdateRequested;
        
        public void Configure(Config.DpsUpdateMode mode, int intervalMs) { }
        public void Start() { }
        public void Stop() { }
        public void Pause() { }
        public void Resume() { }
    }

    // ⭐ NEW: Design-time reset coordinator
    private sealed class DesignResetCoordinator : IResetCoordinator
    {
        public void ResetCurrentSection() { }
        public void ResetAll() { }
        public void Reset(ScopeTime scope) { }
        public void ResetWithSnapshot(ScopeTime scope, bool saveSnapshot, TimeSpan battleDuration, int minimalDuration) { }
    }

    // 修复: 设计时快照服务（添加 IConfigManager 参数）
    private sealed class DesignBattleSnapshotService : BattleSnapshotService
    {
        public DesignBattleSnapshotService() : base(
            NullLogger<BattleSnapshotService>.Instance,
            new DesignConfigManager()) // 新增：传入配置管理器
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
        public PlayerInfo CurrentPlayerInfo { get; } = new();

        public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas { get; } =
            new(new Dictionary<long, PlayerInfo>());

        public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => ReadOnlySectionedDpsDatas;
        public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList { get; } = [];

        public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas { get; } =
            new(new Dictionary<long, DpsData>());

        public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList { get; } = [];
        public TimeSpan SectionTimeout { get; set; } = TimeSpan.FromSeconds(5);
        bool IDataStorage.IsServerConnected { get; set; }
        public long CurrentPlayerUUID { get; set; }
        public bool IsServerConnected => false;

        public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
        public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
        public event NewSectionCreatedEventHandler? NewSectionCreated;
        public event BattleLogCreatedEventHandler? BattleLogCreated;
        public event DpsDataUpdatedEventHandler? DpsDataUpdated;
        public event DataUpdatedEventHandler? DataUpdated;
        public event ServerChangedEventHandler? ServerChanged;
        public event SectionEndedEventHandler? SectionEnded;
        public void LoadPlayerInfoFromFile() { }
        public void SavePlayerInfoToFile() { }
        public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs) => new();
        public void ClearAllDpsData() { }
        public void ClearDpsData() { }
        public void ClearCurrentPlayerInfo() { }
        public void ClearPlayerInfos() { }
        public void ClearAllPlayerInfos() { }
        public void RaiseServerChanged(string currentServerStr, string prevServer) { }
        public void SetPlayerLevel(long playerUid, int tmpLevel) { }
        public bool EnsurePlayer(long playerUid) => true;
        public void SetPlayerHP(long playerUid, long hp) { }
        public void SetPlayerMaxHP(long playerUid, long maxHp) { }
        public void SetPlayerName(long playerUid, string playerName) { }
        public void SetPlayerCombatPower(long playerUid, int combatPower) { }
        public void SetPlayerProfessionID(long playerUid, int professionId) { }
        public void AddBattleLog(BattleLog log) { }
        public void SetPlayerRankLevel(long playerUid, int readInt32) { }
        public void SetPlayerCritical(long playerUid, int readInt32) { }
        public void SetPlayerLucky(long playerUid, int readInt32) { }
        public void SetPlayerElementFlag(long playerUid, int readInt32) { }
        public void SetPlayerReductionLevel(long playerUid, int readInt32) { }
        public void SetPlayerEnergyFlag(long playerUid, int readInt32) { }
        public void SetNpcTemplateId(long playerUid, int templateId) { }
        public void SetPlayerSeasonLevel(long playerUid, int seasonLevel) { }
        public void SetPlayerSeasonStrength(long playerUid, int seasonStrength) { }
        public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession) => Array.Empty<BattleLog>();
        public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession) => Array.Empty<BattleLog>();
        public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession) => null!;
        public int GetStatisticsCount(bool fullSession) => 0;
        public event Action? BeforeSectionCleared;
        public void RecordSamples(TimeSpan sectionDuration) { }
        public void Dispose() { }
    }

    private sealed class DesignConfigManager : IConfigManager
    {
        public event EventHandler<AppConfig>? ConfigurationUpdated;
        public AppConfig CurrentConfig => new() { DebugEnabled = true };
        public Task SaveAsync(AppConfig? config) => Task.CompletedTask;
    }

    private sealed class DesignLogObservable : IObservable<LogEvent>
    {
        public IDisposable Subscribe(IObserver<LogEvent> observer) => new DummyDisp();
        private sealed class DummyDisp : IDisposable { public void Dispose() { } }
    }

    private sealed class DesignOptionsMonitor : IOptionsMonitor<AppConfig>
    {
        public AppConfig CurrentValue { get; } = new() { DebugEnabled = true };
        public AppConfig Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<AppConfig, string?> listener)
        {
            listener(CurrentValue, null);
            return new DummyDisp();
        }
        private sealed class DummyDisp : IDisposable { public void Dispose() { } }
    }

    #endregion
}
#pragma warning restore
