using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;
using System.ComponentModel;

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
            Mode = AppConfig.DpsUpdateMode.ToDataSourceMode(),
            ActiveUpdateInterval = AppConfig.DpsUpdateInterval,
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
        if (e.PropertyName == nameof(AppConfig.MaskPlayerName))
        {
            // まずUI反映（既存処理があるならそれを残す）
            ApplyMaskToPlayers(AppConfig.MaskPlayerName);

            // “外した時”だけ毎回警告
            if (!_maskWarningReentry && _isInitialized && !AppConfig.MaskPlayerName)
            {
                _maskWarningReentry = true;
                try
                {
                    var title = _localizationManager.GetString(ResourcesKeys.Settings_PlayerNameMask_Warning_Title);
                    var message = _localizationManager.GetString(ResourcesKeys.Settings_PlayerNameMask_Warning_Message);
                    var result = _messageDialogService.Show(title, message, _windowManagement.DpsStatisticsView);
                    if (result != true)
                    {
                        AppConfig.MaskPlayerName = true; // キャンセルなら戻す
                    }
                }
                finally
                {
                    _maskWarningReentry = false;
                }
            }

            return;
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
        _logger.LogDebug("IsIncludeNpcData changed to: {Value}", value);

        _configManager.CurrentConfig.IsIncludeNpcData = value;
        _ = _configManager.SaveAsync();
        _logger.LogInformation("统计NPC设置已保存到配置: {Value}", value);

        // ★重要：processed は includeNpcData により中身が変わるので古いキャッシュを無効化
        ClearProcessedCache();
        PublishEmptyTeamTotal(StatisticIndex);

        // 新しい設定で再生成させる
        UpdateData();
    }

    partial void OnScopeTimeChanged(ScopeTime oldValue, ScopeTime newValue)
    {
        _logger.LogInformation("ScopeTime changed: {OldValue} -> {NewValue}", oldValue, newValue);

        foreach (var subViewModel in StatisticData.Values)
        {
            // ★修正：oldValue ではなく newValue
            subViewModel.ScopeTime = newValue;
            subViewModel.Data.Clear();
            subViewModel.DataDictionary.Clear();
        }

        // scope が変わる＝データも総入れ替えなのでキャッシュ無効化
        ClearProcessedCache();
        PublishEmptyTeamTotal(StatisticIndex);

        UpdateBattleDuration();
        _dataSourceEngine.Scope = newValue;
        UpdateData();
        OnPropertyChanged(nameof(CurrentStatisticData));
    }

    partial void OnShowTeamTotalDamageChanged(bool value)
    {
        _logger.LogDebug("ShowTeamTotalDamage changed to: {Value}", value);

        _teamStatsManager.ShowTeamTotal = value;

        _configManager.CurrentConfig.ShowTeamTotalDamage = value;
        _ = _configManager.SaveAsync();
        _logger.LogInformation("显示团队总伤设置已保存到配置: {Value}", value);

        // ★ON/OFF 即時反映（残り値防止）
        if (!value)
        {
            _teamStatsManager.ResetTeamStats();
            TeamTotalDamage = 0;
            TeamTotalDps = 0;
            return;
        }

        // ON に戻したら、最新キャッシュから即再計算（データ到着待ちしない）
        RecalculateAndPublishTeamTotalFor(StatisticIndex);
    }

    partial void OnStatisticIndexChanged(StatisticType value)
    {
        TeamLabel = GetTeamLabel(value);
        CurrentPlayerLabel = GetCurrentPlayerLabel(value);
        TeamTotalLabel = GetTeamTotalLabel(value);

        _logger.LogDebug("OnStatisticIndexChanged: Changed to {Type}", value);

        OnPropertyChanged(nameof(CurrentStatisticData));

        // ★タブ切替で即再計算（キャッシュから）
        if (ShowTeamTotalDamage)
        {
            RecalculateAndPublishTeamTotalFor(value);
        }
        else
        {
            // 非表示中なら表示値も落としておく（残り値防止）
            TeamTotalDamage = 0;
            TeamTotalDps = 0;
        }
    }
}