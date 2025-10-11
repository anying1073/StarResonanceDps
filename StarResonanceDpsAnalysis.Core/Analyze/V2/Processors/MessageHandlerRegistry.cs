using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;

/// <summary>
/// A registry that maps message method IDs to their corresponding processors.
/// </summary>
internal sealed class MessageHandlerRegistry(IDataStorage storage, ILogger? logger)
{
    private readonly Dictionary<MessageMethod, IMessageProcessor> _processors = new()
    {
        { MessageMethod.SyncNearEntities, new SyncNearEntitiesProcessor(storage, logger) },
        { MessageMethod.SyncContainerData, new SyncContainerDataProcessor(storage, logger) },
        { MessageMethod.SyncContainerDirtyData, new SyncContainerDirtyDataProcessor(storage, logger) },
        { MessageMethod.SyncToMeDeltaInfo, new SyncToMeDeltaInfoProcessor(storage, logger) },
        { MessageMethod.SyncNearDeltaInfo, new SyncNearDeltaInfoProcessor(storage, logger) }
    };

    /// <summary>
    /// Tries to get the processor for a given method ID.
    /// </summary>
    /// <param name="methodId">The method ID of the message.</param>
    /// <param name="processor">The resolved processor, if found.</param>
    /// <returns>True if a processor was found, otherwise false.</returns>
    public bool TryGetProcessor(uint methodId, [NotNullWhen(returnValue: true)] out IMessageProcessor? processor)
    {
        var method = (MessageMethod)methodId;
        if (Enum.IsDefined(typeof(MessageMethod), method))
        {
            return _processors.TryGetValue(method, out processor);
        }

        Debug.WriteLine($"No processor found for method ID: {methodId}");
        processor = null;
        return false;
    }

    /// <inheritdoc cref="TryGetProcessor(uint,out StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.IMessageProcessor?)"/>
    public bool TryGetProcessor(MessageMethod method, [NotNullWhen(returnValue: true)] out IMessageProcessor? processor)
    {
        return _processors.TryGetValue(method, out processor);
    }
}
