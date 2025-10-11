using System.Collections.Concurrent;
using System.Reflection;
using StarResonanceDpsAnalysis.Core.Analyze;

namespace StarResonanceDpsAnalysis.Core.Tests;

public class PacketAnalyzerTests
{
    [Fact]
    public void ResetCaptureState_ClearsCachesAndServer()
    {
        var analyzer = new PacketAnalyzer();
        analyzer.CurrentServer = "1.1.1.1:1000 -> 2.2.2.2:2000";

        var tcpCache = GetFieldValue<ConcurrentDictionary<uint, byte[]>>(analyzer, "TcpCache");
        var tcpCacheTime = GetFieldValue<ConcurrentDictionary<uint, DateTime>>(analyzer, "TcpCacheTime");
        var tcpStream = GetFieldValue<MemoryStream>(analyzer, "TcpStream");

        tcpCache[1] = new byte[] { 0x01 };
        tcpCacheTime[1] = DateTime.UtcNow;
        tcpStream.Write(new byte[] { 0xAA, 0xBB }, 0, 2);

        analyzer.ResetCaptureState();

        Assert.Equal(string.Empty, analyzer.CurrentServer);
        Assert.Empty(tcpCache);
        Assert.Empty(tcpCacheTime);
        Assert.Equal(0, tcpStream.Length);
    }

    private static T GetFieldValue<T>(object instance, string name)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var property = instance.GetType().GetProperty(name, flags);
        if (property != null)
        {
            return (T)property.GetValue(instance)!;
        }

        var field = instance.GetType().GetField(name, flags);
        return (T)field!.GetValue(instance)!;
    }
}