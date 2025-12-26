using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;
using AppConfig = StarResonanceDpsAnalysis.WPF.Config.AppConfig;
using KeyBinding = StarResonanceDpsAnalysis.WPF.Models.KeyBinding;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class SettingsViewModel(
    IConfigManager configManager,
    IDeviceManagementService deviceManagementService,
    LocalizationManager localization,
    IMessageDialogService messageDialogService)
    : BaseViewModel
{
    [ObservableProperty] private AppConfig _appConfig = configManager.CurrentConfig.Clone(); // Initialized here with a cloned config; may be overwritten in LoadedAsync

    [ObservableProperty]
    private List<Option<Language>> _availableLanguages =
    [
        new(Language.Auto, Language.Auto.GetLocalizedDescription()),
        new(Language.ZhCn, Language.ZhCn.GetLocalizedDescription()),
        new(Language.EnUs, Language.EnUs.GetLocalizedDescription()),
        new(Language.PtBr, Language.PtBr.GetLocalizedDescription()),
        new(Language.KoKr, Language.KoKr.GetLocalizedDescription())
    ];

    [ObservableProperty] private List<NetworkAdapterInfo> _availableNetworkAdapters = [];

    [ObservableProperty]
    private List<Option<NumberDisplayMode>> _availableNumberDisplayModes =
    [
        new(NumberDisplayMode.Wan, NumberDisplayMode.Wan.GetLocalizedDescription()),
        new(NumberDisplayMode.KMB, NumberDisplayMode.KMB.GetLocalizedDescription())
    ];

    private bool _cultureHandlerSubscribed;
    private bool _networkHandlerSubscribed;
    private bool _isLoaded; // becomes true after LoadedAsync completes
    private bool _hasUnsavedChanges; // tracks whether any property changed after load

    // Store original values for cancel/restore
    private AppConfig _originalConfig = null!;

    [ObservableProperty] private Option<Language>? _selectedLanguage;
    [ObservableProperty] private Option<NumberDisplayMode>? _selectedNumberDisplayMode;


    /// <summary>
    /// 格式字符串预览
    /// </summary>
    public string FormatPreview
    {
        get
        {
            if (!AppConfig.UseCustomFormat) return "Custom format is disabled. Using field visibility settings.";
            
            // 创建一个示例 PlayerInfoViewModel 来生成预览
            var previewVm = new PlayerInfoViewModel(localization)
            {
                Name = "PlayerName",
                Spec = ClassSpec.FrostMageIcicle,
                PowerLevel = 25000,
                SeasonStrength = 8,
                SeasonLevel = 50,
                Guild = "MyGuild",
                Uid = 123456789,
                Mask = false,
                UseCustomFormat = true,
                FormatString = AppConfig.PlayerInfoFormatString
            };
            
            // Trigger the update to generate the formatted string
            return previewVm.PlayerInfo;
        }
    }

    /// <summary>
    /// 可用的格式化字段列表
    /// </summary>
    public List<FormatFieldOption> AvailableFormatFields { get; } = new()
    {
        new FormatFieldOption("Name", "Player Name", "{Name}", "e.g., PlayerName"),
        new FormatFieldOption("Spec", "Class Spec", "{Spec}", "e.g., FrostMage"),
        new FormatFieldOption("PowerLevel", "Power Level", "{PowerLevel}", "e.g., 25000"),
        new FormatFieldOption("SeasonStrength", "Season Strength", "{SeasonStrength}", "e.g., 8"),
        new FormatFieldOption("SeasonLevel", "Season Level", "{SeasonLevel}", "e.g., 50"),
        new FormatFieldOption("Guild", "Guild Name", "{Guild}", "e.g., MyGuild"),
        new FormatFieldOption("Uid", "Player UID", "{Uid}", "e.g., 123456789"),
    };

    /// <summary>
    /// 常用分隔符列表
    /// </summary>
    public List<string> CommonSeparators { get; } = new()
    {
        " - ",
        " | ",
        " / ",
        " • ",
        ", ",
        " ",
    };

    /// <summary>
    /// 添加字段到格式字符串
    /// </summary>
    [RelayCommand]
    private void AddFieldToFormat(FormatFieldOption? field)
    {
        if (field == null) return;
        
        AppConfig.PlayerInfoFormatString += field.Placeholder;
        AppConfig.UseCustomFormat = true;
        OnPropertyChanged(nameof(FormatPreview));
    }

    /// <summary>
    /// 添加分隔符到格式字符串
    /// </summary>
    [RelayCommand]
    private void AddSeparatorToFormat(string? separator)
    {
        if (string.IsNullOrEmpty(separator)) return;
        
        AppConfig.PlayerInfoFormatString += separator;
        OnPropertyChanged(nameof(FormatPreview));
    }

    /// <summary>
    /// 清空格式字符串
    /// </summary>
    [RelayCommand]
    private void ClearFormat()
    {
        AppConfig.PlayerInfoFormatString = string.Empty;
        OnPropertyChanged(nameof(FormatPreview));
    }

    /// <summary>
    /// 设置格式字符串预设
    /// </summary>
    [RelayCommand]
    private void SetPlayerInfoFormatPreset(string preset)
    {
        if (!string.IsNullOrEmpty(preset))
        {
            AppConfig.PlayerInfoFormatString = preset;
            AppConfig.UseCustomFormat = true;
            OnPropertyChanged(nameof(FormatPreview));
        }
    }

    public event Action? RequestClose;

    partial void OnAppConfigChanging(AppConfig value)
    {
        // Unsubscribe from the old instance before changing
        _appConfig.PropertyChanged -= OnAppConfigPropertyChanged;
    }

    partial void OnAppConfigChanged(AppConfig value)
    {
        // Subscribe to the new instance
        value.PropertyChanged += OnAppConfigPropertyChanged;

        localization.ApplyLanguage(value.Language);
        UpdateLanguageDependentCollections();
        SyncOptions();
    }

    partial void OnSelectedNumberDisplayModeChanged(Option<NumberDisplayMode>? value)
    {
        if (value == null) return;
        AppConfig.DamageDisplayType = value.Value;
    }

    partial void OnSelectedLanguageChanged(Option<Language>? value)
    {
        if (value == null) return;
        AppConfig.Language = value.Value;
        localization.ApplyLanguage(value.Value);
    }

    partial void OnAvailableNetworkAdaptersChanged(List<NetworkAdapterInfo> value)
    {
        AppConfig.PreferredNetworkAdapter ??= value.FirstOrDefault();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadedAsync()
    {
        // Clone current config for editing
        AppConfig = configManager.CurrentConfig.Clone();

        // Store original config for cancel/restore (deep clone)
        _originalConfig = configManager.CurrentConfig.Clone();

        SubscribeHandlers();

        UpdateLanguageDependentCollections();
        localization.ApplyLanguage(AppConfig.Language);
        await LoadNetworkAdaptersAsync();

        _hasUnsavedChanges = false;
        _isLoaded = true;
    }

    private void SubscribeHandlers()
    {
        if (!_cultureHandlerSubscribed)
        {
            localization.CultureChanged += OnCultureChanged;
            _cultureHandlerSubscribed = true;
        }

        if (!_networkHandlerSubscribed)
        {
            NetworkChange.NetworkAvailabilityChanged += OnSystemNetworkChanged;
            NetworkChange.NetworkAddressChanged += OnSystemNetworkChanged;
            _networkHandlerSubscribed = true;
        }
    }

    private async Task LoadNetworkAdaptersAsync()
    {
        var adapters = await deviceManagementService.GetNetworkAdaptersAsync();
        AvailableNetworkAdapters = adapters.Select(a => new NetworkAdapterInfo(a.name, a.description)).ToList();
        AppConfig.PreferredNetworkAdapter =
            AvailableNetworkAdapters.FirstOrDefault(a => a.Name == AppConfig.PreferredNetworkAdapter?.Name);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task NetworkAdapterAutoSelect()
    {
        var ret = await deviceManagementService.GetAutoSelectedNetworkAdapterAsync();
        if (ret != null)
        {
            AppConfig.PreferredNetworkAdapter = ret;
            deviceManagementService.SetActiveNetworkAdapter(ret);
            return;
        }
        MessageBox.Show(localization.GetString(ResourcesKeys.Settings_NetworkAdapterAutoSelect_Failed)); // Temporary message dialog
    }

    private async void OnSystemNetworkChanged(object? sender, EventArgs e)
    {
        try
        {
            await LoadNetworkAdaptersAsync();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Handle shortcut key input for mouse through shortcut
    /// </summary>
    [RelayCommand]
    private void HandleMouseThroughShortcut(object parameter)
    {
        if (parameter is KeyEventArgs e)
        {
            HandleShortcutInput(e, ShortcutType.MouseThrough);
        }
    }

    /// <summary>
    /// Handle shortcut key input for clear data shortcut
    /// </summary>
    /// <param name="parameter">KeyEventArgs from the view</param>
    [RelayCommand]
    private void HandleClearDataShortcut(object parameter)
    {
        if (parameter is KeyEventArgs e)
        {
            HandleShortcutInput(e, ShortcutType.ClearData);
        }
    }

    [RelayCommand]
    private void HandleTopMostShortcut(object parameter)
    {
        if (parameter is KeyEventArgs e)
        {
            HandleShortcutInput(e, ShortcutType.TopMost);
        }
    }

    private void OnAppConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not AppConfig config)
        {
            return;
        }

        if (e.PropertyName == nameof(AppConfig.Language))
        {
            localization.ApplyLanguage(config.Language);
            UpdateLanguageDependentCollections();
        }
        else if (e.PropertyName == nameof(AppConfig.MaskPlayerName) && _isLoaded && !config.MaskPlayerName)
        {
            var title = localization.GetString(ResourcesKeys.Settings_PlayerNameMask_Warning_Title);
            var message = localization.GetString(ResourcesKeys.Settings_PlayerNameMask_Warning_Message);
            var result = messageDialogService.Show(title, message);
            if (result != true)
            {
                config.MaskPlayerName = true;
            }
        }
        else if (e.PropertyName == nameof(AppConfig.PreferredNetworkAdapter))
        {
            var adapter = AppConfig.PreferredNetworkAdapter;
            if (adapter != null)
            {
                deviceManagementService.SetActiveNetworkAdapter(adapter);
            }
        }
        else if (e.PropertyName == nameof(AppConfig.Opacity))
        {
            // Real-time preview: immediately apply opacity to the actual config
            if (_isLoaded)
            {
                ApplyOpacityImmediately(config.Opacity);
            }
        }
        else if (e.PropertyName is nameof(AppConfig.PlayerInfoFormatString) or nameof(AppConfig.UseCustomFormat))
        {
            // Update format string preview only (no real-time application to actual config)
            OnPropertyChanged(nameof(FormatPreview));
        }

        if (_isLoaded)
        {
            _hasUnsavedChanges = true;
        }
    }

    /// <summary>
    /// Immediately apply opacity change to the running application config for real-time preview
    /// </summary>
    private void ApplyOpacityImmediately(double opacity)
    {
        // Update the actual application config (not just the clone)
        // This allows real-time preview while still supporting cancel
        configManager.CurrentConfig.Opacity = opacity;
    }

    /// <summary>
    /// Generic shortcut input handler
    /// </summary>
    private void HandleShortcutInput(KeyEventArgs e, ShortcutType shortcutType)
    {
        e.Handled = true; // we'll handle the key

        var modifiers = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Allow Delete to clear
        if (key == Key.Delete)
        {
            ClearShortcut(shortcutType);
            return;
        }

        // Ignore modifier-only presses
        if (key.IsControlKey() || key.IsAltKey() || key.IsShiftKey())
        {
            return;
        }

        UpdateShortcut(shortcutType, key, modifiers);
    }

    /// <summary>
    /// Update a specific shortcut
    /// </summary>
    private void UpdateShortcut(ShortcutType shortcutType, Key key, ModifierKeys modifiers)
    {
        var shortcutData = new KeyBinding(key, modifiers);

        switch (shortcutType)
        {
            case ShortcutType.MouseThrough:
                AppConfig.MouseThroughShortcut = shortcutData;
                break;
            case ShortcutType.ClearData:
                AppConfig.ClearDataShortcut = shortcutData;
                break;
            case ShortcutType.TopMost:
                AppConfig.TopmostShortcut = shortcutData;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shortcutType), shortcutType, null);
        }
    }

    /// <summary>
    /// Clear a specific shortcut
    /// </summary>
    private void ClearShortcut(ShortcutType shortcutType)
    {
        var shortCut = new KeyBinding(Key.None, ModifierKeys.None);
        switch (shortcutType)
        {
            case ShortcutType.MouseThrough:
                AppConfig.MouseThroughShortcut = shortCut;
                break;
            case ShortcutType.ClearData:
                AppConfig.ClearDataShortcut = shortCut;
                break;
            case ShortcutType.TopMost:
                AppConfig.TopmostShortcut = shortCut;
                break;
        }
    }

    public Task ApplySettingsAsync()
    {
        return configManager.SaveAsync(AppConfig);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        await ApplySettingsAsync();
        UnsubscribeHandlers();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task Cancel()
    {
        if (!_hasUnsavedChanges)
        {
            UnsubscribeHandlers();
            RequestClose?.Invoke();
            return;
        }

        var title = localization.GetString(ResourcesKeys.Settings_CancelConfirm_Title);
        var message = localization.GetString(ResourcesKeys.Settings_CancelConfirm_Message);

        var result = messageDialogService.Show(title, message);
        if (result == true)
        {
            // User chose to discard changes - restore original config
            RestoreOriginalConfig();

            _hasUnsavedChanges = false;
            UnsubscribeHandlers();
            RequestClose?.Invoke();
        }
    }

    /// <summary>
    /// Restore the original config when user cancels
    /// </summary>
    private void RestoreOriginalConfig()
    {
        if (_originalConfig == null) return;

        // Restore opacity to original value
        configManager.CurrentConfig.Opacity = _originalConfig.Opacity;
        
        // Restore player info format settings
        configManager.CurrentConfig.UseCustomFormat = _originalConfig.UseCustomFormat;
        configManager.CurrentConfig.PlayerInfoFormatString = _originalConfig.PlayerInfoFormatString;
    }

    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        UpdateLanguageDependentCollections();
    }

    private void UnsubscribeHandlers()
    {
        if (_cultureHandlerSubscribed)
        {
            localization.CultureChanged -= OnCultureChanged;
            _cultureHandlerSubscribed = false;
        }

        if (_networkHandlerSubscribed)
        {
            NetworkChange.NetworkAvailabilityChanged -= OnSystemNetworkChanged;
            NetworkChange.NetworkAddressChanged -= OnSystemNetworkChanged;
            _networkHandlerSubscribed = false;
        }
    }
}

