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

public sealed class ApplicationStartup : IApplicationStartup
{
    private readonly ILogger<ApplicationStartup> _logger;
    private readonly IConfigManager _configManager;
    private readonly IDeviceManagementService _deviceManagementService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IPacketAnalyzer _packetAnalyzer;
    private readonly IDataStorage _dataStorage;
    private readonly IClassColorService _classColorService;
    private readonly LocalizationManager _localization;
    private AppConfig _appConfig;

    public ApplicationStartup(ILogger<ApplicationStartup> logger,
        IConfigManager configManager,
        IDeviceManagementService deviceManagementService,
        IGlobalHotkeyService hotkeyService,
        IPacketAnalyzer packetAnalyzer,
        IDataStorage dataStorage,
        IClassColorService classColorService,
        LocalizationManager localization)
    {
        _logger = logger;
        _configManager = configManager;
        _deviceManagementService = deviceManagementService;
        _hotkeyService = hotkeyService;
        _packetAnalyzer = packetAnalyzer;
        _dataStorage = dataStorage;
        _classColorService = classColorService;
        _localization = localization;
        _configManager.ConfigurationUpdated += ConfigManagerOnConfigurationUpdated;
        _appConfig = _configManager.CurrentConfig;
        _appConfig.MouseThroughEnabled = false;
        MessageAnalyzer.Logger = logger;
        ConfigManagerOnConfigurationUpdated(_configManager, _configManager.CurrentConfig);
    }

    private void ConfigManagerOnConfigurationUpdated(object? sender, AppConfig newConfig)
    {
        _appConfig = newConfig;
        ConfigDeviceManagementService();
    }

    private void ConfigDeviceManagementService()
    {
        _deviceManagementService.SetUseProcessPortsFilter(_appConfig.UseProcessPortsFilter);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation(WpfLogEvents.StartupInit, "Startup initialization started");

            // ? Configure time series sample capacity from config
            StatisticsConfiguration.TimeSeriesSampleCapacity = _configManager.CurrentConfig.TimeSeriesSampleCapacity;
            _logger.LogInformation("Time series sample capacity configured: {Capacity}",
                StatisticsConfiguration.TimeSeriesSampleCapacity);

            // ? Configure sample recording interval from DpsUpdateInterval
            _dataStorage.SampleRecordingInterval = _configManager.CurrentConfig.DpsUpdateInterval;
            _logger.LogInformation("Sample recording interval configured: {Interval}ms",
                _dataStorage.SampleRecordingInterval);

            // Apply localization
            _localization.Initialize(_configManager.CurrentConfig.Language);
            _classColorService.Init();

            await TryFindBestNetworkAdapter().ConfigureAwait(false);

            _dataStorage.LoadPlayerInfoFromFile();
            // Start analyzer
            _packetAnalyzer.Start();
            _hotkeyService.Start();
            _logger.LogInformation(WpfLogEvents.StartupInit, "Startup initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup initialization encountered an issue");
            throw;
        }
    }

    private async Task TryFindBestNetworkAdapter()
    {
        // Activate preferred/first network adapter
        var adapters = await _deviceManagementService.GetNetworkAdaptersAsync();
        NetworkAdapterInfo? target = null;
        var pref = _configManager.CurrentConfig.PreferredNetworkAdapter;
        if (pref != null)
        {
            var match = adapters.FirstOrDefault(a => a.name == pref.Name);
            if (!match.Equals(default((string name, string description))))
            {
                target = new NetworkAdapterInfo(match.name, match.description);
            }
        }

        // If preferred not found, try automatic selection via routing
        target ??= await _deviceManagementService.GetAutoSelectedNetworkAdapterAsync();

        //target ??= adapters.Count > 0
        //    ? new NetworkAdapterInfo(adapters[0].name, adapters[0].description)
        //    : null;

        if (target != null)
        {
            _logger.LogInformation(WpfLogEvents.StartupAdapter, "Activating adapter: {Name}", target.Name);
            _deviceManagementService.SetActiveNetworkAdapter(target);
            _configManager.CurrentConfig.PreferredNetworkAdapter = target;
            //await _configManager.SaveAsync().ConfigureAwait(false);
            _ = _configManager.SaveAsync();
        }
        else
        {
            _logger.LogWarning(WpfLogEvents.StartupAdapter, "No adapters available for activation");
        }
    }

    public void Shutdown()
    {
        try
        {
            _logger.LogInformation(WpfLogEvents.Shutdown, "Application shutdown");
            _deviceManagementService.StopActiveCapture();
            _packetAnalyzer.Stop();
            _hotkeyService.Stop();
            _dataStorage.SavePlayerInfoToFile();
            _configManager.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shutdown encountered an issue");
        }
    }
}

