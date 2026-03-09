using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Logging;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;
using System.Diagnostics;
using System.Windows;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DpsStatisticsViewModel
{
    [RelayCommand]
    private void Shutdown()
    {
        _appControlService.Shutdown();
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var about = _windowManagement.AboutView;
        about.ShowDialog();
        about.Activate();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManagement.SettingsView.Show();
        _windowManagement.SettingsView.Activate();
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
        foreach (var itm in StatisticData.Values)
        {
            itm.AddTestItem();
        }
        //UpdateData();
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

        TeamLabel = GetTeamLabel(StatisticIndex);
        CurrentPlayerLabel = GetCurrentPlayerLabel(StatisticIndex);
        TeamTotalLabel = GetTeamTotalLabel(StatisticIndex);

        StartBattleDurationUpdate();

        // Configure update mode based on settings
        ConfigureDpsUpdateMode();
    }


    [RelayCommand]
    private void OpenSkillLog()
    {
        var userUid = _storage.CurrentPlayerInfo.UID > 0 ? _storage.CurrentPlayerInfo.UID : _configManager.CurrentConfig.Uid;

        if (userUid <= 0)
        {
            // UID not configured, show prompt and open settings
            _logger.LogWarning(_localizationManager.GetString(ResourcesKeys.Warning_UidNotConfigured, defaultValue: "Tried to open skill log without UID configured"));

            _messageDialogService.Show(
                _localizationManager.GetString(ResourcesKeys.Dialog_UidRequired_Title, defaultValue: "Character UID required"),
                _localizationManager.GetString(ResourcesKeys.Dialog_UidRequired_Message2,
                    defaultValue: "Please configure your character UID in Settings before using skill log.\n\nHow to get UID: in game, the bottom-left player number is your UID."),
                _windowManagement.DpsStatisticsView);

            // Open settings page (character settings area)
            _windowManagement.SettingsView.Show();
            _windowManagement.SettingsView.Activate(); // Ensure window is brought to front

            return; // Don't open personal DPS window
        }

        // UID is configured, open personal DPS window normally
        _logger.LogInformation("{Message}, UID={Uid}",
            _localizationManager.GetString(ResourcesKeys.Command_OpenSkillLog, defaultValue: "Open skill log window"),
            userUid);
        _windowManagement.SkillLogView.Show();
        _windowManagement.SkillLogView.Activate();
    }

    [RelayCommand]
    private void OpenPersonalDpsView()
    {
        // Check if user has configured UID
        var userUid = _storage.CurrentPlayerInfo.UID > 0 ? _storage.CurrentPlayerInfo.UID : _configManager.CurrentConfig.Uid;

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

        var uid = target.Player.Uid;
        var scope = _dataSourceEngine.CurrentSource.Scope;

        var logs = _dataSourceEngine.CurrentSource.GetBattleLogsForPlayer(uid);
        PlayerStatistics? stats = null;

        // 1) Prefer live/raw stats when they still exist.
        // This preserves real-time updates before log save / section clear.
        var raw = _dataSourceEngine.CurrentSource.GetRawData();
        if (raw.TryGetValue(uid, out var liveStats))
        {
            stats = liveStats;
        }
        // 2) If live/raw stats are already gone, rebuild one detached snapshot from logs.
        // This is the expected "frozen graph" path after save / clear.
        else if (logs.Count > 0)
        {
            stats = SkillBreakdownReplayBuilder.BuildForPlayer(
                uid,
                logs,
                Math.Max(1, _storage.SampleRecordingInterval),
                Math.Clamp(AppConfig.TimeSeriesSampleCapacity, 50, 1000));
        }

        if (stats == null)
        {
            _logger.LogWarning("PlayerStatistics not found for UID {Uid} when opening SkillBreakdown", uid);
            return;
        }

        var playerInfo = _dataSourceEngine.GetPlayerInfoDictionary().TryGetValue(uid, out var info)
            ? info
            : null;

        vm.InitializeFrom(stats, playerInfo, StatisticIndex, logs, scope);
        _windowManagement.SkillBreakdownView.Show();
        _windowManagement.SkillBreakdownView.Activate();
    }
}
