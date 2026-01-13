using System.ComponentModel;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Storage event handlers partial class for DpsStatisticsViewModel
/// Handles all events from IDataStorage
/// </summary>
public partial class DpsStatisticsViewModel
{
    private void SectionEnded()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(SectionEnded);
            return;
        }

        _logger.LogInformation("=== SectionEnded event received ===");

        var finalSectionDuration = _timerService.GetSectionDuration();
        _combatState.MarkSectionEnded(finalSectionDuration);

        _logger.LogInformation("Section ended, final duration: {Duration:F1}s (using DpsTimerService)", 
            finalSectionDuration.TotalSeconds);

        UpdateBattleDuration();
    }

    private void StorageOnBeforeSectionCleared()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger.LogInformation("=== BeforeSectionCleared: 准备保存快照 (数据还在!) ===");

            if (ScopeTime != ScopeTime.Current)
            {
                _logger.LogDebug("跳过快照保存: ScopeTime={ScopeTime}, DataCount={Count}", 
                    ScopeTime, _storage.GetStatisticsCount(true));
                return;
            }

            var statCount = _storage.GetStatisticsCount(false);
            if (statCount <= 0) return;
            
            try
            {
                var duration = _timerService.GetSectionDuration();
                _logger.LogInformation(
                    "脱战自动保存快照, 数据量: {Count}, 时长: {Duration:F1}s (using DpsTimerService)",
                    _storage.GetStatisticsCount(false),
                    duration.TotalSeconds);

                SnapshotService.SaveCurrentSnapshot(_storage, duration, Options.MinimalDurationInSeconds);

                _combatState.SkipNextSnapshotSave = true;

                _logger.LogInformation("? 脱战自动保存快照成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? 脱战自动保存快照失败");
            }
        }
    }

    private void StorageOnNewSectionCreated()
    {
        _dispatcher.BeginInvoke(() =>
        {
            _logger.LogInformation("=== NewSectionCreated triggered (数据已被清空) ===");

            if (_combatState.SkipNextSnapshotSave)
            {
                _logger.LogInformation("?? 跳过快照保存(已在脱战前保存)");
                _combatState.SkipNextSnapshotSave = false;
            }

            if (_timerService.IsRunning)
            {
                _combatState.AccumulateSectionDuration();
            }

            _combatState.AwaitingSectionStart = true;
            _combatState.SectionTimedOut = false;
            UpdateBattleDuration();

            _logger.LogInformation("NewSection完成: awaiting={AwaitingStart}, 全程时长={TotalDuration:F1}s",
                _combatState.AwaitingSectionStart, _combatState.TotalCombatDuration.TotalSeconds);
        });
    }

    private void StorageOnPlayerInfoUpdated(PlayerInfo? info)
    {
        if (info == null) return;

        foreach (var subViewModel in StatisticData.Values)
        {
            if (!subViewModel.DataDictionary.TryGetValue(info.UID, out var slot))
            {
                continue;
            }

            InvokeOnDispatcher(ApplyUpdate);
            continue;

            void ApplyUpdate()
            {
                slot.Player.Name = info.Name ?? slot.Player.Name;
                slot.Player.Class = info.ProfessionID.GetClassNameById();
                slot.Player.Spec = info.Spec;
                slot.Player.Uid = info.UID;

                if (_storage.CurrentPlayerUUID == info.UID)
                {
                    subViewModel.CurrentPlayerSlot = slot;
                }
            }
        }
    }

    private void StorageOnServerChanged(string currentServer, string prevServer)
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger.LogInformation("服务器切换: {Prev} -> {Current}", prevServer, currentServer);

            if (ScopeTime != ScopeTime.Total || _storage.GetStatisticsCount(true) <= 0) return;
            
            try
            {
                SnapshotService.SaveTotalSnapshot(_storage, BattleDuration, Options.MinimalDurationInSeconds);
                _logger.LogInformation("服务器切换时保存全程快照成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "服务器切换时保存快照失败");
            }
        }
    }

    private void StorageOnServerConnectionStateChanged(bool serverConnectionState)
    {
        if (_dispatcher.CheckAccess())
        {
            IsServerConnected = serverConnectionState;
        }
        else
        {
            _dispatcher.Invoke(() => IsServerConnected = serverConnectionState);
        }
    }
}
