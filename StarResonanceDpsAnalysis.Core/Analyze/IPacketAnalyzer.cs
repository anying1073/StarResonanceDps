using SharpPcap;

namespace StarResonanceDpsAnalysis.Core.Analyze;

public interface IPacketAnalyzer
{
    string CurrentServer { get; }

    /// <summary>
    /// Starts the packet analysis processing.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the packet analysis processing.
    /// </summary>
    void Stop();

    void ResetCaptureState();

    bool TryEnlistData(RawCapture data);

    /// <summary>
    /// Public-facing method to enqueue a raw packet for analysis.
    /// </summary>
    Task EnlistDataAsync(RawCapture data, CancellationToken token = default);
}