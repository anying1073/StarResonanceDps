using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Orchestrates statistics calculation by delegating to specific calculators
/// Following SRP: Only responsible for coordinating calculators
/// Following OCP: New calculators can be added without modifying this class
/// Following DIP: Depends on IStatisticsCalculator abstraction
/// </summary>
public sealed class StatisticsEngine
{
    private readonly List<IStatisticsCalculator> _calculators = new();
    private readonly StatisticsContext _context = new();
    
    public StatisticsEngine()
    {
    }
    
    /// <summary>
    /// Register a statistics calculator
    /// Follows OCP: Easily extensible with new calculator types
    /// </summary>
    public void RegisterCalculator(IStatisticsCalculator calculator)
    {
        _calculators.Add(calculator);
    }
    
    /// <summary>
    /// Process a battle log through all registered calculators
    /// </summary>
    public void ProcessBattleLog(BattleLog log)
    {
        // Store the battle log
        _context.AddBattleLog(log);
        
        // Process through all calculators
        foreach (var calculator in _calculators)
        {
            calculator.Calculate(log, _context);
        }
    }
    
    /// <summary>
    /// Reset section statistics for all calculators
    /// </summary>
    public void ResetSection()
    {
        _context.ClearSection();
        foreach (var calculator in _calculators)
        {
            calculator.ResetSection(_context);
        }
    }
    
    /// <summary>
    /// Clear all statistics and battle logs (both full and section)
    /// </summary>
    public void ClearAll()
    {
        _context.ClearAll();
    }
    
    /// <summary>
    /// Get full statistics
    /// </summary>
    public IReadOnlyDictionary<long, PlayerStatistics> GetFullStatistics() 
        => _context.FullStatistics;
    
    /// <summary>
    /// Get section statistics
    /// </summary>
    public IReadOnlyDictionary<long, PlayerStatistics> GetSectionStatistics() 
        => _context.SectionStatistics;
    
    /// <summary>
    /// Get full battle logs
    /// </summary>
    public IReadOnlyList<BattleLog> GetFullBattleLogs()
        => _context.FullBattleLogs;
    
    /// <summary>
    /// Get section battle logs
    /// </summary>
    public IReadOnlyList<BattleLog> GetSectionBattleLogs()
        => _context.SectionBattleLogs;

    public int GetFullStatisticsCount()
    {
        return _context.FullStatistics.Count;
    }

    public int GetSectionStatisticsCount()
    {
        return _context.SectionStatistics.Count;
    }
}
