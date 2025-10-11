namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;

/// <summary>
/// Defines a contract for processing a specific type of message payload.
/// </summary>
internal interface IMessageProcessor
{
    /// <summary>
    /// Processes the given message payload.
    /// </summary>
    /// <param name="payload">The raw byte payload of the message.</param>
    void Process(byte[] payload);
}
