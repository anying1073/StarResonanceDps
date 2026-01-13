using Microsoft.Extensions.Logging;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Implementation of combat section state management
/// Encapsulates all section state logic (timing now delegated to IDpsTimerService)
/// </summary>
public class CombatSectionStateManager : ICombatSectionStateManager
{
    private readonly ILogger<CombatSectionStateManager> _logger;

    public CombatSectionStateManager(ILogger<CombatSectionStateManager> logger)
    {
        _logger = logger;
    }

    public bool AwaitingSectionStart { get; set; }
    public bool SectionTimedOut { get; set; }
    public TimeSpan LastSectionElapsed { get; set; } = TimeSpan.Zero;
    public TimeSpan TotalCombatDuration { get; set; } = TimeSpan.Zero;
    public bool SkipNextSnapshotSave { get; set; }

    public void ResetSectionState()
    {
        LastSectionElapsed = TimeSpan.Zero;
        AwaitingSectionStart = false;
        SectionTimedOut = false;
        SkipNextSnapshotSave = false;
        
        _logger.LogDebug("Section state reset");
    }

    public void ResetAllState()
    {
        ResetSectionState();
        TotalCombatDuration = TimeSpan.Zero;
        
        _logger.LogInformation("All combat state reset");
    }

    public void MarkSectionStarted()
    {
        AwaitingSectionStart = false;
        SectionTimedOut = false;
        SkipNextSnapshotSave = false;
        LastSectionElapsed = TimeSpan.Zero;
        
        _logger.LogDebug("Section marked as started");
    }

    public void MarkSectionEnded(TimeSpan finalDuration)
    {
        LastSectionElapsed = finalDuration;
        SectionTimedOut = true;
        
        _logger.LogInformation("Section ended with duration: {Duration:F1}s", finalDuration.TotalSeconds);
    }

    public void AccumulateSectionDuration()
    {
        if (LastSectionElapsed > TimeSpan.Zero)
        {
            TotalCombatDuration += LastSectionElapsed;
            _logger.LogInformation(
                "Accumulated section duration: +{Duration:F1}s, Total: {Total:F1}s",
                LastSectionElapsed.TotalSeconds,
                TotalCombatDuration.TotalSeconds);
        }
    }
}
