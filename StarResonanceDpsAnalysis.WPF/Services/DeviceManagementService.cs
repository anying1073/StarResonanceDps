using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.WPF.Models;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StarResonanceDpsAnalysis.WPF.Services;

public class DeviceManagementService(
    CaptureDeviceList captureDeviceList,
    IPacketAnalyzer packetAnalyzer,
    ILogger<DeviceManagementService> logger) : IDeviceManagementService
{
    private ILiveDevice? _activeDevice;
    private ProcessPortsWatcher? _portsWatcher;
    private readonly object _filterSync = new();

    public async Task<List<(string name, string description)>> GetNetworkAdaptersAsync()
    {
        return await Task.FromResult(captureDeviceList.Select(device => (device.Name, device.Description)).ToList());
    }

    /// <summary>
    /// Attempts to auto-select the best network adapter by consulting the routing table (GetBestInterface)
    /// and mapping the resulting interface index to a SharpPcap device. Returns null if no match.
    /// </summary>
    public Task<NetworkAdapterInfo?> GetAutoSelectedNetworkAdapterAsync()
    {
        try
        {
            var routeIndex = GetBestInterfaceForExternalDestination();
            if (routeIndex == null) return Task.FromResult<NetworkAdapterInfo?>(null);

            var ni = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                {
                    try
                    {
                        var props = n.GetIPProperties();
                        var ipv4 = props.GetIPv4Properties();
                        return ipv4 != null && ipv4.Index == routeIndex.Value;
                    }
                    catch
                    {
                        return false;
                    }
                });

            if (ni == null) return Task.FromResult<NetworkAdapterInfo?>(null);

            // Find best matching capture device by description/name
            int bestIndex = -1, bestScore = -1;
            for (var i = 0; i < captureDeviceList.Count; i++)
            {
                var score = 0;
                if (captureDeviceList[i].Description.Contains(ni.Name, StringComparison.OrdinalIgnoreCase)) score += 2;
                if (captureDeviceList[i].Description.Contains(ni.Description, StringComparison.OrdinalIgnoreCase)) score += 3;
                if (score <= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }

            if (bestIndex >= 0)
            {
                var d = captureDeviceList[bestIndex];
                return Task.FromResult<NetworkAdapterInfo?>(new NetworkAdapterInfo(d.Name, d.Description));
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Auto select network adapter failed");
        }

        return Task.FromResult<NetworkAdapterInfo?>(null);
    }

    public void SetActiveNetworkAdapter(NetworkAdapterInfo adapter)
    {
        packetAnalyzer.ResetCaptureState();
        packetAnalyzer.Stop();

        if (_activeDevice != null)
        {
            try
            {
                _activeDevice.OnPacketArrival -= OnPacketArrival;
                _activeDevice.StopCapture();
                _activeDevice.Close();
            }
            catch
            {
#if DEBUG
                throw;
#endif
            }
            finally
            {
                _activeDevice = null;
            }
        }

        if (_portsWatcher != null)
        {
            _portsWatcher.PortsChanged -= PortsWatcherOnPortsChanged;
            _portsWatcher.Dispose();
            _portsWatcher = null;
        }

        _portsWatcher = new ProcessPortsWatcher("star.exe");
        _portsWatcher.PortsChanged += PortsWatcherOnPortsChanged;

        var device = captureDeviceList.FirstOrDefault(d => d.Name == adapter.Name);
        Debug.Assert(device != null, "Selected device not found by name");

        device.Open(new DeviceConfiguration
        {
            Mode = DeviceModes.Promiscuous,
            Immediate = true,
            ReadTimeout = 1000,
            BufferSize = 1024 * 1024 * 4
        });

        // Start with no traffic until ports are known
        TrySetDeviceFilter("");

        device.OnPacketArrival += OnPacketArrival;
        device.StartCapture();
        _activeDevice = device;

        // Start the watcher after capture is active to avoid missing early events
        _portsWatcher.Start();
        // Immediately apply current snapshot (if any)
        ApplyProcessPortsFilter(_portsWatcher.TcpPorts, _portsWatcher.UdpPorts);

        packetAnalyzer.Start();
        logger.LogInformation("Active capture device switched to: {Name}", adapter.Name);
    }

    public void StopActiveCapture()
    {
        packetAnalyzer.Stop();
        if (_activeDevice == null)
        {
            _portsWatcher?.Dispose();
            _portsWatcher = null;
            return;
        }
        try
        {
            _activeDevice.OnPacketArrival -= OnPacketArrival;
            _activeDevice.StopCapture();
            _activeDevice.Close();
        }
        finally
        {
            _activeDevice = null;
            if (_portsWatcher != null)
            {
                _portsWatcher.PortsChanged -= PortsWatcherOnPortsChanged;
                _portsWatcher.Dispose();
                _portsWatcher = null;
            }
        }
    }

    private void PortsWatcherOnPortsChanged(object? sender, PortsChangedEventArgs e)
    {
        ApplyProcessPortsFilter(e.TcpPorts, e.UdpPorts);
    }

    private void ApplyProcessPortsFilter(IReadOnlyCollection<int> tcpPorts, IReadOnlyCollection<int> udpPorts)
    {
        string filter = BuildFilter(tcpPorts, udpPorts);
        TrySetDeviceFilter(filter);
    }

    private string BuildFilter(IReadOnlyCollection<int> tcpPorts, IReadOnlyCollection<int> udpPorts)
    {
        // Build BPF like: (ip or ip6) and ((tcp and (port a or port b)) or (udp and (port c or port d)))
        var parts = new List<string>();
        if (tcpPorts.Count > 0)
        {
            parts.Add($"(tcp and (port {string.Join(" or port ", tcpPorts)}))");
        }
        if (udpPorts.Count > 0)
        {
            parts.Add($"(udp and (port {string.Join(" or port ", udpPorts)}))");
        }

        if (parts.Count == 0)
            return ""; // match nothing until we know ports

        return $"(ip or ip6) and ({string.Join(" or ", parts)})";
    }

    private void TrySetDeviceFilter(string filter)
    {
        var dev = _activeDevice;
        if (dev == null) return;

        lock (_filterSync)
        {
            try
            {
                dev.Filter = filter;
                logger.LogDebug("Updated capture filter: {Filter}", filter);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to set capture filter: {Filter}", filter);
#if DEBUG
                throw;
#endif
            }
        }
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            var raw = e.GetPacket();
            var ret = packetAnalyzer.TryEnlistData(raw);
            if (!ret)
            {
                logger.LogWarning("Packet enlist failed from device {Device} with Packet {p}", sender, raw.ToString());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Packet enlist failed from device {Device}", sender);
#if DEBUG
            throw;
#endif
        }
    }

    // PInvoke to call GetBestInterface from iphlpapi.dll
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);

    private int? GetBestInterfaceForExternalDestination()
    {
        try
        {
            var dest = IPAddress.Parse("8.8.8.8");
            // Convert IP address from host byte order to the format expected by GetBestInterface (network byte order)
            var bytes = dest.GetAddressBytes();
            var addr = BitConverter.ToUInt32(bytes, 0);

            if (GetBestInterface(addr, out var index) == 0)
            {
                return (int)index;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "GetBestInterfaceForExternalDestination failed");
        }

        return null;
    }
}