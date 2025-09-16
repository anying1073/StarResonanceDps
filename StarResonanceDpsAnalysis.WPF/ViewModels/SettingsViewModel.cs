using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Converters;
using StarResonanceDpsAnalysis.WPF.Models;
using AppConfig = StarResonanceDpsAnalysis.WPF.Config.AppConfig;
using KeyBinding = StarResonanceDpsAnalysis.WPF.Models.KeyBinding;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class SettingsViewModel(IConfigManager configManger, IDeviceManager deviceManager) : BaseViewModel
{
    [ObservableProperty] private AppConfig _appConfig = null!;
    [ObservableProperty] private List<NetworkAdapterInfo> _availableNetworkAdapters = [];

    public List<NumberDisplayMode> AvailableNumberDisplayModes { get; } =
        [NumberDisplayMode.Wan, NumberDisplayMode.KMB];

    public event Action? RequestClose;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadedAsync()
    {
        AppConfig = configManger.CurrentConfig.Clone();
        await LoadNetworkAdaptersAsync();
    }

    private async Task LoadNetworkAdaptersAsync()
    {
        var adapters = await deviceManager.GetNetworkAdaptersAsync();
        AvailableNetworkAdapters = adapters.Select(a => new NetworkAdapterInfo(a.name, a.description)).ToList();
        AppConfig.PreferredNetworkAdapter =
            AvailableNetworkAdapters.FirstOrDefault(a => a.Name == AppConfig.PreferredNetworkAdapter?.Name);
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

    /// <summary>
    /// Generic shortcut input handler
    /// </summary>
    private void HandleShortcutInput(KeyEventArgs e, ShortcutType shortcutType)
    {
        e.Handled = true; // we'll handle the key

        var modifiers = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Allow Delete to clear - same logic as WinForms
        if (key == Key.Delete)
        {
            ClearShortcut(shortcutType);
            return;
        }

        // Ignore modifier-only presses - same logic as WinForms
        if (key.IsControlKey() || key.IsAltKey() || key.IsShiftKey())
        {
            return;
        }

        // Exclude physical modifier keys from being shown as main key
        if (!key.IsControlKey() && !key.IsAltKey() && !key.IsShiftKey())
        {
            UpdateShortcut(shortcutType, key, modifiers);
        }
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

    public void ApplySettings()
    {
        configManger.SaveAsync(AppConfig);
    }

    [RelayCommand]
    private void Confirm()
    {
        ApplySettings();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}

/// <summary>
/// Enum to identify shortcut types
/// </summary>
public enum ShortcutType
{
    MouseThrough,
    ClearData
}

public sealed class SettingsDesignTimeViewModel : SettingsViewModel
{
    public SettingsDesignTimeViewModel() : base(null!, null!)
    {
        AppConfig = new AppConfig();
        AvailableNetworkAdapters =
        [
            new NetworkAdapterInfo("WAN Adapter", "WAN"),
            new NetworkAdapterInfo("WLAN Adapter", "WLAN")
        ];
        AppConfig.MouseThroughShortcut = new KeyBinding(Key.F6, ModifierKeys.Control);
        AppConfig.ClearDataShortcut = new KeyBinding(Key.F9, ModifierKeys.None);
    }
}