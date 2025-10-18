using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

internal sealed class DesignConfigManager : IConfigManager
{
    public event EventHandler<AppConfig>? ConfigurationUpdated;
    public AppConfig CurrentConfig { get; } = new();

    public Task SaveAsync(AppConfig? newConfig)
    {
        return Task.CompletedTask;
    }
}