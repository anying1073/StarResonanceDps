using BenchmarkDotNet.Attributes;
using PacketDotNet;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Zproto;

namespace StarResonanceDpsAnalysis.Core.Benchmarks;

[MemoryDiagnoser]
public class PacketAnalyzerBenchmarks
{
    private PacketAnalyzer _v1 = null!;
    private PacketAnalyzerV2 _v2 = null!;
    private IDataStorage _storage = null!;
    private MessageAnalyzerV2 _msgV2 = null!;

    private RawCapture[] _traffic = Array.Empty<RawCapture>();

    // Synthetic flow parameters
    private readonly IPAddress _srcIp = IPAddress.Parse("58.217.182.174");
    private readonly IPAddress _dstIp = IPAddress.Parse("192.168.1.10");
    private const ushort SrcPort = 23000;
    private const ushort DstPort = 52000;

    [Params(256, 1024)]
    public int Messages; // how many application messages to send after handshake

    [GlobalSetup]
    public void Setup()
    {
        _v1 = new PacketAnalyzer();
        _storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        _msgV2 = new MessageAnalyzerV2(_storage);
        _v2 = new PacketAnalyzerV2(_storage, _msgV2);

        // Build synthetic TCP stream: one handshake packet to trigger server detection, then N data packets
        var appMsg = BuildNotifyEnvelope(0x00000006U, BuildSyncNearEntitiesPayload(123456L));

        var packets = new List<RawCapture>();
        uint seq = 1000;

        // 1) Handshake packet so analyzers lock onto server
        var handshake = BuildHandshakePayload();
        packets.Add(BuildRaw(seq, handshake));
        seq += (uint)handshake.Length;

        // 2) Data packets: each carries exactly one framed application message
        for (int i = 0; i < Messages; i++)
        {
            packets.Add(BuildRaw(seq, appMsg));
            seq += (uint)appMsg.Length;
        }

        _traffic = packets.ToArray();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // reset state between iterations
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        DataStorage.ClearCurrentPlayerInfo();

        _storage.ClearAllDpsData();
        _storage.ClearAllPlayerInfos();

        _v2.ResetCaptureState();
    }

    [Benchmark]
    public void V1_PacketAnalyzer_ProcessSyntheticStream()
    {
        foreach (var raw in _traffic)
        {
            _v1.StartNewAnalyzer(device: null!, raw);
        }
    }

    [Benchmark]
    public async Task V2_PacketAnalyzer_ProcessSyntheticStream()
    {
        _v2.Start();
        try
        {
            foreach (var raw in _traffic)
            {
                if (!_v2.TryEnlistData(raw))
                {
                    await _v2.EnlistDataAsync(raw);
                }
            }
        }
        finally
        {
            _v2.Stop();
        }
    }

    [Benchmark(Description = "V1 inline (synchronous) processing")]
    public void V1_PacketAnalyzer_ProcessSyntheticStream_Inline()
    {
        foreach (var raw in _traffic)
        {
            _v1.ProcessInline(raw);
        }
    }

    [Benchmark(Description = "V2 inline (synchronous) processing")]
    public void V2_PacketAnalyzer_ProcessSyntheticStream_Inline()
    {
        foreach (var raw in _traffic)
        {
            _v2.ProcessInline(raw);
        }
    }

    private RawCapture BuildRaw(uint seq, byte[] payload)
    {
        // Build TCP packet with given sequence/payload wrapped in IPv4/Ethernet
        var tcp = new TcpPacket(SrcPort, DstPort)
        {
            SequenceNumber = seq,
            AcknowledgmentNumber = 0,
        };
        tcp.PayloadData = payload;
        tcp.UpdateCalculatedValues();

        var ip = new IPv4Packet(_srcIp, _dstIp)
        {
            Protocol = PacketDotNet.ProtocolType.Tcp,
            TimeToLive = 64
        };
        ip.PayloadPacket = tcp;
        ip.UpdateCalculatedValues();

        var srcMac = new PhysicalAddress(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 });
        var dstMac = new PhysicalAddress(new byte[] { 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB });
        var eth = new EthernetPacket(srcMac, dstMac, EthernetType.IPv4)
        {
            PayloadPacket = ip
        };
        eth.UpdateCalculatedValues();

        var bytes = eth.Bytes;
        var timeval = new PosixTimeval(DateTime.Now);
        return new RawCapture(LinkLayers.Ethernet, timeval, bytes);
    }

    private static byte[] BuildHandshakePayload()
    {
        // Satisfy v1/v2 detection: payload[4] == 0 and at offset 10 there is a framed blob whose data
        // has server signature at offset 5: 00 63 33 53 42 00
        var serverSignature = new byte[] { 0x00, 0x63, 0x33, 0x53, 0x42, 0x00 };

        var tmp = new byte[20];
        // place signature at offset 5 in tmp
        Array.Copy(serverSignature, 0, tmp, 5, serverSignature.Length);

        var len = 4 + tmp.Length; // length includes the 4-byte length itself
        var payloadLen = 10 + 4 + tmp.Length;
        var payload = new byte[payloadLen];
        // payload[4] = 0
        payload[4] = 0;
        // write length at offset 10
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(10, 4), len);
        // copy tmp after the length
        Array.Copy(tmp, 0, payload, 14, tmp.Length);
        return payload;
    }

    private static byte[] BuildSyncNearEntitiesPayload(long playerUid)
    {
        var attrCollection = new AttrCollection();
        // Keep empty to avoid dependency on AttrType enum here

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
        var writer = new Google.Protobuf.CodedOutputStream(ms);
        writer.WriteString(value);
        writer.Flush();
        return ByteString.CopyFrom(ms.ToArray());
    }

    private static ByteString WriteInt32(int value)
    {
        using var ms = new MemoryStream();
        var writer = new Google.Protobuf.CodedOutputStream(ms);
        writer.WriteInt32(value);
        writer.Flush();
        return ByteString.CopyFrom(ms.ToArray());
    }
}
