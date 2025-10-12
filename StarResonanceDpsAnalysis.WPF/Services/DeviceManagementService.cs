using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.WPF.Models;

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
        TrySetDeviceFilter("false");

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
            return "false"; // match nothing until we know ports

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
}