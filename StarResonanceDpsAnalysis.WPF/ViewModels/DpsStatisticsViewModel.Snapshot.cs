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
/// Snapshot management partial class for DpsStatisticsViewModel
/// Handles battle snapshot viewing, loading, and mode switching
/// </summary>
public partial class DpsStatisticsViewModel
{
    // Note: Fields are defined in the main DpsStatisticsViewModel.cs file:
    // - _currentSnapshot (observable property)
    // - _isViewingSnapshot (observable property)
    // - _wasPassiveMode
    // - _wasTimerRunning
    // - _skipNextSnapshotSave
    // - SnapshotService (property)

    // ===== Snapshot View Commands =====

    /// <summary>
    /// View the full/total snapshot (switches to Total mode)
    /// </summary>
    [RelayCommand]
    private void ViewFullSnapshot()
    {
        // 查看全程快照(合并所有分段)
        // 只在当前有战斗数据时允许
        if (_storage.GetStatisticsCount(true) == 0)
        {
            _messageDialogService.Show(
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_Snapshot_ViewFull_Title, defaultValue: "View full snapshot"),
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_Snapshot_ViewFull_EmptyMessage, defaultValue: "No full snapshot data available."),
                _windowManagement.DpsStatisticsView);
            return;
        }

        // 切换到全程模式
        _logger.LogInformation("切换到全程模式以查看快照");
        ScopeTime = ScopeTime.Total;
    }

    /// <summary>
    /// View the current battle snapshot (switches to Current mode)
    /// </summary>
    [RelayCommand]
    private void ViewCurrentSnapshot()
    {
        // 查看当前战斗快照
        // 只在有分段数据时允许
        if (_storage.GetStatisticsCount(false) == 0)
        {
            _messageDialogService.Show(
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_Snapshot_ViewCurrent_Title, defaultValue: "View battle snapshot"),
                _localizationManager.GetString(ResourcesKeys.DpsStatistics_Snapshot_ViewCurrent_EmptyMessage, defaultValue: "No battle snapshot data available."),
                _windowManagement.DpsStatisticsView);
            return;
        }

        // 切换到当前模式
        _logger.LogInformation("切换到当前模式以查看战斗快照");
        ScopeTime = ScopeTime.Current;
    }

    /// <summary>
    /// Load a specific snapshot and enter snapshot view mode
    /// </summary>
    [RelayCommand]
    private void LoadSnapshot(SnapshotInfo snapshotInfo)
    {
        try
        {
            EnterSnapshotViewMode(snapshotInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载快照失败: {snapshotFilePath}", snapshotInfo.FilePath);
        }
    }

    /// <summary>
    /// Enter snapshot view mode - pauses real-time updates and loads snapshot data
    /// </summary>
    private void EnterSnapshotViewMode(SnapshotInfo snapshotInfo)
    {
        InvokeOnDispatcher(() =>
        {
            _logger.LogInformation("=== 进入快照查看模式 ===");

            // 4. Set snapshot mode flags
            IsViewingSnapshot = true;
            var filePath = snapshotInfo.FilePath;
            var snapshot = SnapshotService.LoadSnapshot(filePath);
            if (snapshot == null)
            {
                _logger.LogWarning("Snapshot data load failed");
                return;
            }

            CurrentSnapshot = snapshot;

            // 5. Load snapshot data to UI
            LoadSnapshotDataToUI(filePath);

            _logger.LogInformation("快照查看模式已启动: {Label}, 战斗时长: {Duration}",
                CurrentSnapshot.DisplayLabel, CurrentSnapshot.Duration);
        });
    }

    /// <summary>
    /// Exit snapshot view mode and restore real-time statistics
    /// </summary>
    [RelayCommand]
    private void ExitSnapshotViewMode()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger.LogInformation("=== 退出快照查看模式 ===");

            // 1. Clear snapshot state
            IsViewingSnapshot = false;
            CurrentSnapshot = null;

            // 2. Clear UI data
            foreach (var subVm in StatisticData.Values)
            {
                subVm.Reset();
            }

            UnloadSnapshotData();

            // 4. Refresh real-time data
            UpdateBattleDuration();

            _logger.LogInformation("已恢复实时DPS统计模式");
        }
    }

    /// <summary>
    /// Load snapshot data to UI for display
    /// </summary>
    private void LoadSnapshotDataToUI(string filePath)
    {
        _logger.LogDebug("Load snapshot...");
        _dataSourceEngine.Configure(new DataSourceEngineParam()
        {
            Mode = DataSourceMode.Snapshot,
            BattleSnapshotFilePath = filePath,
        });
    }

    private void UnloadSnapshotData()
    {
        _logger.LogDebug("Unload snapshot...");
        _dataSourceEngine.Configure(new DataSourceEngineParam()
        {
            Mode = AppConfig.DpsUpdateMode.ToDataSourceMode(),
        });
    }
}
