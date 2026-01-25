using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Tests;

public class MessageAnalyzerTests : IDisposable
{
    public MessageAnalyzerTests()
    {
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        DataStorage.ClearCurrentPlayerInfo();
    }

    [Fact]
    public void Process_SyncNearEntities_PopulatesStaticStorage()
    {
        var playerUid = 55502962L;
        var payload = TestMessageBuilder.BuildSyncNearEntitiesPayload(playerUid, "Static Hero", 66);
        var envelope = TestMessageBuilder.BuildNotifyEnvelope(WorldNtfMessageId.SyncNearEntities, payload);

        MessageAnalyzer.Process(envelope);

        Assert.True(DataStorage.ReadOnlyPlayerInfoDatas.TryGetValue(playerUid, out var info));
        Assert.Equal("Static Hero", info!.Name);
        Assert.Equal(66, info.Level);
    }

    [Fact]
    public void Process_MalformedPacket_DoesNotThrow()
    {
        var malformed = new byte[] { 0x00, 0x00, 0x00, 0x01 };
        var exception = Record.Exception(() => MessageAnalyzer.Process(malformed));
        Assert.Null(exception);
    }

    public void Dispose()
    {
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        DataStorage.ClearCurrentPlayerInfo();
    }
}
