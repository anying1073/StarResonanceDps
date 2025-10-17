using System.Collections.Generic;
using System.Threading.Tasks;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services
{
    // Pseudocode plan:
    // - Implement a design-time IDeviceManagementService that returns deterministic, fake data.
    // - Maintain an in-memory list of mock adapters (name, description) for the designer to display.
    // - Keep an in-memory reference to the "active" adapter (NetworkAdapterInfo?) when SetActiveNetworkAdapter is called.
    // - GetNetworkAdaptersAsync: return the mock adapter list.
    // - GetAutoSelectedNetworkAdapterAsync: return the previously "set" active adapter or null.
    // - StopActiveCapture: no-op for design-time.

    internal sealed class DesignTimeDeviceManagementService : IDeviceManagementService
    {
        private readonly List<(string name, string description)> _mockAdapters =
        [
            ("Loopback", "Microsoft KM-TEST Loopback Adapter"),
            ("Ethernet", "Intel(R) Ethernet Connection I219-V"),
            ("Wi-Fi", "Realtek 8822CE Wireless LAN 802.11ac PCI-E NIC")
        ];

        private NetworkAdapterInfo? _activeAdapter;

        public Task<List<(string name, string description)>> GetNetworkAdaptersAsync()
            => Task.FromResult(_mockAdapters);

        public Task<NetworkAdapterInfo?> GetAutoSelectedNetworkAdapterAsync()
            => Task.FromResult(_activeAdapter);

        public void SetActiveNetworkAdapter(NetworkAdapterInfo adapter)
        {
            _activeAdapter = adapter;
        }

        public void StopActiveCapture()
        {
            // Design-time no-op
        }
    }
}