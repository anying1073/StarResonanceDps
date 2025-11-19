namespace StarResonanceDpsAnalysis.WPF.Services;

using StarResonanceDpsAnalysis.WPF.Models;

public interface IDeviceManagementService
{
    Task<List<(string name, string description)>> GetNetworkAdaptersAsync();
    Task<NetworkAdapterInfo?> GetAutoSelectedNetworkAdapterAsync();
    void SetActiveNetworkAdapter(NetworkAdapterInfo adapter);
    void StopActiveCapture();

    // New: switch to control whether ProcessPortsWatcher-based port filtering is enabled
    bool UseProcessPortsFilter { get; }
    void SetUseProcessPortsFilter(bool enabled);
}