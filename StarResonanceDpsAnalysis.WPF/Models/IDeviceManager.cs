using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpPcap;

namespace StarResonanceDpsAnalysis.WPF.Models
{
    public interface IDeviceManager
    {
        Task<List<(string name, string description)>> GetNetworkAdaptersAsync();
    }

    public class DeviceManager(CaptureDeviceList captureDeviceList) : IDeviceManager
    {
        public async Task<List<(string name, string description)>> GetNetworkAdaptersAsync()
        {
            return await Task.FromResult(captureDeviceList.Select(device => (device.Name, device.Description)).ToList());
        }
    }
}
