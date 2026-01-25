using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Google.Protobuf;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Zproto;

BenchmarkSwitcher.FromAssembly(typeof(BenchmarkMarker).Assembly).Run(args);

public static class BenchmarkMarker { }

[MemoryDiagnoser]
public class DataStorageBenchmarks
{
    private readonly List<BattleLog> _logs = new();
    private DataStorageV2 _storageV2 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _storageV2 = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        PrepareLogs();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        DataStorage.ClearCurrentPlayerInfo();

        _storageV2.ClearAllDpsData();
        _storageV2.ClearAllPlayerInfos();
    }

    [Benchmark]
    public void StaticDataStorage_AddBattleLog()
    {
        foreach (var log in _logs)
        {
            DataStorage.AddBattleLog(log);
        }
    }

    [Benchmark]
    public void RealtimeDataStorage_AddBattleLog()
    {
        foreach (var log in _logs)
        {
            _storageV2.AddBattleLog(log);
        }
    }

    private void PrepareLogs()
    {
        const long attackerUid = 123L;
        const long targetUid = 456L;
        DataStorage.TestCreatePlayerInfoByUID(attackerUid);
        DataStorage.TestCreatePlayerInfoByUID(targetUid);
        _storageV2.EnsurePlayer(attackerUid);
        _storageV2.EnsurePlayer(targetUid);

        var baseTicks = TimeSpan.FromSeconds(1).Ticks;
        for (var i = 0; i < 64; i++)
        {
            var log = new BattleLog
            {
                PacketID = i + 1,
                TimeTicks = baseTicks + i * TimeSpan.FromMilliseconds(100).Ticks,
                SkillID = 1000 + i,
                AttackerUuid = attackerUid,
                TargetUuid = targetUid,
                Value = 100 + i,
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

            _logs.Add(log);
        }
    }
}

[MemoryDiagnoser]
public class MessageParsingBenchmarks
{
    private DataStorageV2 _storageV2 = null!;
    private MessageAnalyzerV2 _analyzerV2 = null!;
    private byte[] _notifyEnvelope = Array.Empty<byte>();
    private readonly long _playerUid = 778899L;

    [GlobalSetup]
    public void Setup()
    {
        _storageV2 = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        _analyzerV2 = new MessageAnalyzerV2(_storageV2);
        _notifyEnvelope = BuildNotifyEnvelope(0x00000006U, BuildSyncNearEntitiesPayload(_playerUid, "Benchmark Hero", 55));
    }

    [IterationSetup]
    public void IterationSetup()
    {
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        DataStorage.ClearCurrentPlayerInfo();

        _storageV2.ClearAllDpsData();
        _storageV2.ClearAllPlayerInfos();
    }

    [Benchmark]
    public void StaticMessageAnalyzer_Process()
    {
        MessageAnalyzer.Process(_notifyEnvelope);
    }

    [Benchmark]
    public void RealtimeMessageAnalyzer_Process()
    {
        _analyzerV2.Process(_notifyEnvelope);
    }

    private static byte[] BuildSyncNearEntitiesPayload(long playerUid, string playerName, int level)
    {
        var attrCollection = new AttrCollection();
        attrCollection.Attrs.Add(new Attr
        {
            Id = (int)AttrType.AttrName,
            RawData = WriteString(playerName)
        });
        attrCollection.Attrs.Add(new Attr
        {
            Id = (int)AttrType.AttrLevel,
            RawData = WriteInt32(level)
        });

        var entity = new Entity
        {
            Uuid = playerUid << 16,
            EntType = EEntityType.EntChar,
            Attrs = attrCollection
        };

        var sync = new WorldNtf.Types.SyncNearEntities();
        sync.Appear.Add(entity);
        return sync.ToByteArray();
    }

    private static byte[] BuildNotifyEnvelope(uint methodId, byte[] rpcPayload)
    {
        const ulong serviceUuid = 0x0000000063335342UL;
        var payloadLength = 8 + 4 + 4 + rpcPayload.Length;
        var innerLength = 4 + 2 + payloadLength;
        var buffer = new byte[4 + innerLength];

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)innerLength);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4, 4), (uint)innerLength);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(8, 2), (ushort)MessageType.Notify);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(10, 8), serviceUuid);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(18, 4), 0);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(22, 4), methodId);
        rpcPayload.CopyTo(buffer, 26);
        return buffer;
    }

    private static ByteString WriteString(string value)
    {
        using var ms = new MemoryStream();
        var writer = new CodedOutputStream(ms);
        writer.WriteString(value);
        writer.Flush();
        return ByteString.CopyFrom(ms.ToArray());
    }

    private static ByteString WriteInt32(int value)
    {
        using var ms = new MemoryStream();
        var writer = new CodedOutputStream(ms);
        writer.WriteInt32(value);
        writer.Flush();
        return ByteString.CopyFrom(ms.ToArray());
    }
}
