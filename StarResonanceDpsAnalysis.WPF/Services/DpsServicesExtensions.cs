using Microsoft.Extensions.DependencyInjection;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Extension methods for registering DPS-related services
/// Follows Dependency Inversion Principle - depend on abstractions
/// </summary>
public static class DpsServicesExtensions
{
    /// <summary>
    /// Register all DPS-related services for dependency injection
    /// </summary>
    public static IServiceCollection AddDpsServices(this IServiceCollection services)
    {
        // Phase 1: Core Services (Timer, Data Processing, Update Coordination)
        services.AddSingleton<IDpsTimerService, DpsTimerService>();
        services.AddSingleton<IDpsDataProcessor, DpsDataProcessor>();
        services.AddSingleton<IDpsUpdateCoordinator, DpsUpdateCoordinator>();
        
        // Phase 2: State Management Services (following Single Responsibility)
        services.AddSingleton<ICombatSectionStateManager, CombatSectionStateManager>();
        services.AddSingleton<ITeamStatsUIManager, TeamStatsUIManager>();

        // Phase 3: Coordination Services
        services.AddSingleton<IResetCoordinator, ResetCoordinator>();

        return services;
    }
}
