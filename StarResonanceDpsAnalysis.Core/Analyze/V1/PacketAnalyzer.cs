using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Logging;
using ZstdNet;

namespace StarResonanceDpsAnalysis.Core.Analyze.V1;

public class PacketAnalyzer(IMessageAnalyzer messageProcessor,ILogger<PacketAnalyzer>? logger = null) : IPacketAnalyzer
{
    public void Start()
    {
    }

    public void Stop()
    {
    }

    #region ========== 启用新分析 ==========

    public bool TryEnlistData(RawCapture data)
    {
        var ret = StartNewAnalyzer(null, data);
        ret.ConfigureAwait(false);
        return ret.Result;
    }

    public Task EnlistDataAsync(RawCapture data, CancellationToken token = default)
    {
        return StartNewAnalyzer(null, data);
    }

    public Task<bool> StartNewAnalyzer(ICaptureDevice? device, RawCapture raw)
    {
        return Task.Run(() =>
        {
            try
            {
                HandleRaw(device, raw);
                return true;
            }
            catch (Exception ex)
            {
                var taskIdStr = (Task.CurrentId?.ToString() ?? "?") + ' ';
                logger?.LogCritical(CoreLogEvents.AnalyzerError, ex,
                    "Packet analyzer crashed on thread {ThreadId}.", taskIdStr.Trim());
                return false;
            }
        });
    }

