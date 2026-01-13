using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Logging;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DpsStatisticsViewModel
{
    [RelayCommand]
    private void Shutdown()
    {
        _appControlService.Shutdown();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManagement.SettingsView.Show();
    }

    [RelayCommand]
    private void Refresh()
    {
        _logger.LogDebug(WpfLogEvents.VmRefresh, "Manual refresh requested");

        try
        {
            UpdateData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh DPS statistics");
        }
    }


    [RelayCommand]
    private void OpenContextMenu()
    {
        ShowContextMenu = true;
    }

    [RelayCommand]
    private void MinimizeWindow()
    {
        _windowManagement.DpsStatisticsView.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    private void NextMetricType()
    {
        StatisticIndex = StatisticIndex.Next();
    }

    [RelayCommand]
    private void PreviousMetricType()
    {
        StatisticIndex = StatisticIndex.Previous();
    }

    [RelayCommand]
    private void ToggleScopeTime()
    {
        ScopeTime = ScopeTime.Next();
    }

    [RelayCommand]
    public void AddRandomData()
    {
        UpdateData();
    }

    [RelayCommand]
    private void SetSkillDisplayLimit(int limit)
    {
        var clampedLimit = Math.Max(0, limit);
        _logger.LogDebug("SetSkillDisplayLimit: 修改技能显示条数为 {Limit}", clampedLimit);

        foreach (var vm in StatisticData.Values)
        {
            vm.SkillDisplayLimit =
                clampedLimit; // Displayed skill count will be changed after SkillDisplayLimit is set
        }

        _configManager.CurrentConfig.SkillDisplayLimit = clampedLimit;
        _ = _configManager.SaveAsync();
        _logger.LogDebug("技能显示数量已保存到配置: {Limit}", clampedLimit);

        // Notify that current data's SkillDisplayLimit changed
        OnPropertyChanged(nameof(CurrentStatisticData));

        _logger.LogDebug("SetSkillDisplayLimit: 技能显示条数已更新,所有slot的FilteredSkillList已刷新");
    }

    [RelayCommand]
    private void OnUnloaded()
    {
        _logger.LogDebug("DpsStatisticsViewModel OnUnloaded");
    }

    [RelayCommand]
    private void OnResize()
    {
        _logger.LogDebug("Window Resized");
    }

    [RelayCommand]
    private void OnLoaded()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        foreach (var vm in StatisticData.Values)
        {
            vm.Initialized = true;
        }

        _logger.LogDebug(WpfLogEvents.VmLoaded, "DpsStatisticsViewModel loaded");
        LoadPlayerCache();

        EnsureDurationTimerStarted();
        UpdateBattleDuration();

        // Configure update mode based on settings
        ConfigureDpsUpdateMode();
    }


    [RelayCommand]
    private void OpenSkillLog()
    {
        _logger.LogInformation("打开技能日记窗口");
        _windowManagement.SkillLogView.Show();
        _windowManagement.SkillLogView.Activate();
    }

    [RelayCommand]
    private void OpenPersonalDpsView()
    {
        // 检查用户是否设置了UID
        var userUid = _configManager.CurrentConfig.Uid;

        if (userUid <= 0)
        {
            // UID未设置,弹出提示并打开设置页面
            _logger.LogWarning("尝试打开个人打桩模式但UID未设置");

            _messageDialogService.Show(
                "需要设置角色UID",
                "请先在设置中配置您的角色UID，才能使用个人打桩模式。\n\n如何获取UID：进入游戏后，左下角玩家编号就是UID",
                _windowManagement.DpsStatisticsView);

            // 打开设置页面(角色设置区域)
            _windowManagement.SettingsView.Show();
            _windowManagement.SettingsView.Activate(); // 确保窗口激活到前台

            return; // 不打开个人打桩窗口
        }

        // UID已设置,正常打开个人打桩窗口
        _logger.LogInformation("打开个人打桩模式, UID={Uid}", userUid);
        _windowManagement.PersonalDpsView.Show();
        _windowManagement.DpsStatisticsView.Hide();
    }

    /// <summary>
    /// 切换窗口置顶状态（命令）。
    /// 通过绑定 Window.Topmost 到 AppConfig.TopmostEnabled 实现。
    /// </summary>
    [RelayCommand]
    private async Task ToggleTopmost()
    {
        AppConfig.TopmostEnabled = !AppConfig.TopmostEnabled;
        try
        {
            await _configManager.SaveAsync(AppConfig);
        }
        catch (InvalidOperationException ex)
        {
            // Ignore
            _logger.LogError(ex, "Failed to save AppConfig");
        }
    }

    [RelayCommand]
    private void OpenSkillBreakdown(StatisticDataViewModel? slot)
    {
        var target = slot ?? CurrentStatisticData.SelectedSlot;
        if (target is null) return;

        var vm = _windowManagement.SkillBreakdownView.DataContext as SkillBreakdownViewModel;
        Debug.Assert(vm != null, "vm!=null");

        var playerStats = _storage.GetStatistics(ScopeTime == Models.ScopeTime.Total);
        if (!playerStats.TryGetValue(target.Player.Uid, out var stats)) return;
        _logger.LogInformation("Using PlayerStatistics for SkillBreakdown (accurate data)");

        var playerInfo = _storage.ReadOnlyPlayerInfoDatas.TryGetValue(target.Player.Uid, out var info)
            ? info
            : null;

        vm.InitializeFrom(stats, playerInfo, StatisticIndex, target);
        _windowManagement.SkillBreakdownView.Show();
        _windowManagement.SkillBreakdownView.Activate();
    }
}