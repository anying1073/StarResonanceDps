using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Coordinates reset operations across multiple services
/// Encapsulates all reset logic following Single Responsibility Principle
/// </summary>
public class ResetCoordinator : IResetCoordinator
{
    private readonly IDataStorage _storage;
    private readonly IDpsTimerService _timerService;
    private readonly ITeamStatsUIManager _teamStatsManager;
    private readonly BattleHistoryService _historyService;
    private readonly ILogger<ResetCoordinator> _logger;

    public ResetCoordinator(IDataStorage storage,
        IDpsTimerService timerService,
        ITeamStatsUIManager teamStatsManager,
        BattleHistoryService historyService,
        ILogger<ResetCoordinator> logger)
    {
        _storage = storage;
        _timerService = timerService;
        _teamStatsManager = teamStatsManager;
        _historyService = historyService;
        _logger = logger;
    }

    public void ResetCurrentSection()
    {
        _logger.LogInformation("=== ResetCurrentSection START ===");

        // Clear current section data
        _storage.ClearDpsData();

        // Start new section in timer service
        _timerService.StartNewSection();

        // Reset team stats
        _teamStatsManager.ResetTeamStats();

        _logger.LogInformation("=== ResetCurrentSection COMPLETE ===");
    }
    
    public void ResetAll()
    {
        _logger.LogInformation("=== ResetAll START ===");

        // Clear all data (both total and section)
        _storage.ClearAllDpsData();

        // Reset timer service
        _timerService.Reset();
        _logger.LogInformation("Timer service reset");

        // Reset team stats
        _teamStatsManager.ResetTeamStats();

        _logger.LogInformation("=== ResetAll COMPLETE ===");
    }

    public void Reset(ScopeTime scope)
    {
        _logger.LogInformation("Reset requested for scope: {Scope}", scope);

        if (scope == ScopeTime.Current)
        {
            ResetCurrentSection();
        }
        else
        {
            ResetAll();
        }
    }

    public void ResetWithHistory(ScopeTime scope, bool saveHistory, TimeSpan battleDuration, int minimalDuration)
    {
        _logger.LogInformation("=== ResetWithHistory START === Scope={Scope}, SaveHistory={Save}", scope, saveHistory);

        // Save History before reset if requested and data exists
        if (saveHistory && _storage.HasData())
        {
            try
            {
                if (scope == ScopeTime.Current)
                {
                    _historyService.SaveCurrentHistory(_storage, battleDuration, minimalDuration);
                    _logger.LogInformation("Current History saved successfully");
                }
                else
                {
                    _historyService.SaveTotalHistory(_storage, battleDuration, minimalDuration);
                    _logger.LogInformation("Total History saved successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save History before reset");
            }
        }

        // Perform the reset
        Reset(scope);

        _logger.LogInformation("=== ResetWithHistory COMPLETE ===");
    }
}
