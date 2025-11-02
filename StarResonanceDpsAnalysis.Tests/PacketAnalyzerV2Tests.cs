using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using PacketDotNet;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Tests;

public class PacketAnalyzerV2Tests
{
    private readonly PacketAnalyzerV2 _analyzer;
    private readonly IDataStorage _dataStorage;

    public PacketAnalyzerV2Tests()
    {
        _dataStorage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        var messageAnalyzer = new MessageAnalyzerV2(_dataStorage, NullLogger<MessageAnalyzerV2>.Instance);
        _analyzer = new PacketAnalyzerV2(_dataStorage, messageAnalyzer,
            NullLogger<PacketAnalyzerV2>.Instance);
    }

    [Fact]
    public void Start_WhenNotRunning_StartsProcessingTask()
    {
        // Act
        _analyzer.Start();

        // Assert
        Assert.True(TestUtils.GetFieldValue<bool>(_analyzer, "_isRunning"));
        Assert.NotNull(TestUtils.GetFieldValue<Task>(_analyzer, "_processingTask"));
        Assert.NotNull(TestUtils.GetFieldValue<Channel<RawCapture>>(_analyzer, "_channel"));

        // Cleanup
        _analyzer.Stop();
    }

    [Fact]
    public void Start_WhenAlreadyRunning_DoesNothing()
    {
        // Arrange
        _analyzer.Start();
        var initialTask = TestUtils.GetFieldValue<Task>(_analyzer, "_processingTask");

        // Act
        _analyzer.Start();

        // Assert
        var sameTask = TestUtils.GetFieldValue<Task>(_analyzer, "_processingTask");
        Assert.Same(initialTask, sameTask);

        // Cleanup
        _analyzer.Stop();
    }

    [Fact]
    public void Stop_WhenRunning_StopsProcessingTask()
    {
        // Arrange
        _analyzer.Start();

        // Act
        _analyzer.Stop();

        // Assert
        Assert.False(TestUtils.GetFieldValue<bool>(_analyzer, "_isRunning"));
        Assert.Null(TestUtils.GetFieldValue<Task>(_analyzer, "_processingTask"));
        Assert.Null(TestUtils.GetFieldValue<Channel<RawCapture>>(_analyzer, "_channel"));
    }

    [Fact]
    public async Task EnlistDataAsync_WhenRunning_ProcessesPacket()
    {
        // Arrange
        _analyzer.Start();
        // This is a valid TCP packet that should trigger processing logic.
        var rawCapture = new RawCapture(LinkLayers.Ethernet, new PosixTimeval(), TestPacket.ServerSignaturePacket);

        // Act
        await _analyzer.EnlistDataAsync(rawCapture);

        // Assert
        // Give the background processor a moment to run.
        await Task.Delay(200);

        // Verify that the processor has started its work by checking for a side effect.
        // In this case, TcpStreamProcessor.Process sets IsServerConnected to true.
        // _dataStorage.VerifySet(s => s.IsServerConnected = true, Times.AtLeastOnce);
        Assert.True(_dataStorage.IsServerConnected);

        // Cleanup
        _analyzer.Stop();
    }

    [Fact]
    public async Task EnlistDataAsync_WhenNotRunning_DoesNotWrite()
    {
        // Arrange
        var rawCapture = new RawCapture(LinkLayers.Ethernet, new PosixTimeval(), []);

        // Act & Assert
        // This should complete without error and without writing.
        await _analyzer.EnlistDataAsync(rawCapture);

        // To be sure, we start it and check the channel is empty
        _analyzer.Start();
        var channel = TestUtils.GetFieldValue<Channel<RawCapture>>(_analyzer, "_channel");
        Assert.False(channel.Reader.TryRead(out _)); // Should be false, as nothing should have been written.

        // We can't reliably check the channel state after the fact in a race-condition-free way.
        // The main point is that it doesn't throw and logs a warning (which we can't easily test here).
        // The fact that it doesn't crash is the most important part of this test.
        _analyzer.Stop();
    }

    [Fact]
    public void Dispose_StopsAnalyzer()
    {
        // Arrange
        _analyzer.Start();

        // Act
        _analyzer.Dispose();

        // Assert
        Assert.False(TestUtils.GetFieldValue<bool>(_analyzer, "_isRunning"));
    }

