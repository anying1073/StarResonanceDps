using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.V1;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Tests;

public class MessageAnalyzerTests : IDisposable
{
    private readonly MessageAnalyzer _messageAnalyzer = new MessageAnalyzer(DataStorage.Instance);
    public MessageAnalyzerTests()
    {
        DataStorage.Instance.ClearAllDpsData();
        DataStorage.Instance.ClearAllPlayerInfos();
        DataStorage.Instance.ClearCurrentPlayerInfo();
    }

    [Fact]
    public void Process_SyncNearEntities_PopulatesStaticStorage()
    {
        var playerUid = 55502962L;
        var payload = TestMessageBuilder.BuildSyncNearEntitiesPayload(playerUid, "Static Hero", 66);
        var envelope = TestMessageBuilder.BuildNotifyEnvelope(WorldNtfMessageId.SyncNearEntities, payload);

        _messageAnalyzer.Process(envelope);

        Assert.True(DataStorage.Instance.ReadOnlyPlayerInfoDatas.TryGetValue(playerUid, out var info));
        Assert.Equal("Static Hero", info!.Name);
        Assert.Equal(66, info.Level);
    }

    [Fact]
    public void Process_MalformedPacket_DoesNotThrow()
    {
        var malformed = new byte[] { 0x00, 0x00, 0x00, 0x01 };
        var exception = Record.Exception(() => _messageAnalyzer.Process(malformed));
        Assert.Null(exception);
    }

    public void Dispose()
    {
        DataStorage.Instance.ClearAllDpsData();
        DataStorage.Instance.ClearAllPlayerInfos();
        DataStorage.Instance.ClearCurrentPlayerInfo();
    }
}
