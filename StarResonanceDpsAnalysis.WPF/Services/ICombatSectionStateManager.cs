namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Manages combat section state and timing
/// Single Responsibility: Track section lifecycle and timing state
/// Note: Section timing is now handled by IDpsTimerService
/// </summary>
public interface ICombatSectionStateManager
{
    /// <summary>
    /// Whether we are waiting for the first datapoint of a new section
    /// </summary>
    bool AwaitingSectionStart { get; set; }
    
    /// <summary>
    /// Whether the section has timed out but not yet been cleared
    /// </summary>
    bool SectionTimedOut { get; set; }
    
    /// <summary>
    /// Last section elapsed time (frozen when section ends)
    /// </summary>
    TimeSpan LastSectionElapsed { get; set; }
    
    /// <summary>
    /// Total cumulative combat duration (excluding out-of-combat time)
    /// Note: This is now tracked by IDpsTimerService
    /// </summary>
    TimeSpan TotalCombatDuration { get; set; }
    
    /// <summary>
    /// Flag to skip the next automatic snapshot save
    /// </summary>
    bool SkipNextSnapshotSave { get; set; }
    
    /// <summary>
    /// Reset section state to initial values
    /// </summary>
    void ResetSectionState();
    
    /// <summary>
    /// Reset all state including total combat duration
    /// </summary>
    void ResetAllState();
    
    /// <summary>
    /// Mark section as started with new data
    /// </summary>
    void MarkSectionStarted();
    
    /// <summary>
    /// Mark section as ended/timed out
    /// </summary>
    void MarkSectionEnded(TimeSpan finalDuration);
    
    /// <summary>
    /// Accumulate completed section duration to total
    /// </summary>
    void AccumulateSectionDuration();
}
