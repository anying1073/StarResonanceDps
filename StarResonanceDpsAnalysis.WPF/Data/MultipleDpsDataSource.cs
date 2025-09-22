using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace StarResonanceDpsAnalysis.WPF.Data;

internal class MultipleDpsDataSource : BaseDataSource
{
    private readonly Channel<DpsEvent> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _fanInTasks = new();
    private long _droppedEvents;

    public MultipleDpsDataSource(IEnumerable<IDataSource> dataSources, int capacity = 50_000)
    {
        _channel = Channel.CreateBounded<DpsEvent>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        foreach (var ds in dataSources)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    await foreach (var ev in ds.GetDpsEventsAsync(_cts.Token).ConfigureAwait(false))
                    {
                        // Non-blocking fan-in: prefer dropping to blocking capture threads.
                        if (!_channel.Writer.TryWrite(ev))
                            Interlocked.Increment(ref _droppedEvents);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    /* swallow individual source failures */
                }
            }, _cts.Token);

            _fanInTasks.Add(task);
        }
    }

    public long DroppedEvents => Interlocked.Read(ref _droppedEvents);

    public override async IAsyncEnumerable<DpsEvent> GetDpsEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var reader = _channel.Reader;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cts.Token);
        while (await reader.WaitToReadAsync(linkedCts.Token).ConfigureAwait(false))
        {
            while (reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    public override void Dispose()
    {
        _cts.Cancel();
        try
        {
            Task.WhenAll(_fanInTasks).Wait(500);
        }
        catch
        {
            /* best-effort */
        }

        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}