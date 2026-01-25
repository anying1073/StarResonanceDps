using Microsoft.Extensions.Logging;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

public class WorldNtfEmptyProcessor(ILogger logger, WorldNtfMessageId id) : WorldNtfBaseProcessor(id)
{
    private readonly WorldNtfMessageId _id = id;

    public override void Process(byte[] payload)
    {
        logger.LogTrace("Empty processor for {methodId}: ", _id);
    }
}