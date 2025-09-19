using Microsoft.Extensions.DependencyInjection;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.Services;

public class WindowManagementService(IServiceProvider provider) : IWindowManagementService
{
    private DpsStatisticsView? _dpsStatisticsView;
    private SettingsView? _settingsView;
    private SkillBreakdownView? _skillBreakDownView;

    public DpsStatisticsView DpsStatisticsView => _dpsStatisticsView ??= CreateDpsStatisticsView();
    public SettingsView SettingsView => _settingsView ??= CreateSettingsView();
    public SkillBreakdownView SkillBreakdownView => _skillBreakDownView ??= CreateSkillBreakDownView();

    private DpsStatisticsView CreateDpsStatisticsView()
    {
        var view = provider.GetRequiredService<DpsStatisticsView>();
        // When the window is closed, clear the cached reference so a new instance will be created next time.
        view.Closed += (_, _) =>
        {
            if (_dpsStatisticsView == view) _dpsStatisticsView = null;
        };
        return view;
    }

    private SettingsView CreateSettingsView()
    {
        var view = provider.GetRequiredService<SettingsView>();
        view.Closed += (_, _) =>
        {
            if (_settingsView == view) _settingsView = null;
        };
        return view;
    }

    private SkillBreakdownView CreateSkillBreakDownView()
    {
        var view = provider.GetRequiredService<SkillBreakdownView>();
        view.Closed += (_, _) =>
        {
            if (_skillBreakDownView == view) _skillBreakDownView = null;
        };
        return view;
    }
}

public interface IWindowManagementService
{
    DpsStatisticsView DpsStatisticsView { get; }
    SettingsView SettingsView { get; }
    SkillBreakdownView SkillBreakdownView { get; }
}

public static class WindowManagementServiceExtensions
{
    public static IServiceCollection AddWindowManagementService(this IServiceCollection services)
    {
        services.AddSingleton<IWindowManagementService, WindowManagementService>();
        return services;
    }
}