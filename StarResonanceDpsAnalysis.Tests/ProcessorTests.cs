using Google.Protobuf;
using Moq;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using Zproto;

namespace StarResonanceDpsAnalysis.Tests;

public partial class ProcessorTests
{
    #region SyncContainerData

    [Fact]
    public void Process_WithValidData_ShouldUpdatePlayerInfo()
    {
        // Arrange
        var mockStorage = new Mock<IDataStorage>();
        var playerInfo = new PlayerInfo();
        mockStorage.Setup(s => s.CurrentPlayerInfo).Returns(playerInfo);

        var processor = new SyncContainerDataProcessor(mockStorage.Object, null);

        const long playerUid = 12345L;
        const int level = 60;
        const int curHp = 10000;
        const int maxHp = 15000;
        const string playerName = "TestPlayer";
        const int fightPoint = 50000;
        const int profId = 101;

        var syncContainerData = new Zproto.WorldNtf.Types.SyncContainerData
        {
            VData = new Zproto.CharSerialize
            {
                CharId = playerUid,
                RoleLevel = new Zproto.RoleLevel { Level = level },
                Attr = new Zproto.UserFightAttr { CurHp = curHp, MaxHp = maxHp },
                CharBase = new Zproto.CharBaseInfo { Name = playerName, FightPoint = fightPoint },
                ProfessionList = new Zproto.ProfessionList { CurProfessionId = profId }
            }
        };
        var payload = syncContainerData.ToByteArray();

        // Act
        processor.Process(payload);

        // Assert
        mockStorage.Verify(s => s.EnsurePlayer(playerUid), Times.Once);
        Assert.Equal(playerUid, playerInfo.UID);

        mockStorage.Verify(s => s.SetPlayerLevel(playerUid, level), Times.Once);
        Assert.Equal(level, playerInfo.Level);

        mockStorage.Verify(s => s.SetPlayerHP(playerUid, curHp), Times.Once);
        Assert.Equal(curHp, playerInfo.HP);

        mockStorage.Verify(s => s.SetPlayerMaxHP(playerUid, maxHp), Times.Once);
        Assert.Equal(maxHp, playerInfo.MaxHP);

        mockStorage.Verify(s => s.SetPlayerName(playerUid, playerName), Times.Once);
        Assert.Equal(playerName, playerInfo.Name);

        mockStorage.Verify(s => s.SetPlayerCombatPower(playerUid, fightPoint), Times.Once);
        Assert.Equal(fightPoint, playerInfo.CombatPower);

        mockStorage.Verify(s => s.SetPlayerProfessionID(playerUid, profId), Times.Once);
        Assert.Equal(profId, playerInfo.ProfessionID);
    }

    #endregion

    #region SyncContainerDirtyDataProcessor

    [Fact]
    public void SyncContainerDirtyDataProcessor_Process_ShouldUpdatePlayerName()
    {
        // Arrange
        var mockStorage = new Mock<IDataStorage>();
        const long playerUuidRaw = 12345L << 16;
        const long playerUuid = 12345L;
        const string playerName = "DirtyPlayer";

        mockStorage.Setup(s => s.CurrentPlayerUUID).Returns(playerUuid);
        var playerInfo = new PlayerInfo();
        mockStorage.Setup(s => s.CurrentPlayerInfo).Returns(playerInfo);

        var processor = new SyncContainerDirtyDataProcessor(mockStorage.Object, null);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteIdentifier(bw);
        bw.Write(2u); // fieldIndex for ProcessNameAndCombatPower
        bw.Write(0);
        WriteIdentifier(bw);
        bw.Write(5u); // fieldIndex for player name
        bw.Write(0);
        WriteString(bw, playerName);
        var buffer = ms.ToArray();

        var dirtyData = new WorldNtf.Types.SyncContainerDirtyData
        {
            VData = new BufferStream()
            {
                Buffer = ByteString.CopyFrom(buffer)
            }
        };
        var payload = dirtyData.ToByteArray();

        // Act
        processor.Process(payload);

        // Assert
        mockStorage.Verify(s => s.EnsurePlayer(playerUuid), Times.Once);
        mockStorage.Verify(s => s.SetPlayerName(playerUuid, playerName), Times.Once);
        Assert.Equal(playerName, playerInfo.Name);
    }