public partial class SettingsViewModel
{
    private static void UpdateEnumList<T>(IEnumerable<Option<T>> list) where T : Enum
    {
        foreach (var itm in list)
        {
            itm.Display = itm.Value.GetLocalizedDescription();
        }
    }

    private void UpdateLanguageDependentCollections()
    {
        UpdateEnumList(AvailableNumberDisplayModes);
        UpdateEnumList(AvailableLanguages);
    }

    private void SyncLanguageOption()
    {
        var (ret, opt) = SyncOption(SelectedLanguage, AvailableLanguages, AppConfig.Language);
        if (ret) SelectedLanguage = opt!;
    }

    private void SyncNumberDisplayModeOption()
    {
        var (ret, opt) = SyncOption(SelectedNumberDisplayMode, AvailableNumberDisplayModes,
            AppConfig.DamageDisplayType);
        if (ret) SelectedNumberDisplayMode = opt!;
    }

    private void SyncOptions()
    {
        SyncLanguageOption();
        SyncNumberDisplayModeOption();
    }

    private static (bool result, Option<T>? opt) SyncOption<T>(Option<T>? option, List<Option<T>> availableList,
        T origin)
    {
        if (Equal(option, origin)) return (false, null);

        var match = availableList.FirstOrDefault(l => Equal(l, origin));
        Debug.Assert(match != null);
        return (true, match);

        bool Equal(Option<T>? o1, T o2)
        {
            return o1?.Value?.Equals(o2) ?? false;
        }
    }
}

