using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace StarResonanceDpsAnalysis.WPF.Data;

internal class DpsDummyDataSource : BaseDataSource
{
    private readonly Channel<DpsEvent> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _producerTask;

    public DpsDummyDataSource(int capacity = 10_000)
    {
        _channel = Channel.CreateBounded<DpsEvent>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _producerTask = Task.Run(async () =>
        {
            long i = 0;
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token).ConfigureAwait(false);
                    var ev = new DpsEvent { Timestamp = DateTimeOffset.Now, Value = (int)i++ };
                    await _channel.Writer.WriteAsync(ev, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            finally
            {
                _channel.Writer.TryComplete();
            }
        }, _cts.Token);
    }

    public override async IAsyncEnumerable<DpsEvent> GetDpsEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var reader = _channel.Reader;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
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
            _producerTask.Wait(500);
        }
        catch
        {
            /* best-effort */
        }

        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}
