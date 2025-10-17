using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.Core.Extends.System;
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
    IDeviceManagementService deviceManagementService)
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

    [ObservableProperty] private Option<Language>? _selectedLanguage;
    [ObservableProperty] private Option<NumberDisplayMode>? _selectedNumberDisplayMode;

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

        LocalizationManager.ApplyLanguage(value.Language);
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
        LocalizationManager.ApplyLanguage(value.Value);
    }

    partial void OnAvailableNetworkAdaptersChanged(List<NetworkAdapterInfo> value)
    {
        AppConfig.PreferredNetworkAdapter ??= value.FirstOrDefault();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadedAsync()
    {
        AppConfig = configManager.CurrentConfig.Clone();

        SubscribeHandlers();

        UpdateLanguageDependentCollections();
        LocalizationManager.ApplyLanguage(AppConfig.Language);
        await LoadNetworkAdaptersAsync();
    }

    private void SubscribeHandlers()
    {
        if (!_cultureHandlerSubscribed)
        {
            LocalizationManager.CultureChanged += OnCultureChanged;
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
        MessageBox.Show(LocalizationManager.GetString(ResourcesKeys.Settings_NetworkAdapterAutoSelect_Failed)); // Temporary message dialog
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
            LocalizationManager.ApplyLanguage(config.Language);
            UpdateLanguageDependentCollections();
        }
        else if (e.PropertyName == nameof(AppConfig.PreferredNetworkAdapter))
        {
            var adapter = AppConfig.PreferredNetworkAdapter;
            if (adapter != null)
            {
                deviceManagementService.SetActiveNetworkAdapter(adapter);
            }
        }
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
    private void Cancel()
    {
        UnsubscribeHandlers();
        RequestClose?.Invoke();
    }

    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        UpdateLanguageDependentCollections();
    }

    private void UnsubscribeHandlers()
    {
        if (_cultureHandlerSubscribed)
        {
            LocalizationManager.CultureChanged -= OnCultureChanged;
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
    public SettingsDesignTimeViewModel() : base(new DesignConfigManager(), new DesignTimeDeviceManagementService())
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
// public sealed class SettingsDesignTimeViewModel : BaseViewModel
// {
//     public SettingsDesignTimeViewModel()
//     {
//         AppConfig = new AppConfig
//         {
//             // set friendly defaults shown in designer
//             Opacity = 85,
//             CombatTimeClearDelay = 5,
//             ClearLogAfterTeleport = false,
//             Language = Language.Auto
//         };
//
//         AvailableNetworkAdapters = new List<NetworkAdapterInfo>
//         {
//             new NetworkAdapterInfo("WAN Adapter", "WAN"),
//             new NetworkAdapterInfo("WLAN Adapter", "WLAN")
//         };
//
//         AppConfig.MouseThroughShortcut = new KeyBinding(Key.F6, ModifierKeys.Control);
//         AppConfig.ClearDataShortcut = new KeyBinding(Key.F9, ModifierKeys.None);
//
//         AvailableLanguages = new List<Option<Language>>
//         {
//             new Option<Language>(Language.Auto, "Follow System"),
//             new Option<Language>(Language.ZhCn, "中文 (简体)"),
//             new Option<Language>(Language.EnUs, "English")
//         };
//
//         AvailableNumberDisplayModes = new List<Option<NumberDisplayMode>>
//         {
//             new Option<NumberDisplayMode>(NumberDisplayMode.Wan, "四位计数法 (万)"),
//             new Option<NumberDisplayMode>(NumberDisplayMode.KMB, "三位计数法 (KMB)")
//         };
//
//         SelectedLanguage = AvailableLanguages[0];
//         SelectedNumberDisplayMode = AvailableNumberDisplayModes[0];
//     }
//
//     public AppConfig AppConfig { get; set; }
//
//     public List<NetworkAdapterInfo> AvailableNetworkAdapters { get; set; }
//
//     public List<Option<Language>> AvailableLanguages { get; set; }
//
//     public List<Option<NumberDisplayMode>> AvailableNumberDisplayModes { get; set; }
//
//     public Option<Language>? SelectedLanguage { get; set; }
//
//     public Option<NumberDisplayMode>? SelectedNumberDisplayMode { get; set; }
// }