using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Properties;

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
            _localizationManager.GetString(ResourcesKeys.Common_SkillDisplayLimitChanged, defaultValue: "Skill display limit set to"),
            clampedLimit);

        foreach (var vm in StatisticData.Values)
        {
            vm.SkillDisplayLimit =
                clampedLimit; // Displayed skill count will be changed after SkillDisplayLimit is set
        }

        _configManager.CurrentConfig.SkillDisplayLimit = clampedLimit;
        _ = _configManager.SaveAsync();
        _logger.LogDebug("{Message} {Limit}", 
            _localizationManager.GetString(ResourcesKeys.Common_SkillDisplayLimitSaved, defaultValue: "Skill display limit saved to config:"),
            clampedLimit);

        // Notify that current data's SkillDisplayLimit changed
        OnPropertyChanged(nameof(CurrentStatisticData));

        _logger.LogDebug("SetSkillDisplayLimit: {Message}", 
            _localizationManager.GetString(ResourcesKeys.Common_SkillListRefreshed, defaultValue: "Skill list refreshed for all slots"));
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
        _logger.LogInformation(_localizationManager.GetString(ResourcesKeys.Command_OpenSkillLog, defaultValue: "Open skill log window"));
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
            _logger.LogWarning(_localizationManager.GetString(ResourcesKeys.Warning_UidNotConfigured, defaultValue: "Tried to open personal training mode without UID configured"));

            _messageDialogService.Show(
                _localizationManager.GetString(ResourcesKeys.Dialog_UidRequired_Title, defaultValue: "Character UID required"),
                _localizationManager.GetString(ResourcesKeys.Dialog_UidRequired_Message,
                    defaultValue: "Please configure your character UID in Settings before using personal training mode.\n\nHow to get UID: in game, the bottom-left player number is your UID."),
                _windowManagement.DpsStatisticsView);

            // Open settings page (character settings area)
            _windowManagement.SettingsView.Show();
            _windowManagement.SettingsView.Activate(); // Ensure window is brought to front

            return; // Don't open personal DPS window
        }

        // UID is configured, open personal DPS window normally
        _logger.LogInformation("{Message}, UID={Uid}", 
            _localizationManager.GetString(ResourcesKeys.Info_OpeningPersonalDps, defaultValue: "Opening personal training mode"),
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
