using System.ComponentModel;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Configuration and initialization methods partial class for DpsStatisticsViewModel
/// Handles configuration updates, initialization, and settings
/// </summary>
public partial class DpsStatisticsViewModel
{
    private void ConfigManagerOnConfigurationUpdated(object? sender, AppConfig newConfig)
    {
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            var oldMode = AppConfig.DpsUpdateMode;
            var oldInterval = AppConfig.DpsUpdateInterval;

            AppConfig = newConfig;

            if (oldMode != newConfig.DpsUpdateMode || oldInterval != newConfig.DpsUpdateInterval)
            {
                ConfigureDpsUpdateMode();
            }
        }
    }

    private void ConfigureDpsUpdateMode()
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("ConfigureDpsUpdateMode called but not initialized");
            return;
        }

        _logger.LogInformation(
            "Configuring DPS update mode: {Mode}, Interval: {Interval}ms",
            AppConfig.DpsUpdateMode,
            AppConfig.DpsUpdateInterval);

        _dataSourceEngine.Configure(new DataSourceEngineParam()
        {
            Mode = AppConfig.DpsUpdateMode.ToDataSourceMode()
        });

        _logger.LogInformation("Update mode configuration complete. Mode: {Mode}, DataSourceEngine Mode: {CurrentMode}",
            AppConfig.DpsUpdateMode,
            _dataSourceEngine.CurrentMode);
    }

    private void LoadDpsStatisticsSettings()
    {
        var savedSkillLimit = _configManager.CurrentConfig.SkillDisplayLimit;
        if (savedSkillLimit > 0)
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SkillDisplayLimit = savedSkillLimit;
            }

            _logger.LogInformation("从配置加载技能显示数量: {Limit}", savedSkillLimit);
        }

        IsIncludeNpcData = _configManager.CurrentConfig.IsIncludeNpcData;
        _logger.LogInformation("从配置加载统计NPC设置: {Value}", IsIncludeNpcData);

        ShowTeamTotalDamage = _configManager.CurrentConfig.ShowTeamTotalDamage;
        _logger.LogInformation("从配置加载显示团队总伤设置: {Value}", ShowTeamTotalDamage);

        Options.MinimalDurationInSeconds = _configManager.CurrentConfig.MinimalDurationInSeconds;
        _logger.LogInformation("从配置加载最小记录时长: {Duration}秒", Options.MinimalDurationInSeconds);

        Options.PropertyChanged += Options_PropertyChanged;
    }

    private void Options_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DpsStatisticsOptions.MinimalDurationInSeconds))
        {
            var newValue = Options.MinimalDurationInSeconds;
            _configManager.CurrentConfig.MinimalDurationInSeconds = newValue;
            _ = _configManager.SaveAsync();
            _logger.LogInformation("最小记录时长已保存到配置: {Duration}秒", newValue);
        }
    }

    partial void OnAppConfigChanging(AppConfig? oldValue, AppConfig newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= AppConfigOnPropertyChanged;
        newValue.PropertyChanged += AppConfigOnPropertyChanged;
        ApplyMaskToPlayers(newValue.MaskPlayerName);
        ApplyPlayerInfoFormatToPlayers(newValue.PlayerInfoFormatString);
        ApplyPlayerInfoFormatSwitchToPlayers(newValue.UseCustomFormat);
    }

    private void AppConfigOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppConfig.MaskPlayerName))
        {
            ApplyMaskToPlayers(AppConfig.MaskPlayerName);
        }

        if (e.PropertyName == nameof(AppConfig.PlayerInfoFormatString))
        {
            ApplyPlayerInfoFormatToPlayers(AppConfig.PlayerInfoFormatString);
        }

        if (e.PropertyName == nameof(AppConfig.UseCustomFormat))
        {
            ApplyPlayerInfoFormatSwitchToPlayers(AppConfig.UseCustomFormat);
        }
    }

    private void ApplyMaskToPlayers(bool mask)
    {
        InvokeOnDispatcher(() =>
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SetPlayerInfoMask(mask);
            }
        });
    }

    private void ApplyPlayerInfoFormatSwitchToPlayers(bool valueUseCustomFormat)
    {
        InvokeOnDispatcher(() =>
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SetUsePlayerInfoFormat(valueUseCustomFormat);
            }
        });
    }

    private void ApplyPlayerInfoFormatToPlayers(string formatString)
    {
        InvokeOnDispatcher(() =>
        {
            foreach (var vm in StatisticData.Values)
            {
                vm.SetPlayerInfoFormat(formatString);
            }
        });
    }

    partial void OnIsIncludeNpcDataChanged(bool value)
    {
        _logger.LogDebug($"IsIncludeNpcData changed to: {value}");

        _configManager.CurrentConfig.IsIncludeNpcData = value;
        _ = _configManager.SaveAsync();
        _logger.LogInformation("统计NPC设置已保存到配置: {Value}", value);

        if (!value)
        {
            _logger.LogInformation("Removing NPC data from UI (IsIncludeNpcData=false)");

            foreach (var subViewModel in StatisticData.Values)
            {
                var npcSlots = subViewModel.Data
                    .Where(slot => slot.Player.IsNpc)
                    .ToList();

                foreach (var npcSlot in npcSlots)
                {
                    _dispatcher.Invoke(() =>
                    {
                        subViewModel.Data.Remove(npcSlot);
                        _logger.LogDebug("Removed NPC slot: UID={PlayerUid}, Name={PlayerName}", 
                            npcSlot.Player.Uid, npcSlot.Player.Name);
                    });
                }

                _logger.LogInformation($"Removed {npcSlots.Count} NPC slots from {subViewModel.GetType().Name}");
            }
        }

        UpdateData();
    }

    partial void OnScopeTimeChanged(ScopeTime value)
    {
        _logger.LogInformation("=== ScopeTime changed: {OldValue} -> {NewValue} ===", ScopeTime, value);

        foreach (var subViewModel in StatisticData.Values)
        {
            subViewModel.ScopeTime = value;
            subViewModel.Data.Clear();
            subViewModel.DataDictionary.Clear();
        }

        UpdateBattleDuration();
        UpdateData();
        OnPropertyChanged(nameof(CurrentStatisticData));

        _logger.LogInformation("=== ScopeTime change complete ===");
    }

    partial void OnShowTeamTotalDamageChanged(bool value)
    {
        _logger.LogDebug("ShowTeamTotalDamage changed to: {Value}", value);

        // Update team stats manager
        _teamStatsManager.ShowTeamTotal = value;

        // Save to config
        _configManager.CurrentConfig.ShowTeamTotalDamage = value;
        _ = _configManager.SaveAsync();
        _logger.LogInformation("显示团队总伤设置已保存到配置: {Value}", value);
    }

    partial void OnStatisticIndexChanged(StatisticType value)
    {
        _logger.LogDebug("OnStatisticIndexChanged: 切换到统计类型 {Type}", value);

        OnPropertyChanged(nameof(CurrentStatisticData));

        _logger.LogDebug("OnStatisticIndexChanged: 统计类型已切换,强制刷新完成");
    }
}
