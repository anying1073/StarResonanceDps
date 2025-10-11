using System;
using System.Linq;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using Xunit;

namespace StarResonanceDpsAnalysis.Core.Tests;

public class DataStorageV2Tests
{
    [Fact]
    public void ClearAllDpsData_ClearsDataAndRaisesEvents()
    {
        // Arrange
        var storage = new DataStorageV2();
        storage.GetOrCreateDpsDataByUid(1);
        storage.AddBattleLog(new BattleLog { AttackerUuid = 1, Value = 100 });

        bool dpsDataUpdated = false;
        bool dataUpdated = false;
        storage.DpsDataUpdated += () => dpsDataUpdated = true;
        storage.DataUpdated += () => dataUpdated = true;

        // Act
        storage.ClearAllDpsData();

        // Assert
        Assert.Empty(storage.ReadOnlyFullDpsDatas);
        Assert.Empty(storage.ReadOnlySectionedDpsDatas);
        Assert.True(dpsDataUpdated);
        Assert.True(dataUpdated);
    }

    [Fact]
    public void ClearDpsData_ClearsSectionedDataAndRaisesEvents()
    {
        // Arrange
        var storage = new DataStorageV2();
        storage.GetOrCreateDpsDataByUid(1);
        storage.AddBattleLog(new BattleLog { AttackerUuid = 1, Value = 100, TimeTicks = DateTime.Now.Ticks });

        bool dpsDataUpdated = false;
        bool dataUpdated = false;
        storage.DpsDataUpdated += () => dpsDataUpdated = true;
        storage.DataUpdated += () => dataUpdated = true;

        // Act
        storage.ClearDpsData();

        // Assert
        Assert.NotEmpty(storage.ReadOnlyFullDpsDatas);
        Assert.Empty(storage.ReadOnlySectionedDpsDatas);
        Assert.True(dpsDataUpdated);
        Assert.True(dataUpdated);
    }

    /*
    [Fact]
    public void AddBattleLog_CreatesNewSectionOnTimeout()
    {
        // Arrange
        var storage = new DataStorageV2 { SectionTimeout = TimeSpan.FromMilliseconds(10) };
        var log1 = new BattleLog { AttackerUuid = 1, Value = 100, TimeTicks = DateTime.UtcNow.Ticks };
        storage.AddBattleLog(log1);

        bool newSectionCreated = false;
        storage.NewSectionCreated += () => newSectionCreated = true;

        // Act
        System.Threading.Thread.Sleep(20);
        var log2 = new BattleLog { AttackerUuid = 1, Value = 200, TimeTicks = DateTime.UtcNow.Ticks };
        storage.AddBattleLog(log2);

        // Assert
        Assert.True(newSectionCreated);
        Assert.Equal(200, storage.ReadOnlySectionedDpsDatas[1].TotalAttackDamage);
        Assert.Equal(300, storage.ReadOnlyFullDpsDatas[1].TotalAttackDamage);
    }
    */

    [Fact]
    public void AddBattleLog_Batched_ProcessesAndFiresEventsCorrectly()
    {
        // Arrange
        var storage = new DataStorageV2();
        var log1 = new BattleLog { AttackerUuid = 1, Value = 100, IsAttackerPlayer = true };
        var log2 = new BattleLog { AttackerUuid = 1, Value = 50, IsAttackerPlayer = true };

        int battleLogCreatedCount = 0;
        bool dpsDataUpdated = false;
        bool dataUpdated = false;

        storage.BattleLogCreated += (log) => battleLogCreatedCount++;
        storage.DpsDataUpdated += () => dpsDataUpdated = true;
        storage.DataUpdated += () => dataUpdated = true;

        // Act
        storage.AddBattleLogInternal(log1);
        storage.AddBattleLogInternal(log2);
        storage.FlushPendingEvents();

        // Assert
        Assert.Equal(2, battleLogCreatedCount);
        Assert.True(dpsDataUpdated);
        Assert.True(dataUpdated);
        Assert.Equal(150, storage.ReadOnlyFullDpsDatas[1].TotalAttackDamage);
    }

    [Fact]
    public void SetPlayerInfo_RaisesUpdateEvents()
    {
        // Arrange
        var storage = new DataStorageV2();
        storage.EnsurePlayer(1);

        PlayerInfo? updatedInfo = null;
        bool dataUpdated = false;
        storage.PlayerInfoUpdated += (info) => updatedInfo = info;
        storage.DataUpdated += () => dataUpdated = true;

        // Act
        storage.SetPlayerName(1, "TestPlayer");

        // Assert
        Assert.NotNull(updatedInfo);
        Assert.Equal(1, updatedInfo.UID);
        Assert.Equal("TestPlayer", updatedInfo.Name);
        Assert.True(dataUpdated);
    }

    [Fact]
    public void EnsurePlayer_CreatesNewPlayerAndRaisesEvent()
    {
        // Arrange
        var storage = new DataStorageV2();
        PlayerInfo? updatedInfo = null;
        storage.PlayerInfoUpdated += (info) => updatedInfo = info;

        // Act
        bool exists = storage.EnsurePlayer(123);

        // Assert
        Assert.False(exists);
        Assert.True(storage.ReadOnlyPlayerInfoDatas.ContainsKey(123));
        Assert.NotNull(updatedInfo);
        Assert.Equal(123, updatedInfo.UID);
    }

    [Fact]
    public void NotifyServerChanged_RaisesEvent()
    {
        // Arrange
        var storage = new DataStorageV2();
        string? newServer = null;
        string? oldServer = null;
        storage.ServerChanged += (current, prev) =>
        {
            newServer = current;
            oldServer = prev;
        };

        // Act
        storage.NotifyServerChanged("new_server", "old_server");

        // Assert
        Assert.Equal("new_server", newServer);
        Assert.Equal("old_server", oldServer);
    }
}
