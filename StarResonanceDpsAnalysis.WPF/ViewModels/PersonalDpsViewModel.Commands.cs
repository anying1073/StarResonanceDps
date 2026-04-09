using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class PersonalDpsViewModel
{
    [RelayCommand]
    private void OpenDamageReferenceView()
    {
        _windowManagementService.DamageReferenceView.Show();
    }

    [RelayCommand]
    private void OpenSkillLog()
    {
        _logger?.LogInformation("打开技能日记窗口");
        _windowManagementService.SkillLogView.Show();
        _windowManagementService.SkillLogView.Activate();
    }

    [RelayCommand]
    private void OpenSkillBreakdownView()
    {
        try
        {
            var currentPlayerUid = _configManager.CurrentConfig.Uid;

            //Debug.Assert(currentPlayerUid != 0);
            if (currentPlayerUid == 0)
            {
                _logger?.LogWarning("无法打开技能详情：未找到玩家UID");
                _messageDialogService.Show("Error", "Cannot open skill analysis, current player uid not found");
                return;
            }

            var vm = _windowManagementService.SkillBreakdownView.DataContext as SkillBreakdownViewModel;
            if (vm == null)
            {
                _logger?.LogError("SkillBreakdownViewModel is null");
                return;
            }

            // 获取玩家统计数据
            var data = _engine.GetData()[StatisticType.Damage];
            if (!data.TryGetValue(currentPlayerUid, out var currPlayerData))
            {
                _logger?.LogWarning("未找到玩家 {UID} 的统计数据", currentPlayerUid);
                return;
            }
            var stats = currPlayerData.OriginalData;

            _logger?.LogInformation("Opening SkillBreakdownView for player {UID}", currentPlayerUid);

            // 尝试获取玩家信息（可能不存在）
            var playerInfos = _engine.GetPlayerInfoDictionary();
            if (!playerInfos.TryGetValue(currentPlayerUid, out var info))
            {
                _logger?.LogWarning("Cannot find player info for uid:{uid}", currentPlayerUid);
            }

            // 初始化 ViewModel
            vm.InitializeFrom(stats, info, StatisticType.Damage);

            // 显示窗口
            _windowManagementService.SkillBreakdownView.Show();
            _windowManagementService.SkillBreakdownView.Activate();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error opening skill breakdown view");
        }
    }

    [RelayCommand]
    private void ShowStatisticsAndHidePersonal()
    {
        _windowManagementService.DpsStatisticsView.Show();
        _windowManagementService.ClosePersonalDpsView();
    }


    /// <summary>
    /// Toggle window topmost state (command).
    /// Implemented by binding Window.Topmost to AppConfig.TopmostEnabled.
    /// </summary>
    [RelayCommand]
    private void ToggleTopmost()
    {
        AppConfig.TopmostEnabled = !AppConfig.TopmostEnabled;
    }

    [RelayCommand]
    private void Loaded()
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;

        // 订阅配置更新事件以响应主题颜色变化
        _configManager.ConfigurationUpdated += OnConfigurationUpdated;
        _engine.ProcessedDataReady += EngineOnProcessedDataReady;

        // ⭐ 从配置加载上次选择的木桩类型
        var savedDummyTarget = _configManager.CurrentConfig.DefaultDummyTarget;
        if (Enum.IsDefined(typeof(DummyTargetType), savedDummyTarget))
        {
            SelectedDummyTarget = savedDummyTarget;
            _logger?.LogInformation("从配置加载木桩类型: {Type}", SelectedDummyTarget);
        }

        _engine.Configure(new DataSourceEngineParam()
        {
            Mode = DataSourceMode.DummyTraining,
            DummyTarget = SelectedDummyTarget,
            PlayerUid = _configManager.CurrentConfig.Uid > 0 ? _configManager.CurrentConfig.Uid : -1,
        });

        StartTimer();
        _engine.CurrentSource.Refresh();
    }

    [RelayCommand]
    private void UnLoaded()
    {
        if (!_isLoaded)
        {
            return;
        }

        _isLoaded = false;

        _configManager.ConfigurationUpdated -= OnConfigurationUpdated;
        _engine.ProcessedDataReady -= EngineOnProcessedDataReady;
        _engine.Configure(new DataSourceEngineParam()
        {
            Mode = AppConfig.DpsUpdateMode.ToDataSourceMode(),
            ActiveUpdateInterval = AppConfig.DpsUpdateInterval,
        });
        StopTimer();
    }
}