using System;
using System.Collections.Generic;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Service for processing and preparing DPS data for UI consumption
/// Follows Single Responsibility Principle - only handles data transformation
/// </summary>
public interface IDpsDataProcessor
{
    /// <summary>
    /// Pre-process data for all statistic types to avoid redundant iterations
    /// </summary>
    Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> PreProcessData(
        IReadOnlyDictionary<long, PlayerStatistics> data,
        bool includeNpcData);
    
    /// <summary>
    /// Calculate team total statistics
    /// </summary>
    TeamTotalStats CalculateTeamTotal(
        IReadOnlyDictionary<long, PlayerStatistics> data,
        StatisticType statisticType);
}

/// <summary>
/// Team total statistics result
/// </summary>
public record TeamTotalStats(
    ulong TotalValue,
    double TotalDps,
    int PlayerCount,
    int NpcCount,
    double MaxDuration);
