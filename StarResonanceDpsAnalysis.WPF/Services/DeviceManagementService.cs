using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

public class DeviceManagementService(
    CaptureDeviceList captureDeviceList,
    IPacketAnalyzer packetAnalyzer,
    ILogger<DeviceManagementService> logger) : IDeviceManagementService
{
    private readonly object _filterSync = new();
    private ILiveDevice? _activeDevice;
    private ProcessPortsWatcher? _portsWatcher;

    // New: switch to enable/disable dynamic port-based filtering
    // When false, we will set a simple filter: "ip and tcp"
    public bool UseProcessPortsFilter { get; private set; } = true;

    public async Task<List<(string name, string description)>> GetNetworkAdaptersAsync()
    {
        return await Task.FromResult(captureDeviceList.Select(device => (device.Name, device.Description)).ToList());
    }

    /// <summary>
    /// Toggle port-based filtering at runtime. If capture is active, the device filter and watcher are reconfigured accordingly.
    /// </summary>
    public void SetUseProcessPortsFilter(bool enabled)
    {
        if (UseProcessPortsFilter == enabled) return;
        UseProcessPortsFilter = enabled;

        // Reconfigure current capture if any
        if (_activeDevice == null)
            return;

        if (!UseProcessPortsFilter)
        {
            // Dispose watcher and set a broad filter
            if (_portsWatcher != null)
            {
                _portsWatcher.PortsChanged -= PortsWatcherOnPortsChanged;
                _portsWatcher.Dispose();
                _portsWatcher = null;
            }

            TrySetDeviceFilter("ip and tcp");
        }
        else
        {
            // Create and start watcher, then apply ports filter snapshot
            if (_portsWatcher == null)
            {
                _portsWatcher = new ProcessPortsWatcher(["star.exe", "BPSR_STEAM.exe", "BPSR_EPIC.exe", "BPSR.exe"]);
                _portsWatcher.PortsChanged += PortsWatcherOnPortsChanged;
                _portsWatcher.Start();
            }

            ApplyProcessPortsFilter(_portsWatcher.TcpPorts, _portsWatcher.UdpPorts);
        }
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
                if (captureDeviceList[i].Description
                    .Contains(ni.Description, StringComparison.OrdinalIgnoreCase)) score += 3;
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

        // Only create watcher when using port-based filtering
        if (UseProcessPortsFilter)
        {
            _portsWatcher = new ProcessPortsWatcher(["star.exe", "BPSR_STEAM.exe", "BPSR_EPIC.exe", "BPSR.exe"]);
            _portsWatcher.PortsChanged += PortsWatcherOnPortsChanged;
        }

        var device = captureDeviceList.FirstOrDefault(d => d.Name == adapter.Name);
        Debug.Assert(device != null, "Selected device not found by name");

        device.Open(new DeviceConfiguration
        {
            Mode = DeviceModes.Promiscuous,
            Immediate = true,
            ReadTimeout = 1000,
            BufferSize = 1024 * 1024 * 4
        });

        if (UseProcessPortsFilter)
        {
            // Start with no traffic until ports are known (use a filter that never matches)
            TrySetDeviceFilter(BuildFilter(Array.Empty<int>(), Array.Empty<int>()));
        }
        else
        {
            // Broad filter: capture IPv4 TCP only
            TrySetDeviceFilter("ip and tcp");
        }

        device.OnPacketArrival += OnPacketArrival;
        device.StartCapture();
        _activeDevice = device;

        if (UseProcessPortsFilter && _portsWatcher != null)
        {
            // Start the watcher after capture is active to avoid missing early events
            _portsWatcher.Start();
            // Immediately apply current snapshot (if any)
            ApplyProcessPortsFilter(_portsWatcher.TcpPorts, _portsWatcher.UdpPorts);
        }

        packetAnalyzer.Start();
        logger.LogInformation(WpfLogEvents.DeviceSwitched, "Active capture device switched to: {Name}", adapter.Name);
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
        if (!UseProcessPortsFilter) return;
        logger.LogDebug(WpfLogEvents.PortsChanged, "Process ports changed: TCP={TcpCount}, UDP={UdpCount}", e.TcpPorts.Count, e.UdpPorts.Count);
        ApplyProcessPortsFilter(e.TcpPorts, e.UdpPorts);
    }

    private void ApplyProcessPortsFilter(IReadOnlyCollection<int> tcpPorts, IReadOnlyCollection<int> udpPorts)
    {
        if (!UseProcessPortsFilter)
        {
            TrySetDeviceFilter("ip and tcp");
            return;
        }

        var filter = BuildFilter(tcpPorts, udpPorts);
        TrySetDeviceFilter(filter);
    }

    private string BuildFilter(IReadOnlyCollection<int> tcpPorts, IReadOnlyCollection<int> udpPorts)
    {
        if (!UseProcessPortsFilter)
        {
            // Simple BPF when not filtering by ports
            return "ip and tcp";
        }

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
        {
            // No known process ports -> match nothing to avoid capturing unrelated traffic
            // Using "port 0" is a practical way to yield no matches for TCP/UDP
            return "(ip or ip6) and (port 0)";
        }

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
                logger.LogDebug(WpfLogEvents.CaptureFilterUpdated, "Capture filter updated: {Filter}", filter);
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