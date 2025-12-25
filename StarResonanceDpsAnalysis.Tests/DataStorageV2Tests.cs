using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Tests;

public class DataStorageV2Tests
{
    [Fact]
    public void ClearAllDpsData_ClearsDataAndRaisesEvents()
    {
        // Arrange
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        // Use AddBattleLog to populate data through StatisticsAdapter
        storage.AddBattleLog(new BattleLog 
        { 
            AttackerUuid = 1, 
            TargetUuid = 2,
            Value = 100, 
            TimeTicks = DateTime.UtcNow.Ticks,
            IsAttackerPlayer = true,
            IsTargetPlayer = false
        });

        bool dpsDataUpdated = false;
        bool dataUpdated = false;
        storage.DpsDataUpdated += () => dpsDataUpdated = true;
        storage.DataUpdated += () => dataUpdated = true;

        // Act
        storage.ClearAllDpsData();

        // Assert
        var full = storage.GetStatistics(true);
        var section = storage.GetStatistics(false);
        Assert.Empty(full);
        Assert.Empty(section);
        Assert.True(dpsDataUpdated);
        Assert.True(dataUpdated);
    }

    [Fact]
    public void ClearDpsData_ClearsSectionedDataAndRaisesEvents()
    {
        // Arrange
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        storage.AddBattleLog(new BattleLog 
        { 
            AttackerUuid = 1,
            TargetUuid = 2,
            Value = 100, 
            TimeTicks = DateTime.UtcNow.Ticks,
            IsAttackerPlayer = true,
            IsTargetPlayer = false
        });

        bool dpsDataUpdated = false;
        bool dataUpdated = false;
        storage.DpsDataUpdated += () => dpsDataUpdated = true;
        storage.DataUpdated += () => dataUpdated = true;

        // Act
        storage.ClearDpsData();

        // Assert
        var full = storage.GetStatistics(true);
        var section = storage.GetStatistics(false);
        Assert.NotEmpty(full); // Full data still exists
        Assert.Empty(section); // Section cleared
        Assert.True(dpsDataUpdated);
        Assert.True(dataUpdated);
    }

    [Fact]
    public void AddBattleLog_Batched_ProcessesAndFiresEventsCorrectly()
    {
        // Arrange
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        var log1 = new BattleLog 
        { 
            AttackerUuid = 1,
            TargetUuid = 2,
            Value = 100, 
            IsAttackerPlayer = true,
            IsTargetPlayer = false,
            TimeTicks = DateTime.UtcNow.Ticks
        };
        var log2 = new BattleLog 
        { 
            AttackerUuid = 1,
            TargetUuid = 2,
            Value = 50, 
            IsAttackerPlayer = true,
            IsTargetPlayer = false,
            TimeTicks = DateTime.UtcNow.Ticks
        };

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
        var full = storage.GetStatistics(true).Values.ToList();
        Assert.Equal(150, full[0].AttackDamage.Total);
    }

    [Fact]
    public void SetPlayerInfo_RaisesUpdateEvents()
    {
        // Arrange
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
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
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
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
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        string? newServer = null;
        string? oldServer = null;
        storage.ServerChanged += (current, prev) =>
        {
            newServer = current;
            oldServer = prev;
        };

        // Act
        storage.RaiseServerChanged("new_server", "old_server");

        // Assert
        Assert.Equal("new_server", newServer);
        Assert.Equal("old_server", oldServer);
    }
}
