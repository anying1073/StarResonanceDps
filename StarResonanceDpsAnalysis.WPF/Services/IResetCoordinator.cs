using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Coordinates reset operations for DPS statistics
/// Single Responsibility: Orchestrate reset logic across multiple services
/// </summary>
public interface IResetCoordinator
{
    /// <summary>
    /// Reset current section only (preserve total data)
    /// </summary>
    void ResetCurrentSection();

    /// <summary>
    /// Reset all data (including total)
    /// </summary>
    void ResetAll();

    /// <summary>
    /// Reset specific scope (Current or Total)
    /// </summary>
    /// <param name="scope">Scope to reset</param>
    void Reset(ScopeTime scope);

    /// <summary>
    /// Reset with optional snapshot save
    /// </summary>
    /// <param name="scope">Scope to reset</param>
    /// <param name="saveSnapshot">Whether to save snapshot before reset</param>
    /// <param name="battleDuration">Current battle duration for snapshot</param>
    /// <param name="minimalDuration">Minimal duration threshold for snapshot</param>
    void ResetWithSnapshot(ScopeTime scope, bool saveSnapshot, TimeSpan battleDuration, int minimalDuration);
}