    [Fact]
    public void Stop_CanBeRestarted()
    {
        // Arrange
        _analyzer.Start();
        _analyzer.Stop();

        // Act
        _analyzer.Start();

        // Assert
        Assert.True(TestUtils.GetFieldValue<bool>(_analyzer, "_isRunning"));
        Assert.NotNull(TestUtils.GetFieldValue<Task>(_analyzer, "_processingTask"));
        Assert.NotNull(TestUtils.GetFieldValue<Channel<RawCapture>>(_analyzer, "_channel"));

        // Cleanup
        _analyzer.Stop();
    }

    [Fact]
    public async Task EnlistDataAsync_WhenStopping_DoesNotThrow()
    {
        // Arrange
        _analyzer.Start();
        var rawCapture = new RawCapture(LinkLayers.Null, new PosixTimeval(), []);

        // Act
        // Stop the analyzer concurrently while the enlist task might be running.
        var enlistTask = _analyzer.EnlistDataAsync(rawCapture);
        _analyzer.Stop();
        await enlistTask; // This should complete without throwing ChannelClosedException.

        // Assert
        // The main assertion is that no exception was thrown.
        Assert.True(enlistTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task CheckServerDetect_ServerSignature()
    {
        // Arrange
        _analyzer.Start();
        var streamProcessor = TestUtils.GetFieldValue<TcpStreamProcessor>(_analyzer, "_streamProcessor");

        // Process a packet to change the processor's state.
        var dummyPacket = new RawCapture(LinkLayers.Ethernet, new PosixTimeval(), TestPacket.ServerSignaturePacket);
        await _analyzer.EnlistDataAsync(dummyPacket);
        await Task.Delay(100);

        // Assert
        var currentServer = _analyzer.CurrentServer;
        // The default ToString for an empty ServerEndpoint is ":: -> ::"
        Assert.Equal("36.152.0.122:14270 -> 192.168.0.153:37230", currentServer);

        // Cleanup
        _analyzer.Stop();
    }

    [Fact]
    public async Task CheckServerDetect_LoginSignature()
    {
        // Arrange
        _analyzer.Start();

        // Process a packet to change the processor's state.
        var dummyPacket = new RawCapture(LinkLayers.Ethernet, new PosixTimeval(), TestPacket.LoginSignatureBytes);
        await _analyzer.EnlistDataAsync(dummyPacket);
        await Task.Delay(100);

        // Assert
        var currentServer = _analyzer.CurrentServer;
        // The default ToString for an empty ServerEndpoint is ":: -> ::"
        Assert.Equal("36.152.0.122:10424 -> 192.168.0.153:16956", currentServer);

        // Cleanup
        _analyzer.Stop();
    }

    [Fact]
    public async Task ResetCaptureState_ResetsProcessorState()
    {
        // Arrange
        _analyzer.Start();

        // Process a packet to change the processor's state.
        var dummyPacket = new RawCapture(LinkLayers.Ethernet, new PosixTimeval(), TestPacket.ServerSignaturePacket);
        await _analyzer.EnlistDataAsync(dummyPacket);
        Thread.Sleep(100);

        // Assert
        var currentServer = _analyzer.CurrentServer;
        // The default ToString for an empty ServerEndpoint is ":: -> ::"
        Assert.Equal("36.152.0.122:14270 -> 192.168.0.153:37230", currentServer);
        _analyzer.ResetCaptureState();
        currentServer = _analyzer.CurrentServer;
        // The default ToString for an empty ServerEndpoint is ":: -> ::"
        Assert.Equal(":0 -> :0", currentServer);

        // Cleanup
        _analyzer.Stop();
    }
}

internal static class TestPacket
{
    public static readonly byte[] RealTcpPacket =
    [
        0XC4, 0X3, 0XA8, 0X5, 0XB2, 0X7F, // Dst MAC
        0XB4, 0XF, 0X3B, 0X1A, 0X26, 0XB0, // Src MAC
        0X8, 0X0, // Type: IPv4
        // IPv4 Header
        0X45, 0X0, 0X0, 0X2E, // Version, IHL, ToS, Total Length (46)
        0X8, 0X42, 0X40, 0X0, // Identification, Flags, Fragment Offset
        0X33, 0X6, 0X59, 0X35, // TTL, Protocol (TCP), Header Checksum
        0X24, 0X98, 0X0, 0X7A, // Src IP: 36.152.0.122
        0XC0, 0XA8, 0X0, 0X99, // Dst IP: 192.168.0.153
        // TCP Header
        0X13, 0X8B, 0X50, 0X25, // Src Port 5003, Dst Port 20581
        0XC7, 0XBE, 0XFC, 0XAC, // Sequence Number
        0X9D, 0X25, 0X17, 0XD1, // Ack Number
        0X50, 0X18, 0X0, 0X3F, // Data Offset, Flags (ACK), Window Size
        0XEC, 0X17, 0X0, 0X0, // Checksum, Urgent Pointer
        0X0, 0X0, 0X0, 0X6, 0X0, 0X4 // Data (6 bytes)
    ];

    public static readonly byte[] ServerSignaturePacket =
    [
        0xc4, 0x3, 0xa8, 0x5, 0xb2, 0x7f,  // dst mac
        0xb4, 0xf, 0x3b, 0x1a, 0x26, 0xb0, // Src mac
        0x8, 0x0,  // Type: IPv4
        // IPv4 Header
        0x45, 0x0, 0x1, 0x70,  // Version, IHL, ToS, Total Length (368)
        0x23, 0x2a, 0x40, 0x0,  // Identification, Flags, Fragment Offset
        0x33, 0x6, 0x3d, 0xb,  // TTL, Protocol (TCP), Header Checksum
        0x24, 0x98, 0x0, 0x7a,  // Src IP: 36.152.0.122
        0xc0, 0xa8, 0x0, 0x99,  // Dst IP: 192.168.0.153
        // TCP Header
        0x37, 0xbe, 0x91, 0x6e,  // Src Port 14270, Dst Port 37230
        0x34, 0x9d,
        0x4b, 0xd, 0xfe, 0x60, 0x66, 0x8c, 0x50, 0x18,
        0x0, 0x3f, 0xe4, 0x97, 0x0, 0x0, 0x0, 0x0, 0x1,
        0x48, 0x0, 0x6, 0x0, 0x0, 0x9, 0xa2, 0x0,
        0x0, 0x0, 0x26, 0x0, 0x2, 0x0, 0x0, 0x0,
        0x0, 0x63, 0x33, 0x53, 0x42, 0x0, 0x0, 0x0, // 0x00, 0x63, 0x33, 0x53, 0x42, 0x00 Server signature
        0x0, 0x0, 0x0, 0x0, 0x6, 0x12, 0x6, 0x8,
        0xc0, 0x85, 0x56, 0x10, 0x1, 0x12, 0x6, 0x8,
        0xc0, 0x85, 0x42, 0x10, 0x1, 0x0, 0x0, 0x0,
        0xf0, 0x0, 0x2, 0x0, 0x0, 0x0, 0x0, 0x63,
        0x33, 0x53, 0x42, 0x0, 0x0, 0x0, 0x0, 0x0,
        0x0, 0x0, 0x2d, 0xa, 0x36, 0x8, 0xc0, 0x85,
        0x5a, 0x12, 0x2e, 0x12, 0x5, 0x8, 0xb, 0x12,
        0x1, 0x9, 0x12, 0xe, 0x8, 0x65, 0x12, 0xa,
        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        0xff, 0x1, 0x12, 0x5, 0x8, 0x6c, 0x12, 0x1,
        0x2, 0x12, 0x2, 0x8, 0x64, 0x12, 0x2, 0x8,
        0x6f, 0x12, 0x2, 0x8, 0x67, 0x12, 0x2, 0x8,
        0x6a, 0x1a, 0x0, 0xa, 0x2b, 0x8, 0x80, 0x85,
        0x80, 0xa1, 0xc3, 0x23, 0x12, 0x0, 0x1a, 0x0,
        0x62, 0x1e, 0x8, 0x14, 0x10, 0xdb, 0x94, 0x9,
        0x18, 0xc0, 0x80, 0x98, 0x1, 0x2a, 0xa, 0x15,
        0x66, 0x66, 0xa6, 0x3f, 0x1d, 0xcd, 0xcc, 0x4c,
        0x3f, 0x32, 0x5, 0x1d, 0x0, 0x0, 0x1e, 0x43,
        0xa, 0x19, 0x8, 0x80, 0x85, 0x88, 0xa1, 0xc3,
        0x23, 0x12, 0xe, 0x12, 0xc, 0x8, 0xd2, 0x86,
        0x3, 0x12, 0x6, 0xa, 0x4, 0x0, 0x64, 0x0,
        0x5, 0x1a, 0x0, 0xa, 0x32, 0x8, 0xc0, 0x80,
        0x1e, 0x12, 0x2a, 0x12, 0x13, 0x8, 0x35, 0x12,
        0xf, 0xd, 0xfa, 0x4b, 0xfd, 0x42, 0x15, 0xdf,
        0xae, 0xb5, 0x41, 0x1d, 0xad, 0x4b, 0xf6, 0x42,
        0x12, 0x13, 0x8, 0x34, 0x12, 0xf, 0xd, 0x2c,
        0x43, 0xfb, 0x42, 0x15, 0xf5, 0xc0, 0xb5, 0x41,
        0x1d, 0x2d, 0x91, 0xf7, 0x42, 0x1a, 0x0, 0xa,
        0x24, 0x8, 0xc0, 0x80, 0x98, 0x1, 0x12, 0x1b,
        0x12, 0x7, 0x8, 0x32, 0x12, 0x3, 0xb5, 0x96,
        0x2, 0x12, 0x7, 0x8, 0x33, 0x12, 0x3, 0xca,
        0x93, 0x2, 0x12, 0x7, 0x8, 0x6e, 0x12, 0x3,
        0xd0, 0x8c, 0x1, 0x1a, 0x0, 0x0, 0x0, 0x0,
        0x28, 0x0, 0x2, 0x0, 0x0, 0x0, 0x0, 0x63,
        0x33, 0x53, 0x42, 0x0, 0x0, 0x0, 0x0, 0x0,
        0x0, 0x0, 0x2e, 0xa, 0x10, 0xa, 0x7, 0x8,
        0x80, 0x85, 0xc8, 0xc3, 0xee, 0x69, 0x28, 0x80,
        0x85, 0xc8, 0xc3, 0xee, 0x69
    ];

    public static readonly byte[] LoginSignatureBytes =
    [
        0xc4, 0x3, 0xa8, 0x5, 0xb2, 0x7f, // Dst MAC
        0xb4, 0xf, 0x3b, 0x1a, 0x26, 0xb0, // Src MAC
        0x8, 0x0, // Type: IPv4
        // IPv4 Header
        0x45, 0x0, 0x0, 0x8a, // Version, IHL, ToS, Total Length (138)
        0xc8, 0xf0, 0x40, 0x0, // Identification, Flags, Fragment Offset
        0x33, 0x6, 0x98, 0x2a, // TTL, Protocol (TCP), Header Checksum
        0x24, 0x98, 0x0, 0x7a, // Src IP: 36.152.0.122
        0xc0, 0xa8, 0x0, 0x99, // Dst IP: 192.168.0.153
        // TCP Header
        0x28, 0xb8, 0x42, 0x3c, // Src Port 10424, Dst Port 16956
        0xd4, 0x9d,
        0x5b, 0xfb, 0x73, 0xec, 0xf8, 0x41, 0x50, 0x18,
        0x0, 0x3f, 0x44, 0x63, 0x0, 0x0, 0x0, 0x0,
        0x0, 0x62, 0x0, 0x3, 0x0, 0x0, 0x0, 0x1,
        0x0, 0x0, 0x0, 0xe8, 0x0, 0x0, 0x0, 0x0,
        0xa, 0x4e, 0x8, 0x1, 0x22, 0x24, 0x66, 0x61,
        0x63, 0x32, 0x64, 0x39, 0x33, 0x63, 0x2d, 0x65,
        0x31, 0x39, 0x32, 0x2d, 0x34, 0x36, 0x35, 0x33,
        0x2d, 0x39, 0x62, 0x35, 0x64, 0x2d, 0x63, 0x63,
        0x35, 0x36, 0x32, 0x38, 0x62, 0x38, 0x34, 0x36,
        0x63, 0x61, 0x2a, 0x24, 0x30, 0x66, 0x33, 0x62,
        0x30, 0x38, 0x34, 0x63, 0x2d, 0x62, 0x35, 0x31,
        0x64, 0x2d, 0x34, 0x66, 0x39, 0x38, 0x2d, 0x39,
        0x64, 0x66, 0x38, 0x2d, 0x37, 0x65, 0x33, 0x64,
        0x39, 0x39, 0x36, 0x32, 0x32, 0x37, 0x36, 0x35
    ];
}