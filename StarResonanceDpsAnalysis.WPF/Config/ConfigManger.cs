using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace StarResonanceDpsAnalysis.WPF.Config;

public class ConfigManger : IConfigManager
{
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IOptionsMonitor<AppConfig> _optionsMonitor;

    public ConfigManger(IOptionsMonitor<AppConfig> optionsMonitor,
        IOptions<JsonSerializerOptions> jsonOptions)
    {
        _optionsMonitor = optionsMonitor;
        _jsonOptions = jsonOptions.Value;
        _configFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        // Subscribe to configuration changes
        _optionsMonitor.OnChange(OnConfigurationChanged);
    }

    public async Task SaveAsync(AppConfig? newConfig = null)
    {
        try
        {
            // Read the current appsettings.json
            var jsonContent = await File.ReadAllTextAsync(_configFilePath);
            // var jsonDoc = JsonDocument.Parse(jsonContent);
            var rootDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent, _jsonOptions) ??
                           new Dictionary<string, object>();

            // Update the Config section
            newConfig ??= CurrentConfig;
            rootDict["Config"] = newConfig;

            // Write back to file using the configured options
            var updatedJson = JsonSerializer.Serialize(rootDict, _jsonOptions);
            await File.WriteAllTextAsync(_configFilePath, updatedJson);

            // Force configuration reload (the file watcher should pick this up automatically)
            // But we can also manually notify if needed
            OnConfigurationChanged(newConfig);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update configuration: {ex.Message}", ex);
        }
    }

    public event EventHandler<AppConfig>? ConfigurationUpdated;

    public AppConfig CurrentConfig => _optionsMonitor.CurrentValue;

    private void OnConfigurationChanged(AppConfig newConfig)
    {
        ConfigurationUpdated?.Invoke(this, newConfig);
    }
}