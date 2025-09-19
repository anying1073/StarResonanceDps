using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using StarResonanceDpsAnalysis.Core.Analyze;

namespace StarResonanceDpsAnalysis.Core.Data;

public static class PcapReplay
{
    /// <summary>
    /// Replay a pcap/pcapng file into a PacketAnalyzer.
    /// </summary>
    /// <param name="filePath">Path to .pcap or .pcapng</param>
    /// <param name="analyzer">Your PacketAnalyzer instance</param>
    /// <param name="realtime">If true, replay using original packet timestamps</param>
    /// <param name="speed">Playback speed (1.0 = real time, 2.0 = 2x faster)</param>
    /// <param name="token">Cancellation token</param>
    public static async Task ReplayFileAsync(string filePath, PacketAnalyzer analyzer, bool realtime = true,
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
            var isFirst = true;

            // Read packets until EOF or cancelled.
            while (!token.IsCancellationRequested)
            {
                // Use the parameterless GetNextPacket() which returns RawCapture (null on EOF).
                PacketCapture ee;
                _ = dev.GetNextPacket(out ee);
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
            try { dev.Close(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Event-driven replay: let CaptureFileReaderDevice drive events and forward them to analyzer.
    /// This method prefers the synchronous event model (Capture()) and runs the capture on a background task.
    /// </summary>
    public static Task ReplayFileEventDrivenAsync(string filePath, PacketAnalyzer analyzer, CancellationToken token = default)
    {
        if (analyzer == null) throw new ArgumentNullException(nameof(analyzer));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task.Run(() =>
        {
            var dev = new CaptureFileReaderDevice(filePath);
            PacketArrivalEventHandler? handler = null;

            handler = (sender, e) =>
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
                try { dev.Close(); } catch { }
                tcs.TrySetException(ex);
            }
            finally
            {
                if (handler != null)
                    dev.OnPacketArrival -= handler;
            }
        }, token);

        return tcs.Task;
    }

}