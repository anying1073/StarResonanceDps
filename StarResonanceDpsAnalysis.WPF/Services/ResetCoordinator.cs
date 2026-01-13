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
    private readonly ICombatSectionStateManager _combatState;
    private readonly ITeamStatsUIManager _teamStatsManager;
    private readonly BattleSnapshotService _snapshotService;
    private readonly ILogger<ResetCoordinator> _logger;

    public ResetCoordinator(
        IDataStorage storage,
        IDpsTimerService timerService,
        ICombatSectionStateManager combatState,
        ITeamStatsUIManager teamStatsManager,
        BattleSnapshotService snapshotService,
        ILogger<ResetCoordinator> logger)
    {
        _storage = storage;
        _timerService = timerService;
        _combatState = combatState;
        _teamStatsManager = teamStatsManager;
        _snapshotService = snapshotService;
        _logger = logger;
    }

    public void ResetCurrentSection()
    {
        _logger.LogInformation("=== ResetCurrentSection START ===");

        // Clear current section data
        _storage.ClearDpsData();

        // Start new section in timer service
        _timerService.StartNewSection();

        // Reset section state
        _combatState.ResetSectionState();

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

        // Reset all state
        _combatState.ResetAllState();

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

    public void ResetWithSnapshot(ScopeTime scope, bool saveSnapshot, TimeSpan battleDuration, int minimalDuration)
    {
        _logger.LogInformation("=== ResetWithSnapshot START === Scope={Scope}, SaveSnapshot={Save}", scope, saveSnapshot);

        // Save snapshot before reset if requested and data exists
        if (saveSnapshot && _storage.HasData())
        {
            try
            {
                if (scope == ScopeTime.Current)
                {
                    _snapshotService.SaveCurrentSnapshot(_storage, battleDuration, minimalDuration);
                    _logger.LogInformation("Current snapshot saved successfully");
                    _combatState.SkipNextSnapshotSave = true;
                }
                else
                {
                    _snapshotService.SaveTotalSnapshot(_storage, battleDuration, minimalDuration);
                    _logger.LogInformation("Total snapshot saved successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save snapshot before reset");
            }
        }

        // Perform the reset
        Reset(scope);

        _logger.LogInformation("=== ResetWithSnapshot COMPLETE ===");
    }
}
