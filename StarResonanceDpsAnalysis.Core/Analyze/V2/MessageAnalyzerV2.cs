using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;
using StarResonanceDpsAnalysis.Core.Tools;
using StarResonanceDpsAnalysis.WPF.Data;
using ZstdNet;

#pragma warning disable 0472

namespace StarResonanceDpsAnalysis.Core.Analyze;

/// <summary>
/// Orchestrates message analysis by dispatching packets to registered processors.
/// </summary>
public sealed class MessageAnalyzerV2
{
    private readonly ILogger<MessageAnalyzerV2>? _logger;
    private readonly Dictionary<MessageType, Action<ByteReader, bool>> _messageHandlerMap;
    private readonly MessageHandlerRegistry _registry;

    public MessageAnalyzerV2(IDataStorage storage, ILogger<MessageAnalyzerV2>? logger = null)
    {
        _logger = logger;
        _registry = new MessageHandlerRegistry(storage, logger);
        _messageHandlerMap = new Dictionary<MessageType, Action<ByteReader, bool>>
        {
            { MessageType.Notify, ProcessNotifyMsg },
            { MessageType.FrameDown, ProcessFrameDown }
        };
    }

    private static uint _packetCount = 0;
    /// <summary>
    /// Main entry point for processing a batch of TCP packets.
    /// </summary>
    public void Process(byte[] packets)
    {
        File.WriteAllBytesAsync($"bin\\v2\\packets_{_packetCount}.bin", packets);
        Interlocked.Increment(ref _packetCount);

        if (packets is not { Length: > 0 }) return;

        var packetsReader = new ByteReader(packets);
        while (packetsReader.Remaining > 0)
        {
            if (!packetsReader.TryPeekUInt32BE(out var packetSize)) break;
            if (packetSize < 6) break;
            if (packetSize > packetsReader.Remaining) break;

            var packetReader = new ByteReader(packetsReader.ReadBytes((int)packetSize));
            if (packetReader.ReadUInt32BE() != packetSize) continue;

            var packetType = packetReader.ReadUInt16BE();
            var isZstdCompressed = (packetType & 0x8000) != 0;
            var msgTypeId = (MessageType)(packetType & 0x7FFF);

            if (!_messageHandlerMap.TryGetValue(msgTypeId, out var handler))
            {
                continue;
            }

#if RELEASE
            try
            {
                handler(packetReader, isZstdCompressed);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to handle message type {MessageType}", msgTypeId);
            }
#else
            handler(packetReader, isZstdCompressed);
#endif
        }
    }

    /// <summary>
    /// Processes Notify messages by dispatching them to the appropriate registered processor.
    /// </summary>
    private void ProcessNotifyMsg(ByteReader packet, bool isZstdCompressed)
    {
        var serviceUuid = packet.ReadUInt64BE();
        _ = packet.ReadUInt32BE(); // stubId
        var methodId = packet.ReadUInt32BE();

        if (serviceUuid != 0x0000000063335342UL) return; // Not a combat-related service

        var msgPayload = packet.ReadRemaining();
        if (isZstdCompressed)
        {
            msgPayload = DecompressZstdIfNeeded(msgPayload);
        }

        _logger?.LogTrace("MessageTypeId:{id}", methodId);
        if (_registry.TryGetProcessor(methodId, out var processor))
        {
            processor.Process(msgPayload);
        }
    }

    /// <summary>
    /// Processes FrameDown messages which contain nested packets.
    /// </summary>
    private void ProcessFrameDown(ByteReader reader, bool isZstdCompressed)
    {
        _ = reader.ReadUInt32BE(); // serverSequenceId
        if (reader.Remaining == 0) return;

        var nestedPacket = reader.ReadRemaining();
        if (isZstdCompressed)
        {
            nestedPacket = DecompressZstdIfNeeded(nestedPacket);
        }

        _logger?.LogTrace("ProcessFrameDown");
        Process(nestedPacket); // Recursively process the inner packet
    }

    #region Zstd Decompression

    private static readonly uint ZSTD_MAGIC = 0xFD2FB528;
    private static readonly uint SKIPPABLE_MAGIC_MIN = 0x184D2A50;
    private static readonly uint SKIPPABLE_MAGIC_MAX = 0x184D2A5F;

    private static byte[] DecompressZstdIfNeeded(byte[] buffer)
    {
        if (buffer is not { Length: >= 4 }) return [];

        var off = 0;
        while (off + 4 <= buffer.Length)
        {
            var magic = BitConverter.ToUInt32(buffer, off);
            if (magic == ZSTD_MAGIC) break;
            if (magic >= SKIPPABLE_MAGIC_MIN && magic <= SKIPPABLE_MAGIC_MAX)
            {
                if (off + 8 > buffer.Length) throw new InvalidDataException("Incomplete skippable frame header");
                var size = BitConverter.ToUInt32(buffer, off + 4);
                if (off + 8 + size > buffer.Length) throw new InvalidDataException("Incomplete skippable frame data");
                off += 8 + (int)size;
                continue;
            }

            off++;
        }

        if (off + 4 > buffer.Length) return buffer;

        using var input = new MemoryStream(buffer, off, buffer.Length - off, false);
        using var decoder = new DecompressionStream(input);
        using var output = new MemoryStream();

        const long MAX_OUT = 32L * 1024 * 1024; // 32MB limit
        decoder.CopyTo(output, 8192);
        if (output.Length > MAX_OUT)
        {
            throw new InvalidDataException("Decompressed data exceeds 32MB limit.");
        }

        return output.ToArray();
    }

    #endregion
}