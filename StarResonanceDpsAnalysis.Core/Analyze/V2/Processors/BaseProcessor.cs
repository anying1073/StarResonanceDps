namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;

public abstract class BaseProcessor<T>(T messageId) : IMessageProcessor
{
    public T MessageId { get; } = messageId;
    public abstract void Process(byte[] payload);
}