using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

public sealed class ApplicationStartup(
    ILogger<ApplicationStartup> logger,
    IOptions<AppConfig> options,
    IDeviceManagementService deviceManagementService,
    IPacketAnalyzer packetAnalyzer) : IApplicationStartup
{
    public void Initialize()
    {
        try
        {
            // Apply localization
            LocalizationManager.Initialize(options.Value.Language);

            // Activate preferred/first network adapter
            var adapters = deviceManagementService.GetNetworkAdaptersAsync().GetAwaiter().GetResult();
            NetworkAdapterInfo? target = null;
            var pref = options.Value.PreferredNetworkAdapter;
            if (pref != null)
            {
                var match = adapters.FirstOrDefault(a => a.name == pref.Name);
                if (!match.Equals(default((string name, string description))))
                {
                    target = new NetworkAdapterInfo(match.name, match.description);
                }
            }

            target ??= adapters.Count > 0
                ? new NetworkAdapterInfo(adapters[0].name, adapters[0].description)
                : null;

            if (target != null)
            {
                deviceManagementService.SetActiveNetworkAdapter(target);
            }

            // Start analyzer
            packetAnalyzer.Start();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup initialization encountered an issue");
        }
    }

    public void Shutdown()
    {
        try
        {
            deviceManagementService.StopActiveCapture();
            packetAnalyzer.Stop();
        }
        catch (Exception)
        {
            // ignore
        }
    }
}
