using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// History management partial class for DpsStatisticsViewModel
/// Handles battle History viewing, loading, and mode switching
/// </summary>
public partial class DpsStatisticsViewModel
{
    // Note: Fields are defined in the main DpsStatisticsViewModel.cs file:
    // - _currentHistory (observable property)
    // - _isViewingHistory (observable property)
    // - _wasPassiveMode
    // - _wasTimerRunning
    // - _skipNextHistorySave
    // - HistoryService (property)

    // ===== History View Commands =====

    /// <summary>
    /// View the full/total History (switches to Total mode)
    /// </summary>
    [RelayCommand]
    private void ViewFullHistory()
    {
        // 查看全程快照(合并所有分段)
        // 只在当前有战斗数据时允许
        if (_storage.GetStatisticsCount(true) == 0)
        {
            _messageDialogService.Show(
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_History_ViewFull_Title, defaultValue: "View full History"),
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_History_ViewFull_EmptyMessage, defaultValue: "No full History data available."),
                _windowManagement.DpsStatisticsView);
            return;
        }

        // 切换到全程模式
        _logger.LogInformation("切换到全程模式以查看快照");
        ScopeTime = ScopeTime.Total;
    }

    /// <summary>
    /// View the current battle History (switches to Current mode)
    /// </summary>
    [RelayCommand]
    private void ViewCurrentHistory()
    {
        // 查看当前战斗快照
        // 只在有分段数据时允许
        if (_storage.GetStatisticsCount(false) == 0)
        {
            _messageDialogService.Show(
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_History_ViewCurrent_Title, defaultValue: "View battle History"),
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_History_ViewCurrent_EmptyMessage, defaultValue: "No battle History data available."),
                _windowManagement.DpsStatisticsView);
            return;
        }

        // 切换到当前模式
        _logger.LogInformation("切换到当前模式以查看战斗快照");
        ScopeTime = ScopeTime.Current;
    }

    /// <summary>
    /// Load a specific History and enter History view mode
    /// </summary>
    [RelayCommand]
    private void LoadHistory(HistoryInfo historyInfo)
    {
        try
        {
            EnterHistoryViewMode(historyInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载快照失败: {HistoryFilePath}", historyInfo.FilePath);
        }
    }

    /// <summary>
    /// Enter History view mode - pauses real-time updates and loads History data
    /// </summary>
    private void EnterHistoryViewMode(HistoryInfo historyInfo)
    {
        InvokeOnDispatcher(() =>
        {
            _logger.LogInformation("=== 进入快照查看模式 ===");

            // 4. Set History mode flags
            IsViewingHistory = true;
            var filePath = historyInfo.FilePath;
            var History = HistoryService.LoadHistory(filePath);
            if (History == null)
            {
                _logger.LogWarning("History data load failed");
                return;
            }

            CurrentHistory = History;

            // 5. Load History data to UI
            LoadHistoryDataToUI(filePath);

            _logger.LogInformation("快照查看模式已启动: {Label}, 战斗时长: {Duration}",
                CurrentHistory.DisplayLabel, CurrentHistory.Duration);
        });
    }

    /// <summary>
    /// Exit History view mode and restore real-time statistics
    /// </summary>
    [RelayCommand]
    private void ExitHistoryViewMode()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger.LogInformation("=== 退出快照查看模式 ===");

            // 1. Clear History state
            IsViewingHistory = false;
            CurrentHistory = null;

            // 2. Clear UI data
            foreach (var subVm in StatisticData.Values)
            {
                subVm.Reset();
            }

            UnloadHistoryData();

            // 4. Refresh real-time data
            UpdateBattleDuration();

            _logger.LogInformation("已恢复实时DPS统计模式");
        }
    }

    /// <summary>
    /// Load History data to UI for display
    /// </summary>
    private void LoadHistoryDataToUI(string filePath)
    {
        _logger.LogDebug("Load History...");
        _dataSourceEngine.Configure(new DataSourceEngineParam()
        {
            Mode = DataSourceMode.History,
            BattleHistoryFilePath = filePath,
        });
    }

    private void UnloadHistoryData()
    {
        _logger.LogDebug("Unload History...");
        _dataSourceEngine.Configure(new DataSourceEngineParam()
        {
            Mode = AppConfig.DpsUpdateMode.ToDataSourceMode(),
        });
    }
}