public partial class Option<T>(T value, string display) : BaseViewModel
{
    [ObservableProperty] private string _display = display;
    [ObservableProperty] private T _value = value;

    public void Deconstruct(out T value, out string display)
    {
        value = Value;
        display = Display;
    }
}

/// <summary>
/// Enum to identify shortcut types
/// </summary>
public enum ShortcutType
{
    MouseThrough,
    ClearData,
    TopMost
}

public sealed class SettingsDesignTimeViewModel : SettingsViewModel
{
    public SettingsDesignTimeViewModel() : base(new DesignConfigManager(), new DesignTimeDeviceManagementService(), new LocalizationManager(new LocalizationConfiguration(), NullLogger<LocalizationManager>.Instance), new DesignMessageDialogService())
    {
        AppConfig = new AppConfig
        {
            // set friendly defaults shown in designer
            Opacity = 85,
            CombatTimeClearDelay = 5,
            ClearLogAfterTeleport = false,
            Language = Language.Auto
        };

        AvailableNetworkAdapters = new List<NetworkAdapterInfo>
        {
            new NetworkAdapterInfo("WAN Adapter", "WAN"),
            new NetworkAdapterInfo("WLAN Adapter", "WLAN")
        };

        AppConfig.MouseThroughShortcut = new KeyBinding(Key.F6, ModifierKeys.Control);
        AppConfig.ClearDataShortcut = new KeyBinding(Key.F9, ModifierKeys.None);

        AvailableLanguages = new List<Option<Language>>
        {
            new Option<Language>(Language.Auto, "Follow System"),
            new Option<Language>(Language.ZhCn, "中文 (简体)"),
            new Option<Language>(Language.EnUs, "English")
        };

        AvailableNumberDisplayModes = new List<Option<NumberDisplayMode>>
        {
            new Option<NumberDisplayMode>(NumberDisplayMode.Wan, "四位计数法 (万)"),
            new Option<NumberDisplayMode>(NumberDisplayMode.KMB, "三位计数法 (KMB)")
        };

        SelectedLanguage = AvailableLanguages[0];
        SelectedNumberDisplayMode = AvailableNumberDisplayModes[0];
    }
}

internal sealed class DesignMessageDialogService : IMessageDialogService
{
    public bool? Show(string title, string content, Window? owner = null) => true;
}

/// <summary>
/// 格式字段选项
/// </summary>
public class FormatFieldOption
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Placeholder { get; set; }
    public string Example { get; set; }

    public FormatFieldOption(string id, string displayName, string placeholder, string example)
    {
        Id = id;
        DisplayName = displayName;
        Placeholder = placeholder;
        Example = example;
    }
}
