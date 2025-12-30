using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Collections;
using System.IO.Pipelines;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Logging;

namespace StarResonanceDpsAnalysis.Core.Analyze;

/// <summary>
/// Handles the stateful processing of a single TCP stream, including server detection, packet reassembly, and message parsing.
/// This class is not thread-safe and is intended to be used by a single consumer thread.
/// </summary>
internal sealed class TcpStreamProcessor : IDisposable
{
    private readonly IDataStorage _storage;
    private readonly MessageAnalyzerV2 _messageAnalyzer;
    private readonly ILogger? _logger;

    private readonly TimeSpan _gapTimeout = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(10);

    private readonly byte[] _loginReturnSignature =
    [
        0x00, 0x00, 0x00, 0x62, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,
        0x00, 0x11, 0x45, 0x14, 0x00, 0x00, 0x00, 0x00, 0x0a, 0x4e, 0x08, 0x01, 0x22, 0x24
    ];
    private readonly byte[] _serverSignature = [0x00, 0x63, 0x33, 0x53, 0x42, 0x00];

    // State
    private DateTime _lastAnyPacketAt = DateTime.MinValue;
    private DateTime? _waitingGapSince;
    private uint? _tcpNextSeq;
    private readonly BoundedConcurrentCache<uint, byte[]> _tcpCache = new(1000, TimeSpan.FromSeconds(30));
    private DateTime _tcpLastTime = DateTime.MinValue;

    // Pipe replaces the previous MemoryStream staging buffer
    private Pipe _pipe;
    
    // Reentrancy guard for ParseFromPipe
    private bool _isParsingPipe;

    public string CurrentServer => CurrentServerEndpoint.ToString();
    public ServerEndpoint CurrentServerEndpoint { get; private set; }

