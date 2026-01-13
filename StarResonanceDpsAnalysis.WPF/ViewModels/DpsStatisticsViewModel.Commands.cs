using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
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
        _logger.LogDebug("SetSkillDisplayLimit: {Message} {Limit}", 
            _localizationManager.GetString("Common_SkillDisplayLimitChanged", defaultValue: "修改技能显示条数为"), 
            clampedLimit);

        foreach (var vm in StatisticData.Values)
        {
            vm.SkillDisplayLimit =
                clampedLimit; // Displayed skill count will be changed after SkillDisplayLimit is set
        }

        _configManager.CurrentConfig.SkillDisplayLimit = clampedLimit;
        _ = _configManager.SaveAsync();
        _logger.LogDebug("{Message} {Limit}", 
            _localizationManager.GetString("Common_SkillDisplayLimitSaved", defaultValue: "技能显示数量已保存到配置:"), 
            clampedLimit);

        // Notify that current data's SkillDisplayLimit changed
        OnPropertyChanged(nameof(CurrentStatisticData));

        _logger.LogDebug("SetSkillDisplayLimit: {Message}", 
            _localizationManager.GetString("Common_SkillListRefreshed", defaultValue: "技能显示条数已更新,所有slot的FilteredSkillList已刷新"));
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
        _logger.LogInformation(_localizationManager.GetString("Command_OpenSkillLog", defaultValue: "打开技能日记窗口"));
        _windowManagement.SkillLogView.Show();
        _windowManagement.SkillLogView.Activate();
    }

    [RelayCommand]
    private void OpenPersonalDpsView()
    {
        // Check if user has configured UID
        var userUid = _storage.CurrentPlayerUUID > 0 ? _storage.CurrentPlayerUUID : _configManager.CurrentConfig.Uid;

        if (userUid <= 0)
        {
            // UID not configured, show prompt and open settings
            _logger.LogWarning(_localizationManager.GetString("Warning_UidNotConfigured", defaultValue: "尝试打开个人打桩模式但UID未设置"));

            _messageDialogService.Show(
                _localizationManager.GetString("Dialog_UidRequired_Title", defaultValue: "需要设置角色UID"),
                _localizationManager.GetString("Dialog_UidRequired_Message", 
                    defaultValue: "请先在设置中配置您的角色UID，才能使用个人打桩模式。\n\n如何获取UID：进入游戏后，左下角玩家编号就是UID"),
                _windowManagement.DpsStatisticsView);

            // Open settings page (character settings area)
            _windowManagement.SettingsView.Show();
            _windowManagement.SettingsView.Activate(); // Ensure window is brought to front

            return; // Don't open personal DPS window
        }

        // UID is configured, open personal DPS window normally
        _logger.LogInformation("{Message}, UID={Uid}", 
            _localizationManager.GetString("Info_OpeningPersonalDps", defaultValue: "打开个人打桩模式"), 
            userUid);
        _windowManagement.PersonalDpsView.Show();
        _windowManagement.DpsStatisticsView.Hide();
    }

    /// <summary>
    /// Toggle window topmost state (command).
    /// Implemented by binding Window.Topmost to AppConfig.TopmostEnabled.
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