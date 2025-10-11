using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Core.Tests;

public class DataStorageTests : IDisposable
{
    public DataStorageTests()
    {
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        DataStorage.ClearCurrentPlayerInfo();
    }

    public void Dispose()
    {
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        DataStorage.ClearCurrentPlayerInfo();
    }

    [Fact]
    public void TestCreatePlayerInfoByUID_AddsNewPlayer()
    {
        var uid = 987654321L;

        var existedBefore = DataStorage.TestCreatePlayerInfoByUID(uid);
        var existedAfter = DataStorage.TestCreatePlayerInfoByUID(uid);

        Assert.False(existedBefore);
        Assert.True(existedAfter);
        Assert.True(DataStorage.ReadOnlyPlayerInfoDatas.ContainsKey(uid));
    }

    [Fact]
    public void AddBattleLog_PlayerDamageNpc_UpdatesStaticStore()
    {
        var attackerUid = 6001L;
        var targetUid = 7002L;
        DataStorage.TestCreatePlayerInfoByUID(attackerUid);
        DataStorage.TestCreatePlayerInfoByUID(targetUid);

        var log = new BattleLog
        {
            PacketID = 42,
            TimeTicks = TimeSpan.FromMilliseconds(500).Ticks,
            SkillID = 123,
            AttackerUuid = attackerUid,
            TargetUuid = targetUid,
            Value = 777,
            ValueElementType = 0,
            DamageSourceType = 0,
            IsAttackerPlayer = true,
            IsTargetPlayer = false,
            IsLucky = false,
            IsCritical = false,
            IsHeal = false,
            IsMiss = false,
            IsDead = false
        };

        DataStorage.AddBattleLog(log);

        var attackerData = DataStorage.ReadOnlyFullDpsDatas[attackerUid];
        Assert.Equal(777, attackerData.TotalAttackDamage);
        var npcData = DataStorage.ReadOnlyFullDpsDatas[targetUid];
        Assert.True(npcData.IsNpcData);
        Assert.Equal(777, npcData.TotalTakenDamage);
    }

    [Fact]
    public void AddBattleLog_TriggersEvents()
    {
        var battleLogCreated = false;
        var dpsDataUpdated = false;
        var dataUpdated = false;

        DataStorage.BattleLogCreated += OnDataStorageOnBattleLogCreated;
        DataStorage.DpsDataUpdated += DataStorageOnDpsDataUpdated;
        DataStorage.DataUpdated += OnDataUpdated;

        var log = new BattleLog { TimeTicks = 1, AttackerUuid = 1, Value = 10 };
        DataStorage.AddBattleLog(log);

        Assert.True(battleLogCreated);
        Assert.True(dpsDataUpdated);
        Assert.True(dataUpdated);

        // Cleanup static event handlers
        DataStorage.BattleLogCreated -= OnDataStorageOnBattleLogCreated;
        DataStorage.DpsDataUpdated -= DataStorageOnDpsDataUpdated;
        DataStorage.DataUpdated -= OnDataUpdated;


        return;

        void OnDataUpdated()
        {
            dataUpdated = true;
        }

        void OnDataStorageOnBattleLogCreated(BattleLog _)
        {
            battleLogCreated = true;
        }

        void DataStorageOnDpsDataUpdated()
        {
            dpsDataUpdated = true;
        }
    }

    /*
    [Fact]
    public void AddBattleLog_CreatesNewSection_WhenTimeout()
    {
        // 本测试需要单独运行，不能和其他测试并行
        var newSectionCreated = false;
        DataStorage.NewSectionCreated += OnDataStorageOnNewSectionCreated;

        var log1 = new BattleLog { TimeTicks = TimeSpan.FromSeconds(1).Ticks, AttackerUuid = 1, Value = 10 };
        DataStorage.AddBattleLog(log1);

        Assert.False(newSectionCreated);

        var log2 = new BattleLog { TimeTicks = TimeSpan.FromSeconds(10).Ticks, AttackerUuid = 1, Value = 10 };
        DataStorage.AddBattleLog(log2);

        Assert.True(newSectionCreated);

        DataStorage.NewSectionCreated -= OnDataStorageOnNewSectionCreated;
        return;

        void OnDataStorageOnNewSectionCreated()
        {
            newSectionCreated = true;
        }
    }
    */

    [Fact]
    public void ClearDpsData_ClearsSectionedData()
    {
        DataStorage.AddBattleLog(new BattleLog { AttackerUuid = 1, Value = 100 });
        Assert.NotEmpty(DataStorage.ReadOnlySectionedDpsDatas);

        DataStorage.ClearDpsData();

        Assert.Empty(DataStorage.ReadOnlySectionedDpsDatas);
        Assert.NotEmpty(DataStorage.ReadOnlyFullDpsDatas);
    }

    [Fact]
    public void ClearAllDpsData_ClearsAllDps()
    {
        DataStorage.AddBattleLog(new BattleLog { AttackerUuid = 1, Value = 100 });
        Assert.NotEmpty(DataStorage.ReadOnlyFullDpsDatas);
        Assert.NotEmpty(DataStorage.ReadOnlySectionedDpsDatas);

        DataStorage.ClearAllDpsData();

        Assert.Empty(DataStorage.ReadOnlyFullDpsDatas);
        Assert.Empty(DataStorage.ReadOnlySectionedDpsDatas);
    }

    [Fact]
    public void SetPlayerName_UpdatesName_And_TriggersEvent()
    {
        var uid = 55502692L;
        DataStorage.TestCreatePlayerInfoByUID(uid);

        var eventTriggered = false;
        PlayerInfo? updatedInfo = null;

        PlayerInfoUpdatedEventHandler handler = info =>
        {
            eventTriggered = true;
            updatedInfo = info;
        };
        DataStorage.PlayerInfoUpdated += handler;

        var newName = "Test Player Static";
        DataStorage.SetPlayerName(uid, newName);

        Assert.Equal(newName, DataStorage.ReadOnlyPlayerInfoDatas[uid].Name);
        Assert.True(eventTriggered);
        Assert.NotNull(updatedInfo);
        Assert.Equal(newName, updatedInfo.Name);

        DataStorage.PlayerInfoUpdated -= handler;
    }
}