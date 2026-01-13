using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;

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
    private void LoadSnapshot(BattleSnapshotData snapshot)
    {
        if (snapshot == null)
        {
            _logger.LogWarning("尝试加载空快照");
            return;
        }

        try
        {
            _logger.LogInformation("加载快照: {Label}", snapshot.DisplayLabel);

            // ? 进入快照查看模式
            EnterSnapshotViewMode(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载快照失败: {Label}", snapshot.DisplayLabel);
        }
    }

    /// <summary>
    /// Enter snapshot view mode - pauses real-time updates and loads snapshot data
    /// </summary>
    private void EnterSnapshotViewMode(BattleSnapshotData snapshot)
    {
        _dispatcher.Invoke(() =>
        {
            _logger.LogInformation("=== 进入快照查看模式 ===");

            // 1. Save current state for restoration later
            _wasPassiveMode = AppConfig.DpsUpdateMode == DpsUpdateMode.Passive;
            _wasTimerRunning = _dpsUpdateTimer?.IsEnabled ?? false;

            // 2. Stop all update mechanisms
            
            // Stop DPS update timer
            if (_dpsUpdateTimer != null)
            {
                _dpsUpdateTimer.Stop();
                _logger.LogDebug("已停止DPS更新定时器");
            }

            // Stop duration timer
            if (_durationTimer != null)
            {
                _durationTimer.Stop();
                _logger.LogDebug("已停止战斗时长定时器");
            }

            // Stop main Stopwatch timer
            if (_timerService.IsRunning)
            {
                _timerService.Stop();
                _logger.LogDebug("已停止主计时器");
            }

            // 3. Unsubscribe from real-time data events
            _storage.DpsDataUpdated -= UpdateData;
            _storage.NewSectionCreated -= StorageOnNewSectionCreated;
            _logger.LogDebug("已取消订阅实时数据事件");

            // 4. Set snapshot mode flags
            IsViewingSnapshot = true;
            CurrentSnapshot = snapshot;

            // 5. Load snapshot data to UI
            LoadSnapshotDataToUI(snapshot);

            _logger.LogInformation("快照查看模式已启动: {Label}, 战斗时长: {Duration}",
                snapshot.DisplayLabel, snapshot.Duration);
        });
    }

    /// <summary>
    /// Exit snapshot view mode and restore real-time statistics
    /// </summary>
    [RelayCommand]
    private void ExitSnapshotViewMode()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(ExitSnapshotViewMode);
            return;
        }

        _logger.LogInformation("=== 退出快照查看模式 ===");

        // 1. Clear snapshot state
        IsViewingSnapshot = false;
        CurrentSnapshot = null;

        // 2. Clear UI data
        foreach (var subVm in StatisticData.Values)
        {
            subVm.Reset();
        }

        // 3. Restore DPS update mechanism
        if (_wasPassiveMode)
        {
            _storage.DpsDataUpdated += UpdateData;
            _logger.LogDebug("已恢复订阅DpsDataUpdated事件");
        }
        else if (_wasTimerRunning && _dpsUpdateTimer != null)
        {
            _dpsUpdateTimer.Start();
            _logger.LogDebug("已恢复DPS更新定时器");
        }

        // Restore duration timer
        if (_durationTimer != null && !_durationTimer.IsEnabled)
        {
            _durationTimer.Start();
            _logger.LogDebug("已恢复战斗时长定时器");
        }

        // Restore main timer (if it was running before)
        // Note: only restore if there's real-time data
        var hasData = _storage.HasData();
        if (hasData && !_timerService.IsRunning)
        {
            _timerService.Start();
            _logger.LogDebug("已恢复主计时器");
        }

        _storage.NewSectionCreated += StorageOnNewSectionCreated;

        // 4. Refresh real-time data
        RefreshData();
        UpdateBattleDuration();

        _logger.LogInformation("已恢复实时DPS统计模式");
    }

    /// <summary>
    /// Load snapshot data to UI for display
    /// </summary>
    private void LoadSnapshotDataToUI(BattleSnapshotData snapshot)
    {
        _logger.LogDebug("开始加载快照数据到UI...");

        // ? Clear all SubViewModel data to avoid real-time data residue
        foreach (var subViewModel in StatisticData.Values)
        {
            subViewModel.Data.Clear();
            subViewModel.DataDictionary.Clear();
        }

        // 1. Set battle duration
        BattleDuration = snapshot.Duration;

        // 2. Set team total damage/healing
        TeamTotalDamage = snapshot.TeamTotalDamage;
        TeamTotalDps = snapshot.Duration.TotalSeconds > 0
            ? snapshot.TeamTotalDamage / snapshot.Duration.TotalSeconds
            : 0;

        // 3. Build data for each statistic type
        var damageData = new Dictionary<long, DpsDataProcessed>();
        var healingData = new Dictionary<long, DpsDataProcessed>();
        var takenData = new Dictionary<long, DpsDataProcessed>();
        var npcTakenData = new Dictionary<long, DpsDataProcessed>();

        foreach (var (uid, playerData) in snapshot.Players)
        {
            // ? Null check
            if (playerData == null)
            {
                _logger.LogWarning("Player data is null for UID: {Uid}", uid);
                continue;
            }

            // Build skill lists
            var damageSkills = ConvertSnapshotSkillsToViewModel(playerData.DamageSkills, StatisticType.Damage);
            var healingSkills = ConvertSnapshotSkillsToViewModel(playerData.HealingSkills, StatisticType.Healing);
            var takenSkills = ConvertSnapshotSkillsToViewModel(playerData.TakenSkills, StatisticType.TakenDamage);

            // Damage statistics
            if (playerData.TotalDamage > 0)
            {
                // Control NPC display based on IsIncludeNpcData setting
                var shouldShow = !playerData.IsNpc || IsIncludeNpcData;
                if (shouldShow)
                {
                    // ? Use dummy PlayerStatistics instead of null
                    var dummyDpsData = new PlayerStatistics(uid);
                    damageData[uid] = new DpsDataProcessed(
                        dummyDpsData,
                        playerData.TotalDamage,
                        0, 
                        playerData.Uid
                    );
                }
            }

            // Healing statistics (excluding NPCs)
            if (playerData.TotalHealing > 0 && !playerData.IsNpc)
            {
                var dummyDpsData = new PlayerStatistics(uid);
                healingData[uid] = new DpsDataProcessed(
                    dummyDpsData,
                    playerData.TotalHealing,
                    0, 
                    playerData.Uid
                );
            }

            // Taken damage statistics
            if (playerData.TakenDamage > 0)
            {
                if (playerData.IsNpc)
                {
                    // NPC taken damage
                    var dummyDpsData = new PlayerStatistics(uid);
                    npcTakenData[uid] = new DpsDataProcessed(
                        dummyDpsData,
                        playerData.TakenDamage,
                        0, 
                        playerData.Uid
                    );
                }
                else // Player taken damage
                {
                    var dummyDpsData = new PlayerStatistics(uid);
                    takenData[uid] = new DpsDataProcessed(
                        dummyDpsData,
                        playerData.TakenDamage,
                        0, 
                        playerData.Uid
                    );
                }
            }
        }

        // 4. Update UI for each statistic type
        StatisticData[StatisticType.Damage].UpdateDataOptimized(damageData, 0);
        StatisticData[StatisticType.Healing].UpdateDataOptimized(healingData, 0);
        StatisticData[StatisticType.TakenDamage].UpdateDataOptimized(takenData, 0);
        StatisticData[StatisticType.NpcTakenDamage].UpdateDataOptimized(npcTakenData, 0);
    }

    /// <summary>
    /// Convert snapshot skill data to ViewModel for UI display
    /// </summary>
    private List<SkillItemViewModel> ConvertSnapshotSkillsToViewModel(List<SnapshotSkillData> snapshotSkills,
        StatisticType statisticType)
    {
        if (snapshotSkills.Count == 0)
            return new List<SkillItemViewModel>();

        return snapshotSkills.Select(s =>
        {
            var average = s.UseTimes > 0 ? Math.Round(s.TotalValue / (double)s.UseTimes) : 0d;
            var avgValue = average > int.MaxValue ? int.MaxValue : (int)average;
            var critRate = s.UseTimes > 0 ? (double)s.CritTimes / s.UseTimes : 0d;

            var vm = new SkillItemViewModel
            {
                SkillId = s.SkillId,
                SkillName = s.SkillName,
                TotalValue = (long)s.TotalValue,
                HitCount = s.UseTimes,
                CritCount = s.CritTimes,
                LuckyCount = s.LuckyTimes,
                Average = avgValue,
                CritRate = critRate
            };

            return vm;
        }).ToList();
    }
}