    /// <summary>
    /// Synchronously process a single packet on the calling thread (benchmark/helper).
    /// </summary>
    internal void ProcessInline(RawCapture raw)
    {
        HandleRaw(device: null, raw);
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ForceReconnect(string reason)
    {
        DataStorage.Instance.IsServerConnected = false;

        logger?.LogWarning(CoreLogEvents.Reconnect,
            "Reconnect due to {Reason} at {Time}", reason, DateTime.Now.ToString("HH:mm:ss"));
        // 清空状态，让后续包重新走“识别服务器”的逻辑
        ResetCaptureState();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ForceResyncTo(uint seq)
    {
        logger?.LogInformation(CoreLogEvents.Resync, "Resync TCP stream to seq={Seq}", seq);
        TcpCache.Clear();
        TcpStream.Position = 0;
        TcpStream.SetLength(0); // 完全丢弃当前未对齐的数据
        TcpNextSeq = seq; // 从这个分片开始重新累计
        WaitingGapSince = null;
        TcpLastTime = DateTime.Now;
    }

    #region ====== TCP 缓存清理 ======

    /// <summary>
    /// 清除 TCP 缓存，用于断线、错误重组等情况的重置操作
    /// </summary>
    private void ClearTcpCache()
    {
        TcpNextSeq = null;
        TcpLastTime = DateTime.MinValue;
        TcpCache.Clear();
    }

    #endregion

    #region ====== 常量定义 ======

    // === 超时与缺口处理 ===
    private readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10); // 当前流超过 10s 无包 => 重置
    private readonly TimeSpan GapTimeout = TimeSpan.FromSeconds(2); // 等待缺口分片超过 2s => 强制对齐

    private DateTime LastAnyPacketAt = DateTime.MinValue; // 当前已识别流的“上次任何方向收到包”的时间
    private DateTime? WaitingGapSince; // 正在等待缺口的起始时间戳


    /// <summary>
    /// 服务器签名
    /// </summary>
    /// <remarks>
    /// 不确定是不是服务器签名, StarResonanceDamageCounter 中, 后面还跟了 //c3SB?? 这样的注释
    /// </remarks>
    private readonly byte[] ServerSignature = [0x00, 0x63, 0x33, 0x53, 0x42, 0x00];

    /// <summary>
    /// 切换服务器时的登录返回包签名
    /// </summary>
    private readonly byte[] LoginReturnSignature =
    [
        0x00, 0x00, 0x00, 0x62,
        0x00, 0x03,
        0x00, 0x00, 0x00, 0x01,
        0x00, 0x11, 0x45, 0x14, // seq?
        0x00, 0x00, 0x00, 0x00,
        0x0a, 0x4e, 0x08, 0x01, 0x22, 0x24
    ];

    #endregion

    #region ====== 公共属性与状态 ======

    /// <summary>
    /// 当前连接的服务器地址
    /// </summary>
    public string CurrentServer { get; set; } = string.Empty;

    /// <summary>
    /// 期望的下一个 TCP 序列号
    /// </summary>
    private uint? TcpNextSeq { get; set; }

    /// <summary>
    /// TCP 分片缓存
    /// </summary>
    /// <remarks>
    /// Key 是 TCP 序列号, Value 是对应的分片数据, 用于重组多段 TCP 数据流 (比如一个完整的 protobuf 消息被拆分在多个包里)
    /// </remarks>
    private ConcurrentDictionary<uint, byte[]> TcpCache { get; } = new();

    private DateTime TcpLastTime { get; set; } = DateTime.MinValue;
    private object TcpLock { get; } = new();

    private MemoryStream TcpStream { get; } = new();
    private ConcurrentDictionary<uint, DateTime> TcpCacheTime { get; } = new();

    #endregion


    #region ========== Stage 1: 逐包解析入口 ==========

    // 统一的 TCP 序号比较（考虑 32 位回绕）
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SeqCmp(uint a, uint b)
    {
        return unchecked((int)(a - b));
        // a>=b => 非负；a<b（跨回绕）=> 负
    }


    /// <summary>
    /// 处理单个数据包
    /// </summary>
    private void HandleRaw(ICaptureDevice? device, RawCapture raw)
    {
        try
        {
            // 标记已开始监听服务器
            DataStorage.Instance.IsServerConnected = true;

            // 使用 PacketDotNet 解析为通用数据包对象（包含以太网/IP/TCP 等）
            var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

            // 提取 TCP 数据包（如果不是 TCP，会返回 null）
            var tcpPacket = packet.Extract<TcpPacket>();
            if (tcpPacket == null) return;

            // 提取 IPv4 数据包（如果不是 IPv4，也会返回 null）
            var ipv4Packet = packet.Extract<IPv4Packet>();
            if (ipv4Packet == null) return;

            // 获取 TCP 负载（即应用层数据）
            var payload = tcpPacket.PayloadData;
            if (payload == null || payload.Length == 0) return;

            // 构造当前包的源 -> 目的 IP 和端口的字符串，作为唯一标识
            var srcServer =
                $"{ipv4Packet.SourceAddress}:{tcpPacket.SourcePort} -> {ipv4Packet.DestinationAddress}:{tcpPacket.DestinationPort}";
            var revServer =
                $"{ipv4Packet.DestinationAddress}:{tcpPacket.DestinationPort} -> {ipv4Packet.SourceAddress}:{tcpPacket.SourcePort}";
            var now = DateTime.Now;
            lock (TcpLock)
            {
                // === 已有流的空闲超时：长时间没看到该流任一方向的包，直接重置，允许重新识别服务器 ===
                if (!string.IsNullOrEmpty(CurrentServer))
                {
                    if (CurrentServer == srcServer || CurrentServer == revServer)
                        LastAnyPacketAt = now;

                    if (LastAnyPacketAt != DateTime.MinValue && now - LastAnyPacketAt > IdleTimeout)
                    {
                        ForceReconnect("idle timeout");
                        // 继续往下走，让新包有机会被识别为新的服务器
                    }
                }

                if (CurrentServer != srcServer)
                {
                    try
                    {
                        // 尝试通过小包识别服务器
                        if (payload.Length > 10 && payload[4] == 0)
                        {
                            var data = payload.AsSpan(10);
                            if (data.Length > 0)
                            {
                                using var payloadMs = new MemoryStream(data.ToArray());
                                byte[] tmp;
                                do
                                {
                                    var lenBuffer = new byte[4];
                                    if (payloadMs.Read(lenBuffer, 0, 4) != 4)
                                        break;

                                    var len = lenBuffer.ReadInt32BigEndian();
                                    if (len < 4 || len > payloadMs.Length - 4)
                                    {
                                        break;
                                    }

                                    tmp = new byte[len - 4];
                                    if (payloadMs.Read(tmp, 0, tmp.Length) != tmp.Length)
                                    {
                                        break;
                                    }

                                    if (!tmp.Skip(5).Take(ServerSignature.Length).SequenceEqual(ServerSignature))
                                    {
                                        break;
                                    }

                                    try
                                    {
                                        if (CurrentServer != srcServer)
                                        {
                                            var prevServer = CurrentServer;
                                            CurrentServer = srcServer;

                                            ClearTcpCache();

                                            TcpNextSeq = tcpPacket.SequenceNumber + (uint)payload.Length;

                                            logger?.LogInformation(CoreLogEvents.ServerDetected,
                                                "Detected scene server {Server}", srcServer);

                                            DataStorage.Instance.ServerChange(CurrentServer, prevServer);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger?.LogError(CoreLogEvents.AnalyzerError, ex,
                                            "Error while detecting scene server");
                                    }
                                } while (tmp.Length > 0);
                            }
                        }

                        // 尝试通过登录返回包识别服务器(仍需测试)
                        if (payload.Length == 0x62)
                        {
                            if (payload.AsSpan(0, 10).SequenceEqual(LoginReturnSignature.AsSpan(0, 10)) &&
                                payload.AsSpan(14, 6).SequenceEqual(LoginReturnSignature.AsSpan(14, 6)))
                            {
                                if (CurrentServer != srcServer)
                                {
                                    var prevServer = CurrentServer;
                                    CurrentServer = srcServer;

                                    ClearTcpCache();

                                    TcpNextSeq = tcpPacket.SequenceNumber + (uint)payload.Length;

                                    logger?.LogInformation(CoreLogEvents.ServerDetected,
                                        "Detected scene server (login) {Server}", srcServer);

                                    DataStorage.Instance.ServerChange(CurrentServer, prevServer);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(CoreLogEvents.AnalyzerError, ex, "Error in server detection phase");
                    }

                    return;
                }
                // 这里已经是识别到的服务器的包了

                if (TcpNextSeq == null)
                {
                    logger?.LogWarning("Unexpected TCP capture state: tcp_next_seq is NULL");
                    if (payload.Length > 4 && BinaryPrimitives.ReadUInt32BigEndian(payload) < 0x0fffff)
                    {
                        TcpNextSeq = tcpPacket.SequenceNumber;
                    }
                }

                // === 缺口检测：新来的分片序号 > 期望序号，说明漏包了 ===
                if (TcpNextSeq != null)
                {
                    var cmp = SeqCmp(tcpPacket.SequenceNumber, TcpNextSeq.Value);
                    if (cmp > 0) // 前方缺口，等一小会
                    {
                        WaitingGapSince ??= now;
                        if (now - WaitingGapSince.Value > GapTimeout)
                        {
                            // 超时依旧没等到缺失分片，直接从当前分片重新对齐
                            ForceResyncTo(tcpPacket.SequenceNumber);
                        }
                    }
                    else if (cmp == 0)
                    {
                        // 正常顺序到达，清除缺口等待
                        WaitingGapSince = null;
                    }
                    // cmp < 0：旧/重复分片，按需忽略即可（无需额外动作）
                }

                // 缓存策略：只收“当前/未来”的段（避免重复旧段占用内存）
                if (TcpNextSeq == null || SeqCmp(tcpPacket.SequenceNumber, TcpNextSeq.Value) >= 0)
                {
                    TcpCache[tcpPacket.SequenceNumber] = payload.ToArray(); // 注意持有拷贝
                    //ScheduleEvictAfter(tcpPacket.SequenceNumber);
                }

                // 顺序拼接到一个临时缓冲（减少多次 ToArray）
                using var messageMs = new MemoryStream(4096);


                while (TcpNextSeq != null && TcpCache.Remove(TcpNextSeq.Value, out var cachedTcpData))
                {
                    messageMs.Write(cachedTcpData, 0, cachedTcpData.Length);
                    unchecked
                    {
                        TcpNextSeq += (uint)cachedTcpData.Length;
                    }

                    TcpLastTime = now; // <== 更新“最后拼接时间”
                    LastAnyPacketAt = now; // <== 只要拼接成功，也认为流是活跃的
                }

                // 直接把本次拼好的字节“追加”到 TcpStream 末尾，避免 CopyTo 的中间拷贝
                if (messageMs.Length > 0)
                {
                    var endPos = TcpStream.Length;
                    TcpStream.Position = endPos;
                    messageMs.Position = 0;
                    messageMs.CopyTo(TcpStream);
                }

                // 解析：把 Position 设到当前未消费起点（即 0），循环取包
                TcpStream.Position = 0;

                Span<byte> lenBuf = stackalloc byte[4];
                while (true)
                {
                    var start = TcpStream.Position;
                    if (TcpStream.Length - start < 4) break; // 不足 4 字节长度头

                    var n = TcpStream.Read(lenBuf);
                    if (n < 4)
                    {
                        TcpStream.Position = start;
                        break;
                    }

                    var packetSize = BinaryPrimitives.ReadInt32BigEndian(lenBuf);

                    // 协议约定：长度为“总长（含 4B 头）”
                    if (packetSize <= 4 || packetSize > 0x0FFFFF)
                    {
                        TcpStream.Position = start; // 回滚，留待上层/后续处理
                        break;
                    }

                    if (TcpStream.Length - start < packetSize)
                    {
                        TcpStream.Position = start; // 包未齐，一个字节都不消费
                        break;
                    }

                    // 够完整包：读出 [4B长度头 + 负载]
                    TcpStream.Position = start;
                    var messagePacket = new byte[packetSize];
                    var read = TcpStream.Read(messagePacket, 0, packetSize);
                    if (read != packetSize)
                    {
                        TcpStream.Position = start;
                        break;
                    }

                    try
                    {
                        messageProcessor.Process(messagePacket);
                    }
                    catch (ZstdException e)
                    {
                        if (e.Code == ZSTD_ErrorCode.ZSTD_error_corruption_detected)
                        {
                            logger?.LogWarning(CoreLogEvents.AnalyzerError, e,
                                "Detected corrupted zstd payload, reset stream buffer and wait for next packets");
                            TcpStream.SetLength(0);
                            TcpStream.Position = 0;
                            break;
                        }

                        throw;
                    }
                }

                // 压实剩余未解析数据到流头（零拷贝尽量减少临时数组）
                if (TcpStream.Position > 0)
                {
                    var remain = TcpStream.Length - TcpStream.Position;
                    if (remain > 0)
                    {
                        // 把剩余数据搬到前面
                        var buffer = TcpStream.GetBuffer(); // 需要确保 TcpStream 是可公开缓冲的 MemoryStream
                        Buffer.BlockCopy(buffer, (int)TcpStream.Position, buffer, 0, (int)remain);
                        TcpStream.Position = 0;
                        TcpStream.SetLength(remain);
                    }
                    else
                    {
                        TcpStream.SetLength(0);
                        TcpStream.Position = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 捕获异常，避免程序崩溃，同时打印异常信息
            logger?.LogError(CoreLogEvents.AnalyzerError, ex, "Packet processing error");
        }
    }


    public void ResetCaptureState()
    {
        lock (TcpLock)
        {
            CurrentServer = string.Empty; // 或者 null，看你的判断逻辑
            TcpNextSeq = null;
            TcpLastTime = DateTime.MinValue;

            TcpCache.Clear();
            TcpCacheTime.Clear();

            // 如果上一轮流变得很大，直接丢弃换新，阈值自定
            if (TcpStream.Capacity > 1 << 20) // >1MB 就换新
            {
                TcpStream.Position = 0;
                TcpStream.SetLength(0);
                try
                {
                    TcpStream.Dispose();
                }
                catch
                {
                    // Ignore
                }
            }
            else
            {
                TcpStream.Position = 0;
                TcpStream.SetLength(0);
            }
        }
    }

    #endregion
}