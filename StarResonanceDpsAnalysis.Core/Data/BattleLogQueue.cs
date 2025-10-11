using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Data;

/// <summary>
/// High-performance message queue for battle logs with batching and backpressure support
/// </summary>
public sealed class BattleLogQueue : IDisposable
{
    private readonly Channel<BattleLog> _channel;
    private readonly DataStorageV2 _storageV2;
    private readonly ILogger<BattleLogQueue>? _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processorTask;

    // Configuration
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;

    // Metrics
    private long _totalProcessed;
    private long _totalBatches;
    private readonly Stopwatch _metricsTimer = Stopwatch.StartNew();

    public BattleLogQueue(
        DataStorageV2 storageV2,
        ILogger<BattleLogQueue>? logger = null,
        int capacity = 10_000,
        int batchSize = 100,
        TimeSpan? batchTimeout = null)
    {
        _storageV2 = storageV2 ?? throw new ArgumentNullException(nameof(storageV2));
        _logger = logger;
        _batchSize = batchSize;
        _batchTimeout = batchTimeout ?? TimeSpan.FromMilliseconds(50);

        _channel = Channel.CreateBounded<BattleLog>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = false, // Multiple producers (packet analyzer threads)
            SingleReader = true,  // Single consumer
            FullMode = BoundedChannelFullMode.Wait
        });

        _processorTask = Task.Run(ProcessLogsAsync, _cts.Token);
    }

    /// <summary>
    /// Enqueue a battle log for processing
    /// </summary>
    public async ValueTask EnqueueAsync(BattleLog log, CancellationToken cancellationToken = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(log, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            _logger?.LogWarning("Attempted to enqueue battle log to closed channel");
        }
    }

    /// <summary>
    /// Try to enqueue synchronously (returns false if channel is full)
    /// </summary>
    public bool TryEnqueue(BattleLog log)
    {
        return _channel.Writer.TryWrite(log);
    }

    /// <summary>
    /// Main processing loop - batches logs and processes them
    /// </summary>
    private async Task ProcessLogsAsync()
    {
        var batch = new List<BattleLog>(_batchSize);
        var reader = _channel.Reader;
        var lastFlush = Stopwatch.StartNew();

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var hasData = await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false);
                if (!hasData) break;

                // Read as many items as possible up to batch size
                while (batch.Count < _batchSize && reader.TryRead(out var log))
                {
                    batch.Add(log);
                }

                // Flush if batch is full or timeout elapsed
                var shouldFlush = batch.Count >= _batchSize || 
                                  lastFlush.Elapsed >= _batchTimeout ||
                                  reader.Completion.IsCompleted;

                if (shouldFlush && batch.Count > 0)
                {
                    await ProcessBatchAsync(batch).ConfigureAwait(false);
                    batch.Clear();
                    lastFlush.Restart();
                }
                else if (batch.Count == 0)
                {
                    // Nothing to process, yield to avoid tight loop
                    await Task.Delay(1, _cts.Token).ConfigureAwait(false);
                }
            }

            // Flush remaining items
            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Battle log processing cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fatal error in battle log processor");
        }
    }

    /// <summary>
    /// Process a batch of battle logs
    /// </summary>
    private Task ProcessBatchAsync(List<BattleLog> batch)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            foreach (var log in batch)
            {
                _storageV2.AddBattleLogInternal(log);
            }

            // After processing all logs, fire batched events once
            _storageV2.FlushPendingEvents();

            Interlocked.Add(ref _totalProcessed, batch.Count);
            Interlocked.Increment(ref _totalBatches);

            sw.Stop();

            // Log metrics periodically
            if (_metricsTimer.Elapsed.TotalSeconds >= 10)
            {
                var throughput = _totalProcessed / _metricsTimer.Elapsed.TotalSeconds;
                _logger?.LogInformation(
                    "BattleLogQueue Metrics: Processed {Total} logs in {Batches} batches. Throughput: {Throughput:F0} logs/sec. Last batch: {Count} logs in {Ms:F2}ms",
                    _totalProcessed, _totalBatches, throughput, batch.Count, sw.Elapsed.TotalMilliseconds);
                
                _metricsTimer.Restart();
                Interlocked.Exchange(ref _totalProcessed, 0);
                Interlocked.Exchange(ref _totalBatches, 0);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing battle log batch of {Count} items", batch.Count);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Get current queue depth (approximate)
    /// </summary>
    public int GetQueueDepth()
    {
        // Note: This is an estimate as Channel doesn't expose exact count
        return _channel.Reader.CanCount ? _channel.Reader.Count : -1;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();

        try
        {
            _processorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error waiting for processor task to complete");
        }

        _cts.Dispose();
    }
}
