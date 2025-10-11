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
}