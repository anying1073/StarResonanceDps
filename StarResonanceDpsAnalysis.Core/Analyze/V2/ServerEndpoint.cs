using System.Net;
using System.Runtime.CompilerServices;
using PacketDotNet;

namespace StarResonanceDpsAnalysis.Core.Analyze;

/// <summary>
/// Represents a TCP server endpoint (IP:Port -> IP:Port)
/// </summary>
public readonly struct ServerEndpoint(IPAddress? sourceIp, ushort sourcePort, IPAddress? destIp, ushort destPort)
    : IEquatable<ServerEndpoint>
{
    public readonly IPAddress? SourceIp = sourceIp;
    public readonly ushort SourcePort = sourcePort;
    public readonly IPAddress? DestinationIp = destIp;
    public readonly ushort DestinationPort = destPort;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ServerEndpoint FromPacket(IPv4Packet ipv4, TcpPacket tcp)
    {
        return new ServerEndpoint(
            ipv4.SourceAddress,
            tcp.SourcePort,
            ipv4.DestinationAddress,
            tcp.DestinationPort
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ServerEndpoint Reverse()
    {
        return new ServerEndpoint(DestinationIp, DestinationPort, SourceIp, SourcePort);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty()
    {
        return SourceIp == null || DestinationIp == null;
    }

    public bool Equals(ServerEndpoint other)
    {
        return SourcePort == other.SourcePort &&
               DestinationPort == other.DestinationPort &&
               Equals(SourceIp, other.SourceIp) &&
               Equals(DestinationIp, other.DestinationIp);
    }

    public override bool Equals(object? obj)
    {
        return obj is ServerEndpoint other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SourceIp, SourcePort, DestinationIp, DestinationPort);
    }

    public override string ToString()
    {
        return $"{SourceIp}:{SourcePort} -> {DestinationIp}:{DestinationPort}";
    }

    public static bool operator ==(ServerEndpoint left, ServerEndpoint right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ServerEndpoint left, ServerEndpoint right)
    {
        return !left.Equals(right);
    }
}