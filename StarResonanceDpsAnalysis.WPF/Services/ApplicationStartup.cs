using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Logging;

namespace StarResonanceDpsAnalysis.WPF.Services;

public sealed class ApplicationStartup(
    ILogger<ApplicationStartup> logger,
    IConfigManager configManager,
    IDeviceManagementService deviceManagementService,
    IGlobalHotkeyService hotkeyService,
    IPacketAnalyzer packetAnalyzer,
    IDataStorage dataStorage,
    LocalizationManager localization) : IApplicationStartup
{
    public async Task InitializeAsync()
    {
        try
        {
            logger.LogInformation(WpfLogEvents.StartupInit, "Startup initialization started");
            
            // ? Configure time series sample capacity from config
            StatisticsConfiguration.TimeSeriesSampleCapacity = configManager.CurrentConfig.TimeSeriesSampleCapacity;
            logger.LogInformation("Time series sample capacity configured: {Capacity}", 
                StatisticsConfiguration.TimeSeriesSampleCapacity);
            
            // ? Configure sample recording interval from DpsUpdateInterval
            if (dataStorage is DataStorageV2 storageV2)
            {
                storageV2.SampleRecordingInterval = configManager.CurrentConfig.DpsUpdateInterval;
                logger.LogInformation("Sample recording interval configured: {Interval}ms", 
                    storageV2.SampleRecordingInterval);
            }
            
            // Apply localization
            localization.Initialize(configManager.CurrentConfig.Language);

            await TryFindBestNetworkAdapter().ConfigureAwait(false);

            dataStorage.LoadPlayerInfoFromFile();
            // Start analyzer
            packetAnalyzer.Start();
            hotkeyService.Start();
            logger.LogInformation(WpfLogEvents.StartupInit, "Startup initialization completed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup initialization encountered an issue");
            throw;
        }
    }

    private async Task TryFindBestNetworkAdapter()
    {
        // Activate preferred/first network adapter
        var adapters = await deviceManagementService.GetNetworkAdaptersAsync();
        NetworkAdapterInfo? target = null;
        var pref = configManager.CurrentConfig.PreferredNetworkAdapter;
        if (pref != null)
        {
            var match = adapters.FirstOrDefault(a => a.name == pref.Name);
            if (!match.Equals(default((string name, string description))))
            {
                target = new NetworkAdapterInfo(match.name, match.description);
            }
        }

        // If preferred not found, try automatic selection via routing
        if (target == null)
        {
            target = await deviceManagementService.GetAutoSelectedNetworkAdapterAsync();
        }

        target ??= adapters.Count > 0
            ? new NetworkAdapterInfo(adapters[0].name, adapters[0].description)
            : null;

        if (target != null)
        {
            logger.LogInformation(WpfLogEvents.StartupAdapter, "Activating adapter: {Name}", target.Name);
            deviceManagementService.SetActiveNetworkAdapter(target);
            configManager.CurrentConfig.PreferredNetworkAdapter = target;
            _ = configManager.SaveAsync();
        }
        else
        {
            logger.LogWarning(WpfLogEvents.StartupAdapter, "No adapters available for activation");
        }
    }

    public void Shutdown()
    {
        try
        {
            logger.LogInformation(WpfLogEvents.Shutdown, "Application shutdown");
            deviceManagementService.StopActiveCapture();
            packetAnalyzer.Stop();
            hotkeyService.Stop();
            dataStorage.SavePlayerInfoToFile();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shutdown encountered an issue");
        }
    }
}
