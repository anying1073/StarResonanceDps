using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;
using AppConfig = StarResonanceDpsAnalysis.WPF.Config.AppConfig;
using KeyBinding = StarResonanceDpsAnalysis.WPF.Models.KeyBinding;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    [ObservableProperty] private AppConfig _appConfig; // Initialized here with a cloned config; may be overwritten in LoadedAsync

    [ObservableProperty]
    private ObservableCollection<ClassColorSettingViewModel> _classColorSettings = new();

    [ObservableProperty]
    private List<Option<Language>> _availableLanguages =
    [
        new(Language.Auto, Language.Auto.GetLocalizedDescription()),
        new(Language.ZhCn, Language.ZhCn.GetLocalizedDescription()),
        new(Language.EnUs, Language.EnUs.GetLocalizedDescription()),
        new(Language.PtBr, Language.PtBr.GetLocalizedDescription()),
        new(Language.JaJp, Language.JaJp.GetLocalizedDescription()),
        new(Language.KoKr, Language.KoKr.GetLocalizedDescription())
    ];

    [ObservableProperty] private List<NetworkAdapterInfo> _availableNetworkAdapters = [];

    [ObservableProperty]
    private List<Option<NumberDisplayMode>> _availableNumberDisplayModes =
    [
        new(NumberDisplayMode.Wan, NumberDisplayMode.Wan.GetLocalizedDescription()),
        new(NumberDisplayMode.KMB, NumberDisplayMode.KMB.GetLocalizedDescription())
    ];

    [ObservableProperty]
    private List<FormatFieldOption> _availableFormatFields = new();

    private bool _cultureHandlerSubscribed;
    private bool _networkHandlerSubscribed;
    private bool _dataStorageHandlerSubscribed;
    private bool _isLoaded; // becomes true after LoadedAsync completes
    private bool _hasUnsavedChanges; // tracks whether any property changed after load
    private bool _suppressUnsavedChangeTracking;

    // Store original values for cancel/restore
    private AppConfig _originalConfig = null!;

    [ObservableProperty] private Option<Language>? _selectedLanguage;
    [ObservableProperty] private Option<NumberDisplayMode>? _selectedNumberDisplayMode;
    private readonly IConfigManager _configManager;
    private readonly IDeviceManagementService _deviceManagementService;
    private readonly LocalizationManager _localization;
    private readonly IMessageDialogService _messageDialogService;
    private readonly IDataStorage _dataStorage;
    private readonly IClassColorService _classColorService;
    private readonly IAutoUpdateService _autoUpdateService;
    private readonly ILogger<SettingsViewModel> _logger;

    private static readonly (string Key,
        string LabelResourceKey,
        string Placeholder,
        string? ExampleValueResourceKey,
        string? ExampleValueLiteral)[] FormatFieldDefinitions =
    [
        (
            "Name",
            ResourcesKeys.Settings_PlayerInfo_Name,
            "{Name}",
            ResourcesKeys.Settings_PlayerInfo_Name,
            null
        ),
        (
            "Spec",
            ResourcesKeys.Settings_PlayerInfo_ClassSpec,
            "{Spec}",
            ResourcesKeys.ClassSpec_FrostMageIcicle,
            null
        ),
        (
            "PowerLevel",
            ResourcesKeys.SkillBreakdown_Label_Power,
            "{PowerLevel}",
            null,
            "25000"
        ),
        (
            "SeasonStrength",
            ResourcesKeys.Settings_PlayerInfo_SeasonStrength,
            "{SeasonStrength}",
            null,
            "8"
        ),
        (
            "SeasonLevel",
            ResourcesKeys.Settings_PlayerInfo_SeasonLevel,
            "{SeasonLevel}",
            null,
            "50"
        ),
        /*
        (
            "Guild",
            ResourcesKeys.Settings_PlayerInfo_GuildName,
            "{Guild}",
            ResourcesKeys.Settings_PlayerInfo_MyGuild,
            null
        ),
        */
        (
            "Uid",
            ResourcesKeys.Settings_PlayerInfo_PlayerUID,
            "{Uid}",
            null,
            "123456789"
        ),
    ];

    /// <inheritdoc/>
    public SettingsViewModel(IConfigManager configManager,
        IDeviceManagementService deviceManagementService,
        LocalizationManager localization,
        IMessageDialogService messageDialogService,
        IDataStorage dataStorage,
        IClassColorService classColorService,
        IAutoUpdateService autoUpdateService,
        ILogger<SettingsViewModel> logger)
    {
        _configManager = configManager;
        _deviceManagementService = deviceManagementService;
        _localization = localization;
        _messageDialogService = messageDialogService;
        _dataStorage = dataStorage;
        _classColorService = classColorService;
        _autoUpdateService = autoUpdateService;
        _logger = logger;
        _appConfig = configManager.CurrentConfig.Clone();

        InitializeClassColors();
        _logger.LogDebug("SettingsViewModel created");
    }

    /// <summary>
    /// 格式字符串预览
    /// </summary>
    public string FormatPreview
    {
        get
        {
            if (!AppConfig.UseCustomFormat) return _localization.GetString(ResourcesKeys.Settings_CustomFormat_Message);
            // 创建一个示例 PlayerInfoViewModel 来生成预览
            var previewVm = new PlayerInfoViewModel(_localization)
            {
                Name = _localization.GetString(ResourcesKeys.Settings_PlayerInfo_Name),
                Spec = ClassSpec.FrostMageIcicle,
                PowerLevel = 25000,
                SeasonStrength = 8,
                SeasonLevel = 50,
                //Guild = "MyGuild",
                Uid = 123456789,
                Mask = false,
                UseCustomFormat = true,
                FormatString = AppConfig.PlayerInfoFormatString
            };

            // Trigger the update to generate the formatted string
            return previewVm.PlayerInfo;
        }
    }

    private string BuildExampleText(string? resourceKey, string? literal)
    {
        var examplePrefix = _localization.GetString(ResourcesKeys.Settings_PlayerInfo_Tip);
        string? exampleValue;
        if (!string.IsNullOrWhiteSpace(resourceKey))
            exampleValue = _localization.GetString(resourceKey);
        else
            exampleValue = literal;
        return $"{examplePrefix}{exampleValue}";
    }

    private void RebuildAvailableFormatFields()
    {
        AvailableFormatFields = FormatFieldDefinitions
            .Select(x => new FormatFieldOption(
                x.Key,
                _localization.GetString(x.LabelResourceKey),
                x.Placeholder,
                BuildExampleText(x.ExampleValueResourceKey, x.ExampleValueLiteral)))
            .ToList();
    }

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

    partial void OnAppConfigChanged(AppConfig? oldValue, AppConfig newValue)
    {
        oldValue?.PropertyChanged -= OnAppConfigPropertyChanged;
        // Subscribe to the new instance
        newValue.PropertyChanged += OnAppConfigPropertyChanged;

        _localization.ApplyLanguage(newValue.Language);
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
        _localization.ApplyLanguage(value.Value);
    }

    partial void OnAvailableNetworkAdaptersChanged(List<NetworkAdapterInfo> value)
    {
        AppConfig.PreferredNetworkAdapter ??= value.FirstOrDefault();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadedAsync()
    {
        // Clone current config for editing
        AppConfig = _configManager.CurrentConfig.Clone();

        // Store original config for cancel/restore (deep clone)
        _originalConfig = _configManager.CurrentConfig.Clone();

        SubscribeHandlers();

        UpdateLanguageDependentCollections();
        _localization.ApplyLanguage(AppConfig.Language);
        await LoadNetworkAdaptersAsync();

        RebuildAvailableFormatFields();

        // ✅ 初次载入时同步一次当前UID到设置页显示
        SyncUidFromDataStorage(saveToConfig: false);

        _hasUnsavedChanges = false;
        _isLoaded = true;
        _logger.LogDebug("SettingsViewModel Loaded");
    }

    private void InitializeClassColors()
    {
        ClassColorSettings.Clear();
        var classes = Enum.GetValues<Classes>()
            .Where(c => c != Classes.Unknown)
            .ToList();

        foreach (var cls in classes)
        {
            var color = _classColorService.GetColor(cls);
            var defaultColor = _classColorService.GetDefaultColor(cls);
            var name = cls.GetLocalizedDescription();
            ClassColorSettings.Add(new ClassColorSettingViewModel(cls, name, color, defaultColor, AppConfig, ApplyColorChange));
        }
    }

    private void ApplyColorChange(Classes cls, Color color)
    {
        _logger.LogInformation("Updating class color for {Class}: {Color}", cls, color);
        AppConfig.CustomClassColors[cls] = color.ToString();
        _classColorService.UpdateColor(cls, color);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task NetworkAdapterAutoSelect()
    {
        _logger.LogInformation("Starting auto-selection of network adapter...");
        var ret = await _deviceManagementService.GetAutoSelectedNetworkAdapterAsync();
        if (ret != null)
        {
            _logger.LogInformation("Auto-selected network adapter: {AdapterName}", ret.Name);
            AppConfig.PreferredNetworkAdapter = ret;
            _deviceManagementService.SetActiveNetworkAdapter(ret);
            return;
        }
        _logger.LogWarning("Auto-selection of network adapter failed.");
        _messageDialogService.Show(_localization.GetString(ResourcesKeys.Settings_NetworkAdapterAutoSelect_Title), _localization.GetString(ResourcesKeys.Settings_NetworkAdapterAutoSelect_Failed)); // Temporary message dialog
    }

    private async Task LoadNetworkAdaptersAsync()
    {
        var adapters = await _deviceManagementService.GetNetworkAdaptersAsync();
        AvailableNetworkAdapters = adapters.Select(a => new NetworkAdapterInfo(a.name, a.description)).ToList();
        AppConfig.PreferredNetworkAdapter =
            AvailableNetworkAdapters.FirstOrDefault(a => a.Name == AppConfig.PreferredNetworkAdapter?.Name);
    }

    private void SubscribeHandlers()
    {
        if (!_cultureHandlerSubscribed)
        {
            _localization.CultureChanged += OnCultureChanged;
            _cultureHandlerSubscribed = true;
        }

        if (!_networkHandlerSubscribed)
        {
            NetworkChange.NetworkAvailabilityChanged += OnSystemNetworkChanged;
            NetworkChange.NetworkAddressChanged += OnSystemNetworkChanged;
            _networkHandlerSubscribed = true;
        }

        if (!_dataStorageHandlerSubscribed)
        {
            _dataStorage.DataUpdated += OnDataStorageDataUpdated;
            _dataStorageHandlerSubscribed = true;
        }
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

    private void OnDataStorageDataUpdated()
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(new Action(() => SyncUidFromDataStorage(saveToConfig: true)));
            return;
        }

        SyncUidFromDataStorage(saveToConfig: true);
    }

    private void SyncUidFromDataStorage(bool saveToConfig)
    {
        var currentUid = _dataStorage.CurrentPlayerInfo.UID;
        if (currentUid == 0) return;

        var runtimeConfigChanged = _configManager.CurrentConfig.Uid != currentUid;
        var viewConfigChanged = AppConfig.Uid != currentUid;

        if (viewConfigChanged)
        {
            _suppressUnsavedChangeTracking = true;
            try
            {
                AppConfig.Uid = currentUid;
            }
            finally
            {
                _suppressUnsavedChangeTracking = false;
            }
        }

        if (runtimeConfigChanged)
        {
            _configManager.CurrentConfig.Uid = currentUid;
        }

        if (saveToConfig && runtimeConfigChanged)
        {
            _ = PersistAutoDetectedUidAsync();
        }
    }

    private async Task PersistAutoDetectedUidAsync()
    {
        try
        {
            await _configManager.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist auto-detected UID");
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
            _localization.ApplyLanguage(config.Language);
            UpdateLanguageDependentCollections();
        }
        else if (e.PropertyName == nameof(AppConfig.PreferredNetworkAdapter))
        {
            var adapter = AppConfig.PreferredNetworkAdapter;
            if (adapter != null)
            {
                _deviceManagementService.SetActiveNetworkAdapter(adapter);
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
        else if (e.PropertyName == nameof(AppConfig.CenterBackgroundOpacity))
        {
            if (_isLoaded)
            {
                ApplyCenterBackgroundOpacityImmediately(config.CenterBackgroundOpacity);
            }
        }
        else if (e.PropertyName == nameof(AppConfig.BackgroundImageOpacity))
        {
            if (_isLoaded)
            {
                ApplyBackgroundImageOpacityImmediately(config.BackgroundImageOpacity);
            }
        }
        else if (e.PropertyName == nameof(AppConfig.ThemeColor))
        {
            // ✅ Real-time preview for preset theme color buttons as well
            if (_isLoaded)
            {
                ApplyThemeColorImmediately(config.ThemeColor);
            }

            OnPropertyChanged(nameof(CurrentThemeColor));
        }
        else if (e.PropertyName == nameof(AppConfig.CenterBackgroundColor))
        {
            if (_isLoaded)
            {
                ApplyCenterBackgroundColorImmediately(config.CenterBackgroundColor);
            }

            OnPropertyChanged(nameof(CurrentBackColor));
        }
        else if (e.PropertyName == nameof(AppConfig.BackgroundImagePath))
        {
            if (_isLoaded)
            {
                ApplyBackgroundImageImmediately(config.BackgroundImagePath);
            }
        }
        else if (e.PropertyName is nameof(AppConfig.PlayerInfoFormatString) or nameof(AppConfig.UseCustomFormat))
        {
            // Update format string preview only (no real-time application to actual config)
            OnPropertyChanged(nameof(FormatPreview));
        }
        else if (e.PropertyName == nameof(AppConfig.TimeSeriesSampleCapacity))
        {
            // Update the Core layer's static configuration
            if (_isLoaded)
            {
                StatisticsConfiguration.TimeSeriesSampleCapacity = config.TimeSeriesSampleCapacity;
            }
        }
        else if (e.PropertyName == nameof(AppConfig.DpsUpdateInterval))
        {
            // Update sample recording interval when DpsUpdateInterval changes
            if (_isLoaded)
            {
                _dataStorage.SampleRecordingInterval = config.DpsUpdateInterval;
            }
        }

        if (_isLoaded && !_suppressUnsavedChangeTracking)
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
        _configManager.CurrentConfig.Opacity = opacity;
    }

    private void ApplyCenterBackgroundOpacityImmediately(double opacity)
    {
        _configManager.CurrentConfig.CenterBackgroundOpacity = opacity;
    }

    private void ApplyBackgroundImageOpacityImmediately(double opacity)
    {
        _configManager.CurrentConfig.BackgroundImageOpacity = opacity;
    }
    /// <summary>
    /// ✅ Immediately apply theme color change to the running application config for real-time preview
    /// </summary>
    private void ApplyThemeColorImmediately(string? themeColor)
    {
        if (string.IsNullOrWhiteSpace(themeColor))
            return;

        _configManager.CurrentConfig.ThemeColor = themeColor;
    }

    private void ApplyCenterBackgroundColorImmediately(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return;

        _configManager.CurrentConfig.CenterBackgroundColor = color;
    }

    private void ApplyBackgroundImageImmediately(string? backgroundImagePath)
    {
        _configManager.CurrentConfig.BackgroundImagePath =
            string.IsNullOrWhiteSpace(backgroundImagePath) ? null : backgroundImagePath;
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
        return _configManager.SaveAsync(AppConfig);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        _logger.LogInformation("Saving settings and closing...");
        await ApplySettingsAsync();
        UnsubscribeHandlers();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task Cancel()
    {
        if (!_hasUnsavedChanges)
        {
            _logger.LogDebug("Closing settings (no changes).");
            UnsubscribeHandlers();
            RequestClose?.Invoke();
            return;
        }

        var title = _localization.GetString(ResourcesKeys.Settings_CancelConfirm_Title);
        var message = _localization.GetString(ResourcesKeys.Settings_CancelConfirm_Message);

        var result = _messageDialogService.Show(title, message);
        if (result == true)
        {
            // User chose to discard changes - restore original config
            _logger.LogInformation("Settings changes discarded by user.");
            RestoreOriginalConfig();

            _hasUnsavedChanges = false;
            UnsubscribeHandlers();
            RequestClose?.Invoke();
        }
    }

    /// <summary>
    /// ⭐ 新增: 选择背景图片
    /// </summary>
    [RelayCommand]
    private void SelectBackgroundImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _localization.GetString(ResourcesKeys.Settings_Theme_SelectImage),
            Filter = _localization.GetString(ResourcesKeys.Settings_Theme_BackgroundImage_Filter),
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            _logger.LogInformation("Background image selected: {Path}", dialog.FileName);
            AppConfig.BackgroundImagePath = dialog.FileName;
        }
    }

    /// <summary>
    /// ⭐ 新增: 清除背景图片
    /// </summary>
    [RelayCommand]
    private void ClearBackgroundImage()
    {
        _logger.LogInformation("Background image cleared.");
        AppConfig.BackgroundImagePath = null;
    }

    /// <summary>
    /// ⭐ 设置主题颜色（现在也会实时预览）
    /// </summary>
    [RelayCommand]
    private void SetThemeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return;

        _logger.LogInformation("Theme color set to: {Color}", color);
        AppConfig.ThemeColor = color;
        // 即时预览已在 OnAppConfigPropertyChanged(nameof(AppConfig.ThemeColor)) 里统一处理
    }

    [RelayCommand]
    private void SetBackColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return;

        _logger.LogInformation("Center background color set to: {Color}", color);
        AppConfig.CenterBackgroundColor = color;
    }

    /// <summary>
    /// ⭐ 从颜色选择器更新主题颜色（用于Color对象）
    /// </summary>
    [RelayCommand]
    private void UpdateThemeColorFromPicker(Color color)
    {
        _logger.LogDebug("Theme color picked: #{R:X2}{G:X2}{B:X2}", color.R, color.G, color.B);
        CurrentThemeColor = color;
    }

    [RelayCommand]
    private void UpdateBackColorFromPicker(Color color)
    {
        _logger.LogDebug("Center background color picked: #{R:X2}{G:X2}{B:X2}", color.R, color.G, color.B);
        CurrentBackColor = color;
    }

    [RelayCommand]
    private async Task CheckForUpdatesManualAsync()
    {
        await _autoUpdateService.CheckForUpdatesAsync(false);
    }

    [RelayCommand]
    private void TryGetCurrentUid()
    {
        SyncUidFromDataStorage(saveToConfig: true);

        var title = _localization.GetString(ResourcesKeys.Settings_UID_Setting_Title);
        var message1 = _localization.GetString(ResourcesKeys.Settings_UID_Setting_Message1);
        var message2 = _localization.GetString(ResourcesKeys.Settings_UID_Setting_Message2);

        if (AppConfig.Uid == 0)
            _messageDialogService.Show(title, message1);
        else
            _messageDialogService.Show(title, message2);
    }

    /// <summary>
    /// ⭐ 当前主题颜色（用于颜色选择器初始化）
    /// </summary>
    public Color CurrentThemeColor
    {
        get
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(AppConfig.ThemeColor);
            }
            catch
            {
                return Colors.Gray;
            }
        }
        set
        {
            var hexColor = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
            if (AppConfig.ThemeColor == hexColor)
                return;

            AppConfig.ThemeColor = hexColor;
            // 即时预览已在 OnAppConfigPropertyChanged(nameof(AppConfig.ThemeColor)) 里统一处理
        }
    }

    public Color CurrentBackColor
    {
        get
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(AppConfig.CenterBackgroundColor);
            }
            catch
            {
                return (Color)ColorConverter.ConvertFromString("#191919");
            }
        }
        set
        {
            var hexColor = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
            if (AppConfig.CenterBackgroundColor == hexColor)
                return;

            AppConfig.CenterBackgroundColor = hexColor;
        }
    }

    /// <summary>
    /// Restore the original config when user cancels
    /// </summary>
    private void RestoreOriginalConfig()
    {
        if (_originalConfig == null) return;

        // Restore real-time preview values
        _configManager.CurrentConfig.Opacity = _originalConfig.Opacity;
        _configManager.CurrentConfig.CenterBackgroundOpacity = _originalConfig.CenterBackgroundOpacity;
        _configManager.CurrentConfig.BackgroundImageOpacity = _originalConfig.BackgroundImageOpacity;
        _configManager.CurrentConfig.ThemeColor = _originalConfig.ThemeColor;
        _configManager.CurrentConfig.CenterBackgroundColor = _originalConfig.CenterBackgroundColor;
        _configManager.CurrentConfig.BackgroundImagePath = _originalConfig.BackgroundImagePath;

        // Restore player info format settings
        _configManager.CurrentConfig.UseCustomFormat = _originalConfig.UseCustomFormat;
        _configManager.CurrentConfig.PlayerInfoFormatString = _originalConfig.PlayerInfoFormatString;
    }

    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        UpdateLanguageDependentCollections();
        RebuildAvailableFormatFields();
        OnPropertyChanged(nameof(FormatPreview));
    }

    private void UnsubscribeHandlers()
    {
        if (_cultureHandlerSubscribed)
        {
            _localization.CultureChanged -= OnCultureChanged;
            _cultureHandlerSubscribed = false;
        }

        if (_networkHandlerSubscribed)
        {
            NetworkChange.NetworkAvailabilityChanged -= OnSystemNetworkChanged;
            NetworkChange.NetworkAddressChanged -= OnSystemNetworkChanged;
            _networkHandlerSubscribed = false;
        }

        if (_dataStorageHandlerSubscribed)
        {
            _dataStorage.DataUpdated -= OnDataStorageDataUpdated;
            _dataStorageHandlerSubscribed = false;
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
    public SettingsDesignTimeViewModel() : base(
        new DesignConfigManager(),
        new DesignTimeDeviceManagementService(),
        new LocalizationManager(new LocalizationConfiguration(), NullLogger<LocalizationManager>.Instance),
        new DesignMessageDialogService(),
        new DesignDataStorage(),
        new ClassColorService(null!),
        new DesignAutoUpdateService(),
        NullLogger<SettingsViewModel>.Instance)
    {
        AppConfig = new AppConfig
        {
            // set friendly defaults shown in designer
            Opacity = 100,
            CenterBackgroundOpacity = 30,
            BackgroundImageOpacity = 50,
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

internal sealed class DesignAutoUpdateService : IAutoUpdateService
{
    public Task CheckForUpdatesAsync(bool silentIfNoUpdate = true, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Design-time stub for IDataStorage
/// </summary>
internal sealed class DesignDataStorage : IDataStorage
{
    public PlayerInfo CurrentPlayerInfo => new();
    public ReadOnlyDictionary<long, PlayerInfo> ReadOnlyPlayerInfoDatas => new(new Dictionary<long, PlayerInfo>());
    public ReadOnlyDictionary<long, DpsData> ReadOnlyFullDpsDatas => new(new Dictionary<long, DpsData>());
    public IReadOnlyList<DpsData> ReadOnlyFullDpsDataList => Array.Empty<DpsData>();
    public ReadOnlyDictionary<long, DpsData> ReadOnlySectionedDpsDatas => new(new Dictionary<long, DpsData>());
    public IReadOnlyList<DpsData> ReadOnlySectionedDpsDataList => Array.Empty<DpsData>();
    public TimeSpan SectionTimeout { get; set; }
    public bool IsServerConnected { get; set; }
    public int SampleRecordingInterval { get; set; }

#pragma warning disable CS0067
    public event ServerConnectionStateChangedEventHandler? ServerConnectionStateChanged;
    public event PlayerInfoUpdatedEventHandler? PlayerInfoUpdated;
    public event NewSectionCreatedEventHandler? NewSectionCreated;
    public event BattleLogCreatedEventHandler? BattleLogCreated;
    public event DpsDataUpdatedEventHandler? DpsDataUpdated;
    public event DataUpdatedEventHandler? DataUpdated;
    public event ServerChangedEventHandler? ServerChanged;
    public event Action? BeforeSectionCleared;
    public event SectionEndedEventHandler? SectionEnded;
#pragma warning restore

    public void SetPlayerCombatStateTime(long uid, long time) { }
    public void SetCurrentPlayerUid(long uid) { }

    public void LoadPlayerInfoFromFile() { }
    public void SavePlayerInfoToFile() { }
    public Dictionary<long, PlayerInfoFileData> BuildPlayerDicFromBattleLog(List<BattleLog> battleLogs) => new();
    public void ClearAllDpsData() { }
    public void ClearDpsData() { }
    public void ClearCurrentPlayerInfo() { }
    public void ClearPlayerInfos() { }
    public void ClearAllPlayerInfos() { }
    public void ServerChange(string currentServerStr, string prevServer) { }
    public void SetPlayerLevel(long playerUid, int tmpLevel) { }
    public bool EnsurePlayer(long playerUid) => true;
    public void SetPlayerHP(long playerUid, long hp) { }
    public void SetPlayerMaxHP(long playerUid, long maxHp) { }
    public void SetPlayerCombatState(long uid, bool combatState) { }
    public void SetPlayerName(long playerUid, string playerName) { }
    public void SetPlayerCombatPower(long playerUid, int combatPower) { }
    public void SetPlayerProfessionID(long playerUid, int professionId) { }
    public void SetPlayerGuild(long playerUid, string guild) { }
    public void AddBattleLog(BattleLog log) { }
    public void SetPlayerRankLevel(long playerUid, int readInt32) { }
    public void SetPlayerCritical(long playerUid, int readInt32) { }
    public void SetPlayerLucky(long playerUid, int readInt32) { }
    public void SetPlayerElementFlag(long playerUid, int readInt32) { }
    public void SetPlayerReductionLevel(long playerUid, int readInt32) { }
    public void SetPlayerEnergyFlag(long playerUid, int readInt32) { }
    public void SetNpcTemplateId(long playerUid, int templateId) { }
    public void SetPlayerSeasonLevel(long playerUid, int seasonLevel) { }
    public void SetPlayerSeasonStrength(long playerUid, int seasonStrength) { }
    public IReadOnlyList<BattleLog> GetBattleLogsForPlayer(long uid, bool fullSession) => Array.Empty<BattleLog>();
    public IReadOnlyList<BattleLog> GetBattleLogs(bool fullSession) => Array.Empty<BattleLog>();
    public IReadOnlyDictionary<long, PlayerStatistics> GetStatistics(bool fullSession) => new Dictionary<long, PlayerStatistics>();
    public int GetStatisticsCount(bool fullSession) => 0;
    public void Dispose() { }
}