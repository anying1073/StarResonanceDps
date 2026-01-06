using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics.Calculators;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Adapter that converts new statistics format to legacy DpsData format
/// Allows gradual migration without breaking existing code
/// </summary>
public sealed class StatisticsAdapter
{
    private readonly StatisticsEngine _engine;
    private readonly ILogger? _logger;
    private readonly ISampleRecorder _sampleRecorder;

    public StatisticsAdapter(ILogger? logger = null, ISampleRecorder? sampleRecorder = null)
    {
        _logger = logger;
        _engine = new StatisticsEngine();
        _sampleRecorder = sampleRecorder ?? new PeriodicSampleRecorder();

        // Register all calculators (OCP: easily add new ones)
        _engine.RegisterCalculator(new AttackDamageCalculator());
        _engine.RegisterCalculator(new TakenDamageCalculator());
        _engine.RegisterCalculator(new HealingCalculator());
    }

    /// <summary>
    /// Process a battle log
    /// </summary>
    public void ProcessLog(BattleLog log)
    {
        try
        {
            _engine.ProcessBattleLog(log);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing battle log");
        }
    }

    /// <summary>
    /// Record DPS/HPS/DTPS samples for all players
    /// Records samples for BOTH full session and current section simultaneously
    /// Should be called periodically (e.g., every update cycle)
    /// </summary>
    /// <param name="sectionDuration">Time elapsed since section start</param>
    public void RecordSamples(TimeSpan sectionDuration)
    {
        // Get statistics for both scopes
        var fullStats = _engine.GetFullStatistics();
        var sectionStats = _engine.GetSectionStatistics();

        // Record samples (UpdateDeltaValues is called inside RecordSamples)
        _sampleRecorder.RecordSamples(fullStats, sectionDuration);
        _sampleRecorder.RecordSamples(sectionStats, sectionDuration);
    }

    /// <summary>
    /// Reset section statistics and battle logs
    /// Clears section samples but preserves full session samples
    /// </summary>
    public void ResetSection()
    {
        // ? Clear ONLY section samples (not full session)
        var sectionStats = _engine.GetSectionStatistics();
        foreach (var playerStats in sectionStats.Values)
        {
            playerStats.ClearSamples();
            playerStats.ResumeDeltaTracking(); // Resume tracking for new section
        }
        
        _engine.ResetSection();
    }
    
    /// <summary>
    /// Stop delta tracking for all players (called when section ends)
    /// Preserves current delta values but stops calculating new ones
    /// </summary>
    public void StopDeltaTracking()
    {
        var sectionStats = _engine.GetSectionStatistics();
        foreach (var playerStats in sectionStats.Values)
        {
            playerStats.StopDeltaTracking();
        }
        
        _logger?.LogDebug("Delta tracking stopped for section statistics");
    }

    /// <summary>
    /// Clear all statistics and battle logs (both full and section)
    /// Clears samples for both scopes
    /// </summary>
    public void ClearAll()
    {
        // ? Clear samples for BOTH full and section
        var fullStats = _engine.GetFullStatistics();
        foreach (var playerStats in fullStats.Values)
        {
            playerStats.ClearSamples();
        }
        
        var sectionStats = _engine.GetSectionStatistics();
        foreach (var playerStats in sectionStats.Values)
        {
            playerStats.ClearSamples();
        }
        
        _engine.ClearAll();
    }

    /// <summary>
    /// Get battle logs
    /// </summary>
    public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession)
    {
        return fullSession
            ? _engine.GetFullBattleLogs()
            : _engine.GetSectionBattleLogs();
    }

    /// <summary>
    /// ? NEW: Get battle logs for a specific player (filtered by attacker or target)
    /// </summary>
    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession)
    {
        var allLogs = fullSession
            ? _engine.GetFullBattleLogs()
            : _engine.GetSectionBattleLogs();

        return allLogs
            .Where(log => log.AttackerUuid == uid || log.TargetUuid == uid)
            .ToList();
    }

    /// <summary>
    /// Get raw statistics (new format)
    /// </summary>
    public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession)
    {
        return fullSession
            ? _engine.GetFullStatistics()
            : _engine.GetSectionStatistics();
    }

    public int GetStatisticsCount(bool fullSession)
    {
        return fullSession
            ? _engine.GetFullStatisticsCount()
            : _engine.GetSectionStatisticsCount();
    }
}
