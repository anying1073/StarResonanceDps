using Microsoft.Extensions.Logging;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

public sealed class SyncServerTimeProcessor() : WorldNtfBaseProcessor(WorldNtfMessageId.SyncServerTime)
{
    public override void Process(byte[] payload)
    {
    }
}