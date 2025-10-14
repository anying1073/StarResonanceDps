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
    IGlobalHotkeyService hotkeyService,
    IPacketAnalyzer packetAnalyzer) : IApplicationStartup
{
    public async Task InitializeAsync()
    {
        try
        {
            // Apply localization
            LocalizationManager.Initialize(options.Value.Language);

            // Activate preferred/first network adapter
            var adapters = await deviceManagementService.GetNetworkAdaptersAsync();
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
            hotkeyService.Start();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup initialization encountered an issue");
            throw;
        }
    }

    public void Shutdown()
    {
        try
        {
            deviceManagementService.StopActiveCapture();
            packetAnalyzer.Stop();
            hotkeyService.Stop();
        }
        catch (Exception)
        {
            // ignore
        }
    }
}