    [Fact]
    public void SyncContainerDirtyDataProcessor_Process_ShouldUpdatePlayerHp()
    {
        // Arrange
        var mockStorage = new Mock<IDataStorage>();
        const long playerUuidRaw = 12345L << 16;
        const long playerUuid = 12345L;
        const int curHp = 5000;
        // const int maxHp = 8000;

        mockStorage.Setup(s => s.CurrentPlayerUUID).Returns(playerUuid);
        var playerInfo = new PlayerInfo();
        mockStorage.Setup(s => s.CurrentPlayerInfo).Returns(playerInfo);

        var processor = new SyncContainerDirtyDataProcessor(mockStorage.Object, null);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteIdentifier(bw);
        bw.Write(16u); // fieldIndex for ProcessHp
        bw.Write(0);
        WriteIdentifier(bw);
        bw.Write(1u); // fieldIndex for current HP
        bw.Write(0);
        bw.Write((uint)curHp);
        WriteIdentifier(bw);
        var buffer = ms.ToArray();

        var dirtyData = new WorldNtf.Types.SyncContainerDirtyData
        {
            VData = new BufferStream()
            {
                Buffer = ByteString.CopyFrom(buffer)
            }
        };
        var payload = dirtyData.ToByteArray();

        // Act
        processor.Process(payload);

        // Assert
        mockStorage.Verify(s => s.EnsurePlayer(playerUuid), Times.Once);
        mockStorage.Verify(s => s.SetPlayerHP(playerUuid, curHp), Times.Once);
        Assert.Equal(curHp, playerInfo.HP);
    }

