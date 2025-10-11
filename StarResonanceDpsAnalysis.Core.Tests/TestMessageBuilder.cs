using System.Buffers.Binary;
using BlueProto;
using Google.Protobuf;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Models;

namespace StarResonanceDpsAnalysis.Core.Tests;

internal static class TestMessageBuilder
{
    private const ulong ServiceUuidCombat = 0x0000000063335342UL;

    public static byte[] BuildNotifyEnvelope(MessageMethod method, byte[] rpcPayload)
    {
        var payloadLength = 8 + 4 + 4 + rpcPayload.Length;
        var innerLength = 4 + 2 + payloadLength;
        var buffer = new byte[4 + innerLength];

        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)innerLength);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(4, 2), (ushort)MessageType.Notify);
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(6, 8), ServiceUuidCombat);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(14, 4), 0); // stubId
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(18, 4), method.ToUInt32());

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

        var sync = new SyncNearEntities();
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