    public TcpStreamProcessor(IDataStorage storage, MessageAnalyzerV2 messageAnalyzer, ILogger? logger)
    {
        _storage = storage;
        _messageAnalyzer = messageAnalyzer;
        _logger = logger;
        _pipe = new Pipe(new PipeOptions(useSynchronizationContext: false));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Process(RawCapture raw)
    {
        var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
        var tcpPacket = packet.Extract<TcpPacket>();
        if (tcpPacket == null) return;

        var ipv4Packet = packet.Extract<IPv4Packet>();
        if (ipv4Packet == null) return;

        var payload = tcpPacket.PayloadData;
        if (payload == null || payload.Length == 0) return;

        var now = DateTime.Now;
        var seq = tcpPacket.SequenceNumber;
        var endpoint = ServerEndpoint.FromPacket(ipv4Packet, tcpPacket);

        // --- State-based processing ---
        if (!CurrentServerEndpoint.IsEmpty())
        {
            var isMatch = CurrentServerEndpoint == endpoint || CurrentServerEndpoint == endpoint.Reverse();

            if (isMatch)
            {
                // Traffic belongs to current server
                if (now - _lastAnyPacketAt > _idleTimeout)
                {
                    ForceReconnect("idle timeout");
                    // After reconnect, we might be able to detect a new server with the current packet
                    TryDetectServer(endpoint, payload, seq);
                }
                else
                {
                    ProcessServerTraffic(now, seq, payload);
                }
            }
            else
            {
                // New server traffic detected
                TryDetectServer(endpoint, payload, seq);
            }
        }
        else
        {
            // No current server, try to detect one
            TryDetectServer(endpoint, payload, seq);
        }
    }

    private void ProcessServerTraffic(DateTime now, uint seq, byte[] payload)
    {
        _lastAnyPacketAt = now;

        // Sequence initialization
        if (_tcpNextSeq == null)
        {
            _logger?.LogWarning("tcp_next_seq is NULL");
            if (payload.Length > 4 && BinaryPrimitives.ReadUInt32BigEndian(payload) < 0x0fffff)
            {
                _tcpNextSeq = seq;
            }
        }

        // Gap handling
        if (_tcpNextSeq != null)
        {
            var cmp = SeqCmp(seq, _tcpNextSeq.Value);
            if (cmp > 0)
            {
                _waitingGapSince ??= now;
                if (now - _waitingGapSince.Value > _gapTimeout)
                {
                    ForceResyncTo(seq);
                }
            }
            else if (cmp == 0)
            {
                _waitingGapSince = null;
            }
        }

        // Cache management
        if (_tcpNextSeq == null || SeqCmp(seq, _tcpNextSeq.Value) >= 0)
        {
            var payloadCopy = new byte[payload.Length];
            Buffer.BlockCopy(payload, 0, payloadCopy, 0, payload.Length);
            _tcpCache.TryAdd(seq, payloadCopy);
        }

        // Reassemble packets from cache and feed to the pipe
        ReassembleAndParse(now);

        // Periodic cache eviction
        if ((now - _tcpLastTime).TotalSeconds > 5)
        {
            _tcpCache.ForceEviction();
            // No count available; rely on Count if needed
        }
    }

    private void ReassembleAndParse(DateTime now)
    {
        var hasData = false;
        var messageBuffer = ArrayPool<byte>.Shared.Rent(4096);
        var messageLength = 0;

        try
        {
            while (_tcpNextSeq != null && _tcpCache.TryRemove(_tcpNextSeq.Value, out var cachedData))
            {
                if (messageLength + cachedData.Length > messageBuffer.Length)
                {
                    var newBuffer = ArrayPool<byte>.Shared.Rent(messageLength + cachedData.Length);
                    Buffer.BlockCopy(messageBuffer, 0, newBuffer, 0, messageLength);
                    ArrayPool<byte>.Shared.Return(messageBuffer);
                    messageBuffer = newBuffer;
                }

                Buffer.BlockCopy(cachedData, 0, messageBuffer, messageLength, cachedData.Length);
                messageLength += cachedData.Length;
                hasData = true;

                unchecked { _tcpNextSeq += (uint)cachedData.Length; }
                _tcpLastTime = now;
                _lastAnyPacketAt = now;
            }

            if (hasData)
            {
                _logger?.LogTrace("Writing {Bytes} bytes to pipe for parsing", messageLength);
                _pipe.Writer.Write(messageBuffer.AsSpan(0, messageLength));
                _ = _pipe.Writer.FlushAsync().GetAwaiter().GetResult();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(messageBuffer);
        }

        // Parse as many complete messages as possible from the pipe
        ParseFromPipe();
    }

    private void ParseFromPipe()
    {
        // Prevent reentrancy - if we're already parsing, skip this call
        if (_isParsingPipe)
        {
            return;
        }

        _isParsingPipe = true;
        try
        {
            var reader = _pipe.Reader;
            while (reader.TryRead(out var result))
            {
                var buffer = result.Buffer;
                long consumedBytes = 0;
                var parsedPackets = 0;

                while (true)
                {
                    if (buffer.Length < 4) break;

                    // Peek 4-byte big-endian length
                    var head = buffer.Slice(0, 4);
                    int packetSize;
                    if (head.IsSingleSegment)
                    {
                        packetSize = BinaryPrimitives.ReadInt32BigEndian(head.FirstSpan);
                    }
                    else
                    {
                        // Multi-segment sequence: allocate a small temporary array
                        var tmp = head.ToArray();
                        packetSize = BinaryPrimitives.ReadInt32BigEndian(tmp);
                    }

                    if (packetSize <= 4 || packetSize > 0x0FFFFF) break;

                    if (buffer.Length < packetSize) break; // not enough data yet

                    var packetSeq = buffer.Slice(0, packetSize);

                    // Zero-copy: pass span directly
                    try
                    {
                        if (packetSeq.IsSingleSegment)
                        {
                            _messageAnalyzer.Process(packetSeq.FirstSpan);
                        }
                        else
                        {
                            // Multi-segment: coalesce minimally into a pooled buffer
                            var exactPacket = new byte[packetSize];
                            packetSeq.CopyTo(exactPacket);
                            _messageAnalyzer.Process(exactPacket);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing message in ParseFromPipe");
                        // Continue processing remaining packets
                    }

                    parsedPackets++;
                    buffer = buffer.Slice(packetSize);
                    consumedBytes += packetSize;
                }

                if (parsedPackets > 0)
                {
                    _logger?.LogTrace("Parsed {Count} framed messages from pipe", parsedPackets);
                }

                var consumed = result.Buffer.GetPosition(consumedBytes);
                var examined = buffer.End; // leave remaining unconsumed
                reader.AdvanceTo(consumed, examined);

                if (result.IsCompleted && buffer.Length == 0)
                {
                    break;
                }
            }
        }
        finally
        {
            _isParsingPipe = false;
        }
    }

    private void TryDetectServer(ServerEndpoint endpoint, byte[] payload, uint sequenceNumber)
    {
        _logger?.LogTrace("TryDetect Server on {Endpoint} with {Bytes} bytes", endpoint.ToString(), payload.Length);
        try
        {
            if (payload.Length > 10 && payload[4] == 0)
            {
                var data = payload.AsSpan(10);
                if (data.Length > 0 && DetectFromData(data))
                {
                    SetCurrentServer(endpoint, sequenceNumber, payload.Length);
                    return;
                }
            }

            if (payload.Length == 0x62 && DetectLoginReturnSignature(payload.AsSpan()))
            {
                SetCurrentServer(endpoint, sequenceNumber, payload.Length);
                _lastAnyPacketAt = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error detecting server");
        }
    }

    private bool DetectLoginReturnSignature(ReadOnlySpan<byte> data)
    {
        return data.Slice(0, 10)
                    .SequenceEqual(_loginReturnSignature.AsSpan(0, 10)) &&
                data.Slice(14, 6)
                    .SequenceEqual(_loginReturnSignature.AsSpan(14, 6));
    }

    private bool DetectFromData(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray());
        var lenBuf = ArrayPool<byte>.Shared.Rent(4);

        try
        {
            while (ms.Position < ms.Length)
            {
                if (ms.Read(lenBuf, 0, 4) != 4) break;

                var len = BinaryPrimitives.ReadInt32BigEndian(lenBuf.AsSpan(0, 4));
                if (len < 4 || len > ms.Length - ms.Position + 4) break;

                var tmp = ArrayPool<byte>.Shared.Rent(len - 4);
                try
                {
                    if (ms.Read(tmp, 0, len - 4) != len - 4) break;


                    if (len - 4 >= 5 + _serverSignature.Length &&
                        tmp.AsSpan(5, _serverSignature.Length).SequenceEqual(_serverSignature))
                    {
                        return true;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tmp);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(lenBuf);
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCurrentServer(ServerEndpoint endpoint, uint sequenceNumber, int payloadLength)
    {
        if (CurrentServerEndpoint == endpoint) return;

        var prevServer = CurrentServerEndpoint.ToString();
        CurrentServerEndpoint = endpoint;
        ClearTcpCache();
        unchecked
        {
            _tcpNextSeq = sequenceNumber + (uint)payloadLength;
        }

        _lastAnyPacketAt = DateTime.Now;
        var currentServerStr = endpoint.ToString();
        _logger?.LogInformation(CoreLogEvents.ServerDetected, "Got Scene Server: {Server}", currentServerStr);

        // Mark as connected only after we have positively detected the server
        _storage.IsServerConnected = true;

        _storage.RaiseServerChanged(currentServerStr, prevServer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetCaptureState()
    {
        var prev = CurrentServerEndpoint.ToString();
        CurrentServerEndpoint = default;
        _tcpNextSeq = null;
        _tcpLastTime = DateTime.MinValue;
        _lastAnyPacketAt = DateTime.MinValue;
        _waitingGapSince = null;
        _tcpCache.Clear();

        try
        {
            _pipe.Writer.Complete();
        }
        catch
        {
            //Ignore
        }

        try
        {
            _pipe.Reader.Complete();
        }
        catch
        {
            //Ignore
        }

        _pipe = new Pipe(new PipeOptions(useSynchronizationContext: false));
        _storage.IsServerConnected = false;
        _logger?.LogInformation(CoreLogEvents.Reconnect, "Capture state reset, previous server was {Prev}", prev);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ForceReconnect(string reason)
    {
        _storage.IsServerConnected = false;
        _logger?.LogInformation(CoreLogEvents.Reconnect, "Forcing reconnect due to {Reason}", reason);
        ResetCaptureState();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ForceResyncTo(uint seq)
    {
        _logger?.LogWarning(CoreLogEvents.Resync, "Resyncing TCP stream to seq={Seq}", seq);
        _tcpCache.Clear();
        _tcpNextSeq = seq;
        _waitingGapSince = null;
        _tcpLastTime = DateTime.Now;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearTcpCache()
    {
        _tcpNextSeq = null;
        _tcpLastTime = DateTime.MinValue;
        _tcpCache.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SeqCmp(uint a, uint b)
    {
        return (int)(a - b);
    }

    public void Dispose()
    {
        try
        {
            _pipe.Writer.Complete();
        }
        catch
        {
            //Ignore
        }

        try
        {
            _pipe.Reader.Complete();
        }
        catch
        {
            //Ignore
        }
    }
}
