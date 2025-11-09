using System.Collections.ObjectModel;
using System.Windows; // for Window in ITopmostService
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog.Events;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public sealed class DpsStatisticsDesignTimeViewModel : DpsStatisticsViewModel
{
    public DpsStatisticsDesignTimeViewModel() : base(
        new DesignAppControlService(),
        new DesignDataStorage(),
        NullLogger<DpsStatisticsViewModel>.Instance,
        new DesignConfigManager(),
        new DesignWindowManagementService(),
        new DesignTopmostService(),
        new DebugFunctions(
            Dispatcher.CurrentDispatcher,
            NullLogger<DebugFunctions>.Instance,
            new DesignLogObservable(),
            new DesignOptionsMonitor(),
            null!,
            LocalizationManager.Instance),
        Dispatcher.CurrentDispatcher)
    {
        // Populate with a few sample entries so designer shows something.
        try
        {
            for (var i = 0; i < 3; i++)
            {
                AddTestItem();
            }
        }
        catch
        {
            /* swallow design-time exceptions */
        }
    }

    #region Stub Implementations

    private sealed class DesignTopmostService : ITopmostService
    {
        public void SetTopmost(Window window, bool enable)
        {
            // no-op at design time
        }

        public bool ToggleTopmost(Window window)
        {
            // Return current state or false at design time
            return window.Topmost = !window.Topmost;
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

#pragma warning disable CS0067
        public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
        public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
        public event NewSectionCreatedEventHandler? NewSectionCreated;
        public event BattleLogCreatedEventHandler? BattleLogCreated;
        public event DpsDataUpdatedEventHandler? DpsDataUpdated;
        public event DataUpdatedEventHandler? DataUpdated;
        public event ServerChangedEventHandler? ServerChanged;
#pragma warning restore

        public void LoadPlayerInfoFromFile()
        {
        }

        public void SavePlayerInfoToFile()
        {
        }

        public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs)
        {
            return new Dictionary<long, PlayerInfoFileData>();
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

        public void Dispose()
        {
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


