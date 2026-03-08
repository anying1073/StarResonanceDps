using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Storage event handlers partial class for DpsStatisticsViewModel
/// Handles all events from IDataStorage
/// </summary>
public partial class DpsStatisticsViewModel
{
    private void SectionEnded()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger.LogInformation("=== SectionEnded event received ===");

            if (ScopeTime != ScopeTime.Current)
            {
                _logger.LogDebug("跳过历史保存: ScopeTime={ScopeTime}, DataCount={Count}",
                    ScopeTime, _storage.GetStatisticsCount(true));
                return;
            }

            SaveScopeCurrentHistory();

            var finalSectionDuration = _timerService.SectionDuration;
            _timerService.StopSection();
            _resetCoordinator.ResetCurrentSection();

            _logger.LogInformation("Section ended, final duration: {Duration:F1}s (using DpsTimerService)",
                finalSectionDuration.TotalSeconds);
        }
    }

    private void SaveScopeCurrentHistory()
    {
        var statCount = _storage.GetStatisticsCount(false);
        if (statCount <= 0) return;

        try
        {
            var duration = _timerService.SectionDuration;
            _logger.LogInformation(
                "脱战自动保存历史, 数据量: {Count}, 时长: {Duration:F1}s (using DpsTimerService)",
                _storage.GetStatisticsCount(false),
                duration.TotalSeconds);

            HistoryService.SaveScopeCurrentHistory(duration, Options.MinimalDurationInSeconds);

            _logger.LogInformation("脱战自动保存历史成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "脱战自动保存历史失败");
        }
    }

    private void StorageOnBeforeSectionCleared()
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger.LogInformation("=== BeforeSectionCleared: 准备保存历史 (数据还在!) ===");
        }
    }

    private void StorageOnNewSectionCreated()
    {
        InvokeOnDispatcher(() =>
        {
            _logger.LogInformation("=== NewSectionCreated triggered (数据已被清空) ===");
            ResetSubViewModelsIf(() => (_dataSourceEngine.CurrentMode & DataSourceMode.Paused) == 0);
            _timerService.Start();
            _timerService.StartNewSection();

            UpdateBattleDuration();
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
                slot.Player.Guild = info.Guild ?? slot.Player.Guild;

                if (_storage.CurrentPlayerInfo.UID == info.UID)
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

            _timerService.Stop();
            if (AppConfig.ClearLogAfterTeleport)
            {
                ResetSection();
            }

            if (_storage.GetStatisticsCount(ScopeTime == ScopeTime.Total) <= 0) return;

            try
            {
                HistoryService.SaveScopeTotalHistory(BattleDuration, Options.MinimalDurationInSeconds);
                _logger.LogInformation("服务器切换时保存全程历史成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "服务器切换时保存历史失败");
            }
        }
    }

    private void StorageOnServerConnectionStateChanged(bool serverConnectionState)
    {
        InvokeOnDispatcher(() => IsServerConnected = serverConnectionState);
    }
}