using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SharpPcap;

namespace StarResonanceDpsAnalysis.WPF.Data;

internal abstract class BaseDataSource : IDataSource, IDisposable
{
    public abstract IAsyncEnumerable<DpsEvent> GetDpsEventsAsync(CancellationToken cancellation = default);
    public abstract void Dispose();
}

internal class CapturedDpsDataSource : BaseDataSource
{
    private readonly Channel<DpsEvent> _channel;
    private readonly ICaptureDevice? _device;
    private long _droppedEvents;

    public CapturedDpsDataSource(ICaptureDevice? device, int capacity = 50_000)
    {
        _channel = Channel.CreateBounded<DpsEvent>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = false, // capture callbacks may come on multiple threads
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _device = device;
        if (_device == null)
        {
            _channel.Writer.TryComplete();
            return;
        }

        // Use non-blocking write on packet arrival to avoid blocking capture threads.
        _device.OnPacketArrival += (s, e) =>
        {
            var dpsEvent = ParsePacket(e);
            if (dpsEvent == null) return;
            if (_channel.Writer.TryWrite(dpsEvent)) return;
            Interlocked.Increment(ref _droppedEvents);
        };

        _device.Open();
        _device.StartCapture();
    }

    public long DroppedEvents => Interlocked.Read(ref _droppedEvents);

    public override async IAsyncEnumerable<DpsEvent> GetDpsEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var reader = _channel.Reader;
        while (await reader.WaitToReadAsync(cancellation).ConfigureAwait(false))
        {
            while (reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    private DpsEvent? ParsePacket(PacketCapture e)
    {
        // TODO: Implement actual parsing logic
        return new DpsEvent { Timestamp = DateTimeOffset.Now, Value = e.Data.Length };
    }

    public override void Dispose()
    {
        try
        {
            if (_device == null) return;
            try
            {
                _device.StopCapture();
            }
            catch (Exception)
            {
                // Ignore
            }

            try
            {
                _device.Close();
            }
            catch (Exception)
            {
                // Ignore
            }
        }
        finally
        {
            _channel.Writer.TryComplete();
        }
    }
}

// Example event class
public class DpsEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public int Value { get; set; }
}