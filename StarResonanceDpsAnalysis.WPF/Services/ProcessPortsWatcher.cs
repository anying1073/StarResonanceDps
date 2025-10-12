using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Periodically discovers local TCP/UDP ports owned by a target process name.
/// IPv4 only for now.
/// </summary>
internal sealed class ProcessPortsWatcher : IDisposable
{
    private const uint NO_ERROR = 0;
    private const uint ERROR_INSUFFICIENT_BUFFER = 122;
    private readonly string _processName;
    private readonly TimeSpan _refreshInterval;
    private volatile HashSet<int> _tcpPorts = [];
    private Timer? _timer;
    private volatile HashSet<int> _udpPorts = [];

    public ProcessPortsWatcher(string processName, TimeSpan? refreshInterval = null)
    {
        _processName = NormalizeProcessName(processName);
        _refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(2);
    }

    public IReadOnlyCollection<int> TcpPorts => _tcpPorts;
    public IReadOnlyCollection<int> UdpPorts => _udpPorts;

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public event EventHandler<PortsChangedEventArgs>? PortsChanged;

    public void Start()
    {
        Refresh(null);
        _timer = new Timer(Refresh, null, _refreshInterval, _refreshInterval);
    }

    public bool ContainsTcpPort(int port)
    {
        return _tcpPorts.Contains(port);
    }

    public bool ContainsUdpPort(int port)
    {
        return _udpPorts.Contains(port);
    }

    private void Refresh(object? state)
    {
        try
        {
            var pids = GetTargetProcessPids();
            if (pids.Count == 0)
            {
                PublishIfChanged([], []);
                return;
            }

            var tcpPorts = new HashSet<int>();
            var udpPorts = new HashSet<int>();

            foreach (var row in Tcp4Rows())
            {
                if (pids.Contains((int)row.owningPid))
                {
                    var port = GetPort(row.localPort);
                    if (port != 0)
                        tcpPorts.Add(port);
                }
            }

            foreach (var row in Udp4Rows())
            {
                if (pids.Contains((int)row.owningPid))
                {
                    var port = GetPort(row.localPort);
                    if (port != 0)
                        udpPorts.Add(port);
                }
            }

            PublishIfChanged(tcpPorts, udpPorts);
        }
        catch (Exception)
        {
            // ignore transient failures
        }
    }

    private void PublishIfChanged(HashSet<int> newTcp, HashSet<int> newUdp)
    {
        var oldTcp = _tcpPorts;
        var oldUdp = _udpPorts;
        var changed = !oldTcp.SetEquals(newTcp) || !oldUdp.SetEquals(newUdp);
        if (!changed) return;

        _tcpPorts = newTcp;
        _udpPorts = newUdp;

        try
        {
            PortsChanged?.Invoke(this, new PortsChangedEventArgs(newTcp, newUdp));
        }
        catch
        {
            // ignore subscriber errors
        }
    }

    private HashSet<int> GetTargetProcessPids()
    {
        try
        {
            var processes = Process.GetProcessesByName(_processName);
            return processes.Select(p => p.Id).ToHashSet();
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeProcessName(string name)
    {
        var n = name.Trim();
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            n = n[..^4];
        return n;
    }

    // Windows IP Helper interop (IPv4 only)

    private static IEnumerable<MIB_TCPROW_OWNER_PID> Tcp4Rows()
    {
        var buffSize = 0;
        var ret = GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, (int)AddressFamily.AF_INET,
            TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
        if (ret != ERROR_INSUFFICIENT_BUFFER)
            ThrowIfError(ret, "GetExtendedTcpTable(size)");

        var pTable = Marshal.AllocHGlobal(buffSize);
        try
        {
            ret = GetExtendedTcpTable(pTable, ref buffSize, true, (int)AddressFamily.AF_INET,
                TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
            ThrowIfError(ret, "GetExtendedTcpTable");

            var numEntries = Marshal.ReadInt32(pTable);
            var rowPtr = IntPtr.Add(pTable, sizeof(uint));
            var rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

            for (var i = 0; i < numEntries; i++)
            {
                yield return Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pTable);
        }
    }

    private static IEnumerable<MIB_UDPROW_OWNER_PID> Udp4Rows()
    {
        var buffSize = 0;
        var ret = GetExtendedUdpTable(IntPtr.Zero, ref buffSize, true, (int)AddressFamily.AF_INET,
            UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
        if (ret != ERROR_INSUFFICIENT_BUFFER)
            ThrowIfError(ret, "GetExtendedUdpTable(size)");

        var pTable = Marshal.AllocHGlobal(buffSize);
        try
        {
            ret = GetExtendedUdpTable(pTable, ref buffSize, true, (int)AddressFamily.AF_INET,
                UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
            ThrowIfError(ret, "GetExtendedUdpTable");

            var numEntries = Marshal.ReadInt32(pTable);
            var rowPtr = IntPtr.Add(pTable, sizeof(uint));
            var rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

            for (var i = 0; i < numEntries; i++)
            {
                yield return Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pTable);
        }
    }

    private static int GetPort(uint dwPort)
    {
        // dwPort is network byte order stored in the LOW-ORDER 16 bits of the DWORD on little-endian systems
        var netOrder = (ushort)(dwPort & 0xFFFF);
        return (ushort)IPAddress.NetworkToHostOrder((short)netOrder);
    }

    private static void ThrowIfError(uint errorCode, string api)
    {
        if (errorCode == NO_ERROR) return;
        throw new Win32Exception((int)errorCode, $"{api} failed with {errorCode}");
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion,
        TCP_TABLE_CLASS tblClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int dwOutBufLen, bool sort, int ipVersion,
        UDP_TABLE_CLASS tblClass, uint reserved);

    private enum AddressFamily
    {
        AF_INET = 2
    }

    private enum TCP_TABLE_CLASS
    {
        TCP_TABLE_BASIC_LISTENER,
        TCP_TABLE_BASIC_CONNECTIONS,
        TCP_TABLE_BASIC_ALL,
        TCP_TABLE_OWNER_PID_LISTENER,
        TCP_TABLE_OWNER_PID_CONNECTIONS,
        TCP_TABLE_OWNER_PID_ALL,
        TCP_TABLE_OWNER_MODULE_LISTENER,
        TCP_TABLE_OWNER_MODULE_CONNECTIONS,
        TCP_TABLE_OWNER_MODULE_ALL
    }

    private enum UDP_TABLE_CLASS
    {
        UDP_TABLE_BASIC,
        UDP_TABLE_OWNER_PID,
        UDP_TABLE_OWNER_MODULE
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID
    {
        public uint localAddr;
        public uint localPort;
        public uint owningPid;
    }
}

internal sealed class PortsChangedEventArgs : EventArgs
{
    public PortsChangedEventArgs(IReadOnlyCollection<int> tcpPorts, IReadOnlyCollection<int> udpPorts)
    {
        TcpPorts = tcpPorts;
        UdpPorts = udpPorts;
    }

    public IReadOnlyCollection<int> TcpPorts { get; }
    public IReadOnlyCollection<int> UdpPorts { get; }
}