    private static void WriteIdentifier(BinaryWriter bw)
    {
        bw.Write(0xFFFFFFFE);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);
    }

    private static void WriteString(BinaryWriter bw, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        bw.Write((uint)bytes.Length);
        bw.Write(0);
        bw.Write(bytes);
        bw.Write(0);
    }

    #endregion

    #region SyncNearEntitiesProcessor

    [Fact]
    public void SyncNearEntitiesProcessor_Process_ShouldUpdatePlayerAttributes()
    {
        // Arrange
        var mockStorage = new Mock<IDataStorage>();
        var processor = new SyncNearEntitiesProcessor(mockStorage.Object, null);

        const long playerUid = 67890L;
        const string playerName = "NearPlayer";
        const int level = 55;

        var syncNearEntities = new WorldNtf.Types.SyncNearEntities();
        var entity = new Entity
        {
            Uuid = playerUid << 16,
            EntType = EEntityType.EntChar,
            Attrs = new AttrCollection()
        };
        entity.Attrs.Attrs.Add(new Attr { Id = (int)AttrType.AttrName, RawData = ByteString.CopyFrom(EncodeString(playerName)) });
        entity.Attrs.Attrs.Add(new Attr { Id = (int)AttrType.AttrLevel, RawData = ByteString.CopyFrom(EncodeInt32(level)) });
        syncNearEntities.Appear.Add(entity);

        var payload = syncNearEntities.ToByteArray();

        // Act
        processor.Process(payload);

        // Assert
        mockStorage.Verify(s => s.EnsurePlayer(playerUid), Times.Once);
        mockStorage.Verify(s => s.SetPlayerName(playerUid, playerName), Times.Once);
        mockStorage.Verify(s => s.SetPlayerLevel(playerUid, level), Times.Once);
    }

    private static byte[] EncodeString(string value)
    {
        using var ms = new MemoryStream();
        using var cos = new CodedOutputStream(ms);
        cos.WriteString(value);
        cos.Flush();
        return ms.ToArray();
    }

    private static byte[] EncodeInt32(int value)
    {
        using var ms = new MemoryStream();
        using var cos = new CodedOutputStream(ms);
        cos.WriteInt32(value);
        cos.Flush();
        return ms.ToArray();
    }

    #endregion

    #region DeltaInfoProcessors

    /*
    [Fact]
    public void SyncToMeDeltaInfoProcessor_Process_ShouldUpdateCurrentPlayerAndAddBattleLog()
    {
        // TODO: error thrown, fix
        // Arrange
        var mockStorage = new Mock<IDataStorage>();
        var processor = new SyncToMeDeltaInfoProcessor(mockStorage.Object, null);

        const long currentPlayerUuidRaw = 99999L << 16;
        const long attackerUuid = 0x110640L;
        const long targetUuid = 22222L;
        const int skillId = 123;
        const long damage = 500;

        var syncToMeDeltaInfo = new SyncToMeDeltaInfo
        {
            DeltaInfo = new AoiSyncToMeDelta
            {
                Uuid = currentPlayerUuidRaw,
                BaseDelta = new AoiSyncDelta
                {
                    Uuid = targetUuid << 16,
                    SkillEffects = new SkillEffect()
                }
            }
        };
        syncToMeDeltaInfo.DeltaInfo.BaseDelta.SkillEffects.Damages.Add(new SyncDamageInfo
        {
            OwnerId = skillId,
            AttackerUuid = attackerUuid << 16,
            Value = damage,
            Property = EDamageProperty.Fire,
            Type = EDamageType.Normal
        });

        var payload = syncToMeDeltaInfo.ToByteArray();

        // Act
        processor.Process(payload);

        // Assert
        mockStorage.VerifySet(s => s.CurrentPlayerUUID = currentPlayerUuidRaw, Times.Once);
        mockStorage.Verify(s => s.AddBattleLog(It.Is<BattleLog>(b =>
            b.AttackerUuid == attackerUuid &&
            b.TargetUuid == targetUuid &&
            b.SkillID == skillId &&
            b.Value == damage &&
            b.IsAttackerPlayer &&
            b.IsTargetPlayer
        )), Times.Once);
    }
    */

    [Fact]
    public void SyncNearDeltaInfoProcessor_Process_ShouldAddBattleLogsForMultipleDeltas()
    {
        // Arrange
        var mockStorage = new Mock<IDataStorage>();
        var processor = new SyncNearDeltaInfoProcessor(mockStorage.Object, null);

        const long attacker1 = 333L;
        const long target1 = 444L;
        const long attacker2 = 555L;
        const long target2 = 666L;

        var syncNearDeltaInfo = new WorldNtf.Types.SyncNearDeltaInfo();
        syncNearDeltaInfo.DeltaInfos.Add(new WorldNtf.Types.AoiSyncDelta
        {
            Uuid = target1 << 16,
            SkillEffects = new SkillEffect { Damages = { new SyncDamageInfo { AttackerUuid = attacker1 << 16, Value = 100, OwnerId = 1 } } }
        });
        syncNearDeltaInfo.DeltaInfos.Add(new WorldNtf.Types.AoiSyncDelta
        {
            Uuid = target2 << 16,
            SkillEffects = new SkillEffect { Damages = { new SyncDamageInfo { AttackerUuid = attacker2 << 16, Value = 200, OwnerId = 2, Type = EDamageType.Heal } } }
        });

        var payload = syncNearDeltaInfo.ToByteArray();

        // Act
        processor.Process(payload);

        // Assert
        mockStorage.Verify(s => s.AddBattleLog(It.Is<BattleLog>(b => b.AttackerUuid == attacker1 && b.Value == 100 && !b.IsHeal)), Times.Once);
        mockStorage.Verify(s => s.AddBattleLog(It.Is<BattleLog>(b => b.AttackerUuid == attacker2 && b.Value == 200 && b.IsHeal)), Times.Once);
    }

    #endregion
}