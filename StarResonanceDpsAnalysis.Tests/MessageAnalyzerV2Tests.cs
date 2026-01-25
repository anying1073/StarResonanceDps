using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Tests;

public class MessageAnalyzerV2Tests
{
    [Fact]
    public void Process_SyncNearEntities_UpdatesPlayerInfo()
    {
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        var analyzer = new MessageAnalyzerV2(storage);
        var playerUid = 55502962L;
        var payload = TestMessageBuilder.BuildSyncNearEntitiesPayload(playerUid, "Realtime Hero", 88);
        var envelope = TestMessageBuilder.BuildNotifyEnvelope(WorldNtfMessageId.SyncNearEntities, payload);

        analyzer.Process(envelope);

        Assert.True(storage.ReadOnlyPlayerInfoDatas.TryGetValue(playerUid, out var info));
        Assert.Equal("Realtime Hero", info!.Name);
        Assert.Equal(88, info.Level);
    }

    [Fact]
    public void Process_InvalidPacket_DoesNotThrow()
    {
        var storage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        var analyzer = new MessageAnalyzerV2(storage);
        var malformed = new byte[] { 0x00, 0x00, 0x00, 0x02, 0x00 }; // too short payload

        var exception = Record.Exception(() => analyzer.Process(malformed));
        Assert.Null(exception);
    }
}