using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.World;

internal sealed class WorldMessageHandlerRegistry
{
    private readonly Dictionary<WorldMessageId, WorldBaseProcessor> _processors;

    public WorldMessageHandlerRegistry(ILogger? logger)
    {
        logger ??= NullLogger.Instance;
        _processors = new Dictionary<WorldMessageId, WorldBaseProcessor>
        {
            { WorldMessageId.ChangeCharFunctionState, new WorldChangeCharFunctionStateProcessor(logger) }
        };
    }

    /// <summary>
    /// Tries to get the processor for a given method ID.
    /// </summary>
    /// <param name="methodId">The method ID of the message.</param>
    /// <param name="processor">The resolved processor, if found.</param>
    /// <returns>True if a processor was found, otherwise false.</returns>
    public bool TryGetProcessor(uint methodId, [NotNullWhen(returnValue: true)] out IMessageProcessor? processor)
    {
        var method = (WorldMessageId)methodId;
        if (Enum.IsDefined(typeof(WorldMessageId), method))
        {
            if (_processors.TryGetValue(method, out var ret))
            {
                processor = ret;
                return true;
            }

            Debug.WriteLine($"No processor registered for method: {method} ({methodId})");
            processor = null;
            return false;
        }

        Debug.WriteLine($"No processor found for method ID: {methodId}");
        processor = null;
        return false;
    }

    /// <inheritdoc cref="TryGetProcessor(uint,out StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.IMessageProcessor?)"/>
    public bool TryGetProcessor(WorldMessageId id, [NotNullWhen(returnValue: true)] out IMessageProcessor? processor)
    {
        var result = _processors.TryGetValue(id, out var ret);
        processor = ret;
        return result;
    }
}

public enum WorldMessageId : uint
{
    ChangeCharFunctionState = 1u
}

public class WorldChangeCharFunctionStateProcessor(ILogger logger) : WorldBaseProcessor(WorldMessageId.ChangeCharFunctionState)
{
    public override void Process(byte[] payload)
    {
        var state = Zproto.World.Types.ChangeCharFunctionState.Parser.ParseFrom(payload);
        logger?.LogTrace("ChangeCharState: ");
        // Implementation for processing ChangeCharFunctionState message
    }
}