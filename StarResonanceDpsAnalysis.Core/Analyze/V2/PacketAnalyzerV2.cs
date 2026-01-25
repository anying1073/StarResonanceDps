using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Logging;

namespace StarResonanceDpsAnalysis.Core.Analyze;

/// <summary>
/// Orchestrates packet analysis by decoupling packet capture from processing using a producer-consumer pattern.
/// This class is the main entry point for packet analysis.
/// </summary>
public sealed class PacketAnalyzerV2(
    IDataStorage storage,
    MessageAnalyzerV2 messageAnalyzer,
    ILogger<PacketAnalyzerV2>? logger = null)
    : IDisposable, IPacketAnalyzer
{
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly TcpStreamProcessor _streamProcessor = new(storage, messageAnalyzer, logger);
    private Channel<RawCapture>? _channel;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private Task? _processingTask;

    public string CurrentServer => _streamProcessor.CurrentServer;
    public ServerEndpoint ServerEndpoint => _streamProcessor.CurrentServerEndpoint;

    public void Dispose()
    {
        Stop();
        _streamProcessor.Dispose();
        _stateLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Starts the packet analysis processing.
    /// </summary>
    public void Start()
    {
        _stateLock.Wait();
        try
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _channel = Channel.CreateUnbounded<RawCapture>(new UnboundedChannelOptions
            {
                SingleReader = false,
                // Capture producers (SharpPcap callbacks) may invoke on multiple threads.
                // Do not assume a single writer; let the channel handle concurrent writers.
                SingleWriter = false,
                AllowSynchronousContinuations = true
            });

            // _channel = Channel.CreateBounded<RawCapture>(new BoundedChannelOptions(10_000)
            // {
            //     FullMode = BoundedChannelFullMode.DropOldest,
            //     SingleReader = true,
            //     SingleWriter = false
            // });

            _processingTask = Task.Run(() => ProcessChannelAsync(_cts.Token), _cts.Token);
            _isRunning = true;
            logger?.LogInformation(CoreLogEvents.PacketStart, "Packet analyzer started");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Stops the packet analysis processing.
    /// </summary>
    public void Stop()
    {
        _stateLock.Wait();
        try
        {
            if (!_isRunning) return;

            _channel?.Writer.TryComplete();
            _cts?.Cancel();

            try
            {
                // Wait for the processing task to finish, with a timeout.
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // This is expected if the task is cancelled.
            }
            catch (Exception ex)
            {
                logger?.LogError(CoreLogEvents.AnalyzerError, ex, "Exception during packet processing task shutdown");
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _processingTask = null;
                _channel = null;
                _isRunning = false;
                logger?.LogInformation(CoreLogEvents.PacketStop, "Packet analyzer stopped");
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Synchronous inline processing on the calling thread (benchmark/helper).
    /// </summary>
    internal void ProcessInline(RawCapture raw)
    {
        _streamProcessor.Process(raw);
    }

    public bool TryEnlistData(RawCapture data)
    {
        if (!_isRunning || _channel == null)
        {
            logger?.LogWarning("Analyzer is not running. Packet dropped.");
            return false;
        }

        var writer = _channel.Writer;
        try
        {
            return writer.TryWrite(data);
        }
        catch (ChannelClosedException)
        {
            // Channel was closed while waiting, which is a normal part of shutdown.
        }

        return false;
    }

    /// <summary>
    /// Public-facing method to enqueue a raw packet for analysis.
    /// </summary>
    public async Task EnlistDataAsync(RawCapture data, CancellationToken token = default)
    {
        if (!_isRunning || _channel == null)
        {
            logger?.LogWarning("Analyzer is not running. Packet dropped.");
            return;
        }

        var writer = _channel.Writer;
        try
        {
            while (await writer.WaitToWriteAsync(token))
            {
                await writer.WriteAsync(data, token);
                break;
            }
        }
        catch (ChannelClosedException)
        {
            // Channel was closed while waiting, which is a normal part of shutdown.
        }
    }

    /// <summary>
    /// The long-running consumer task that processes packets from the channel.
    /// </summary>
    private async Task ProcessChannelAsync(CancellationToken token)
    {
        if (_channel == null) return;

        var reader = _channel.Reader;
        var count = 0;
        while (await reader.WaitToReadAsync(token))
        {
            while (reader.TryRead(out var raw))
            {
                try
                {
                    _streamProcessor.Process(raw);
                    if (++count % 1000 == 0)
                    {
                        logger?.LogTrace("Processed {Count} packets from channel", count);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(CoreLogEvents.AnalyzerError, ex, "Error processing packet from channel");
                }
            }
        }
    }

    public void ResetCaptureState()
    {
        _streamProcessor.ResetCaptureState();
    }
}