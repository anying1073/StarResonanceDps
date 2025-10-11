using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using StarResonanceDpsAnalysis.Core.Analyze;
using System.Collections.Concurrent;
using System.Threading;

namespace StarResonanceDpsAnalysis.Core.Data;

public static class PcapReplay
{
    /// <inheritdoc cref="ReplayFileAsync(string,StarResonanceDpsAnalysis.Core.Analyze.PacketAnalyzer,bool,double,System.Threading.CancellationToken)"/>
    public static Task ReplayFileAsync(this IPacketAnalyzer analyzer, string filePath, bool realtime = true,
        double speed = 1.0, CancellationToken token = default)
    {
        if (analyzer is PacketAnalyzerV2 v2)
        {
            return ReplayFileAsync(filePath, v2, realtime, speed, token);
        }
        else if (analyzer is PacketAnalyzer v1)
        {
            return ReplayFileAsync(filePath, v1, realtime, speed, token);
        }
        throw new ArgumentException("Unsupported analyzer type", nameof(analyzer));
    }

    /// <summary>
    /// Replay a pcap/pcapng file into a PacketAnalyzer.
    /// </summary>
    /// <param name="filePath">Path to .pcap or .pcapng</param>
    /// <param name="analyzer">Your PacketAnalyzer instance</param>
    /// <param name="realtime">If true, replay using original packet timestamps</param>
    /// <param name="speed">Playback speed (1.0 = real time, 2.0 = 2x faster)</param>
    /// <param name="token">Cancellation token</param>
    private static async Task ReplayFileAsync(string filePath, PacketAnalyzer analyzer, bool realtime = true,
        double speed = 1.0, CancellationToken token = default)
    {
        if (analyzer == null) throw new ArgumentNullException(nameof(analyzer));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        if (speed <= 0) speed = 1.0;

        // CaptureFileReaderDevice implements ICaptureDevice
        using var dev = new CaptureFileReaderDevice(filePath);
        dev.Open();

        try
        {
            DateTime? lastTs = null;

            // Read packets until EOF or cancelled.
            while (!token.IsCancellationRequested)
            {
                // Use the parameterless GetNextPacket() which returns RawCapture (null on EOF).
                PacketCapture ee;
                var rr = dev.GetNextPacket(out ee);
                if (rr == GetPacketStatus.NoRemainingPackets)
                    break;
                if (rr is GetPacketStatus.Error or GetPacketStatus.ReadTimeout)
                    continue;

                if (rr != GetPacketStatus.PacketRead)
                    continue;
                var raw = ee.GetPacket();

                try
                {
                    // Feed the analyzer with this raw capture.
                    // The analyzer implementation in this project exposes a ProcessPacket method.
                    var ret = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
                    var ipv4Packet = ret.Extract<IPv4Packet>();
                    if (ipv4Packet == null)
                        continue;
                    if (ipv4Packet.SourceAddress.ToString() == "58.217.182.174")
                    {
                        analyzer.StartNewAnalyzer(dev, raw);
                        // Optionally sleep to emulate original timing
                        if (realtime && lastTs.HasValue)
                        {
                            var nowDelta = raw.Timeval.Date - lastTs.Value;
                            var waitMs = (int)(nowDelta.TotalMilliseconds / speed);
                            if (waitMs > 0)
                                await Task.Delay(waitMs, token).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Keep going on analyzer errors, but log minimal info
                    Console.WriteLine($"Replay packet processing error: {ex.Message}");
                }

                lastTs = raw.Timeval.Date;
            }
        }
        finally
        {
            try
            {
                dev.Close();
            }
            catch
            {
                /* ignore */
            }
        }
    }

    /// <summary>
    /// Replay a pcap/pcapng file into a PacketAnalyzer.
    /// </summary>
    /// <param name="filePath">Path to .pcap or .pcapng</param>
    /// <param name="analyzer">Your PacketAnalyzer instance</param>
    /// <param name="realtime">If true, replay using original packet timestamps</param>
    /// <param name="speed">Playback speed (1.0 = real time, 2.0 = 2x faster)</param>
    /// <param name="token">Cancellation token</param>
    private static async Task ReplayFileAsync(string filePath, PacketAnalyzerV2 analyzer, bool realtime = true,
        double speed = 1.0, CancellationToken token = default)
    {
        if (analyzer == null) throw new ArgumentNullException(nameof(analyzer));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        if (speed <= 0) speed = 1.0;

        // CaptureFileReaderDevice implements ICaptureDevice
        using var dev = new CaptureFileReaderDevice(filePath);
        dev.Open();

        try
        {
            DateTime? lastTs = null;

            // Read packets until EOF or cancelled.
            while (!token.IsCancellationRequested)
            {
                // Use the parameterless GetNextPacket() which returns RawCapture (null on EOF).
                PacketCapture ee;
                var rr = dev.GetNextPacket(out ee);
                if (rr == GetPacketStatus.NoRemainingPackets)
                    break;
                if (rr is GetPacketStatus.Error or GetPacketStatus.ReadTimeout)
                    continue;

                if (rr != GetPacketStatus.PacketRead)
                    continue;
                var raw = ee.GetPacket();

                try
                {
                    // Feed the analyzer with this raw capture.
                    // The analyzer implementation in this project exposes a ProcessPacket method.
                    var ret = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
                    var ipv4Packet = ret.Extract<IPv4Packet>();
                    if (ipv4Packet == null)
                        continue;
                    if (ipv4Packet.SourceAddress.ToString() == "58.217.182.174")
                    {
                        await analyzer.EnlistDataAsync(raw, token);
                        // Optionally sleep to emulate original timing
                        if (realtime && lastTs.HasValue)
                        {
                            var nowDelta = raw.Timeval.Date - lastTs.Value;
                            var waitMs = (int)(nowDelta.TotalMilliseconds / speed);
                            if (waitMs > 0)
                                await Task.Delay(waitMs, token).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Keep going on analyzer errors, but log minimal info
                    Console.WriteLine($"Replay packet processing error: {ex.Message}");
                }

                lastTs = raw.Timeval.Date;
            }
        }
        finally
        {
            try
            {
                dev.Close();
            }
            catch
            {
                /* ignore */
            }
        }
    }

    /// <inheritdoc cref="ReplayFileEventDrivenAsync(string,StarResonanceDpsAnalysis.Core.Analyze.PacketAnalyzer,System.Threading.CancellationToken)"/>
    public static Task ReplayFileEventDrivenAsync(this PacketAnalyzer analyzer, string filePath,
        CancellationToken token = default)
    {
        return ReplayFileEventDrivenAsync(filePath, analyzer, token);
    }

    /// <inheritdoc cref="ReplayFileEventDrivenAsync(string,StarResonanceDpsAnalysis.Core.Analyze.PacketAnalyzer,System.Threading.CancellationToken)"/>
    public static Task ReplayFileEventDrivenAsync(this PacketAnalyzerV2 analyzer, string filePath,
        CancellationToken token = default)
    {
        return ReplayFileEventDrivenAsync(filePath, analyzer, token);
    }

    /// <summary>
    /// Event-driven replay: let CaptureFileReaderDevice drive events and forward them to analyzer.
    /// This method prefers the synchronous event model (Capture()) and runs the capture on a background task.
    /// </summary>
    private static Task ReplayFileEventDrivenAsync(string filePath, PacketAnalyzer analyzer,
        CancellationToken token = default)
    {
        if (analyzer == null) throw new ArgumentNullException(nameof(analyzer));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task.Run(() =>
        {
            var dev = new CaptureFileReaderDevice(filePath);

            PacketArrivalEventHandler handler = (_, e) =>
            {
                try
                {
                    var raw = e.GetPacket();
                    analyzer.StartNewAnalyzer(dev, raw);

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Replay error: {ex.Message}");
                }
            };

            try
            {
                dev.OnPacketArrival += handler;
                dev.Open();
                // Capture() is blocking and will fire events until EOF.
                dev.Capture();
                dev.Close();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                try
                {
                    dev.Close();
                }
                catch
                {
                    // ignored
                }

                tcs.TrySetException(ex);
            }
            finally
            {
                dev.OnPacketArrival -= handler;
            }
        }, token);

        return tcs.Task;
    }

    /// <summary>
    /// Event-driven replay: let CaptureFileReaderDevice drive events and forward them to analyzer.
    /// This method prefers the synchronous event model (Capture()) and runs the capture on a background task.
    /// </summary>
    private static Task ReplayFileEventDrivenAsync(string filePath, PacketAnalyzerV2 analyzer,
        CancellationToken token = default)
    {
        if (analyzer == null) throw new ArgumentNullException(nameof(analyzer));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task.Run(() =>
        {
            var dev = new CaptureFileReaderDevice(filePath);

            PacketArrivalEventHandler handler = (_, e) =>
            {
                try
                {
                    var raw = e.GetPacket();
                    analyzer.EnlistDataAsync(raw, token);

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Replay error: {ex.Message}");
                }
            };

            try
            {
                dev.OnPacketArrival += handler;
                dev.Open();
                // Capture() is blocking and will fire events until EOF.
                dev.Capture();
                dev.Close();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                try
                {
                    dev.Close();
                }
                catch
                {
                    // ignored
                }

                tcs.TrySetException(ex);
            }
            finally
            {
                dev.OnPacketArrival -= handler;
            }
        }, token);

        return tcs.Task;
    }

    /// <summary>
    /// Controller for replaying pcap files with controls like start, pause, stop, and single frame stepping.
    /// </summary>
    public class PcapReplayController
    {
        private readonly string _filePath;
        private readonly IPacketAnalyzer _analyzer;
        private readonly bool _realtime;
        private readonly double _speed;
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private Task? _processTask;
        private CaptureFileReaderDevice? _dev;
        private readonly BlockingCollection<RawCapture> _packetQueue = new();
        private readonly ManualResetEventSlim _resumeEvent = new(false);
        private ReplayState _state = ReplayState.Stopped;
        private bool _isStepping = false;
        private DateTime? _lastTs;

        public enum ReplayState { Stopped, Playing, Paused }

        public ReplayState State => _state;

        public PcapReplayController(string filePath, IPacketAnalyzer analyzer, bool realtime = true, double speed = 1.0)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _realtime = realtime;
            _speed = speed <= 0 ? 1.0 : speed;
        }

        public void Start()
        {
            if (_state == ReplayState.Playing) return;
            if (_cts == null || _cts.IsCancellationRequested)
            {
                _cts = new CancellationTokenSource();
            }
            if (_readTask == null)
            {
                _dev = new CaptureFileReaderDevice(_filePath);
                _dev.Open();
                _readTask = Task.Run(() => ReadPackets(_cts.Token), _cts.Token);
            }
            _state = ReplayState.Playing;
            _resumeEvent.Set();
            if (_processTask == null || _processTask.IsCompleted)
            {
                _processTask = Task.Run(() => ProcessPackets(_cts.Token), _cts.Token);
            }
        }

        public void Pause()
        {
            if (_state == ReplayState.Playing)
            {
                _state = ReplayState.Paused;
                _resumeEvent.Reset();
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _state = ReplayState.Stopped;
            _resumeEvent.Reset();
            _packetQueue.CompleteAdding();
            try
            {
                _dev?.Close();
            }
            catch
            {
                // ignore
            }
        }

        public void Step()
        {
            if (_state == ReplayState.Paused)
            {
                _isStepping = true;
                _resumeEvent.Set();
            }
        }

        private void ReadPackets(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    PacketCapture ee;
                    var rr = _dev!.GetNextPacket(out ee);
                    if (rr == GetPacketStatus.NoRemainingPackets)
                        break;
                    if (rr is GetPacketStatus.Error or GetPacketStatus.ReadTimeout)
                        continue;
                    if (rr != GetPacketStatus.PacketRead)
                        continue;
                    var raw = ee.GetPacket();
                    _packetQueue.Add(raw, token);
                }
                _packetQueue.CompleteAdding();
            }
            catch (OperationCanceledException) { }
        }

        private async Task ProcessPackets(CancellationToken token)
        {
            try
            {
                foreach (var raw in _packetQueue.GetConsumingEnumerable(token))
                {
                    if (_state == ReplayState.Paused)
                    {
                        await Task.Run(() => _resumeEvent.Wait(token), token);
                    }

                    try
                    {
                        var ret = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
                        var ipv4Packet = ret.Extract<IPv4Packet>();
                        if (ipv4Packet == null)
                            continue;
                        if (ipv4Packet.SourceAddress.ToString() == "58.217.182.174")
                        {
                            if (_analyzer is PacketAnalyzerV2 v2)
                            {
                                await v2.EnlistDataAsync(raw, token);
                            }
                            else if (_analyzer is PacketAnalyzer v1)
                            {
                                v1.StartNewAnalyzer(_dev!, raw);
                            }

                            if (_realtime && _lastTs.HasValue)
                            {
                                var nowDelta = raw.Timeval.Date - _lastTs.Value;
                                var waitMs = (int)(nowDelta.TotalMilliseconds / _speed);
                                if (waitMs > 0)
                                    await Task.Delay(waitMs, token).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Replay packet processing error: {ex.Message}");
                    }

                    _lastTs = raw.Timeval.Date;

                    if (_isStepping)
                    {
                        _state = ReplayState.Paused;
                        _resumeEvent.Reset();
                        _isStepping = false;
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _state = ReplayState.Stopped;
            }
        }
    }
}