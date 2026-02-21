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
    /// Reset with optional History save
    /// </summary>
    /// <param name="scope">Scope to reset</param>
    /// <param name="saveHistory">Whether to save History before reset</param>
    /// <param name="battleDuration">Current battle duration for History</param>
    /// <param name="minimalDuration">Minimal duration threshold for History</param>
    void ResetWithHistory(ScopeTime scope, bool saveHistory, TimeSpan battleDuration, int minimalDuration);
}
