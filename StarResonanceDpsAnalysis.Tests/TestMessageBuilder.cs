using System.Buffers.Binary;
using Google.Protobuf;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using Zproto;

namespace StarResonanceDpsAnalysis.Tests;

internal static class TestMessageBuilder
{
    // This matches WORLD_NTF_SERVICE_ID in MessageAnalyzerV2
    private const ulong WORLD_NTF_SERVICE_ID = 0x0000000063335342UL; // 1664308034 decimal
    private const ulong WORLD_SERVICE_ID = 0x00000000062827566UL; // 103198054 decimal

    public static byte[] BuildNotifyEnvelope(WorldNtfMessageId id, byte[] rpcPayload, bool useWorldServiceId = false)
    {
        var serviceUuid = useWorldServiceId ? WORLD_SERVICE_ID : WORLD_NTF_SERVICE_ID;
        
        // Packet structure:
        // 4 bytes: packet length (includes these 4 bytes)
        // 2 bytes: packet type
        // 8 bytes: service UUID  
        // 4 bytes: stub ID
        // 4 bytes: method ID
        // N bytes: payload
        
        var payloadLength = 8 + 4 + 4 + rpcPayload.Length; // UUID + stubId + methodId + payload
        var packetLength = 4 + 2 + payloadLength;  // Total including length field itself
        var buffer = new byte[packetLength];

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)packetLength);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(4, 2), (ushort)MessageType.Notify);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(6, 8), serviceUuid);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(14, 4), 0); // stubId
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(18, 4), id.ToUInt32());

        rpcPayload.CopyTo(buffer, 22);
        return buffer;
    }

    public static byte[] BuildSyncNearEntitiesPayload(long playerUid, string playerName, int level)
    {
        var attrCollection = new AttrCollection
        {
            Attrs =
            {
                new Attr
                {
                    Id = (int)AttrType.AttrName,
                    RawData = WriteString(playerName)
                },
                new Attr
                {
                    Id = (int)AttrType.AttrLevel,
                    RawData = WriteInt32(level)
                }
            }
        };

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