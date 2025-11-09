using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Plugins;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Themes;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly ApplicationThemeManager _themeManager;
    private readonly IWindowManagementService _windowManagement;
    private readonly IApplicationControlService _appControlService;
    private readonly ITrayService _trayService;
    private readonly LocalizationManager _localizationManager;
    private readonly IMessageDialogService _dialogService;
    private readonly ObservableCollection<PluginListItemViewModel> _plugins = [];
    private PluginListItemViewModel? _lastSelectedPlugin;

    public MainViewModel(
        ApplicationThemeManager themeManager,
        DebugFunctions debugFunctions,
        IWindowManagementService windowManagement,
        IApplicationControlService appControlService,
        ITrayService trayService,
        IPluginManager pluginManager,
        LocalizationManager localizationManager,
        IMessageDialogService dialogService)
    {
        _themeManager = themeManager;
        _windowManagement = windowManagement;
        _appControlService = appControlService;
        _trayService = trayService;
        _localizationManager = localizationManager;
        _dialogService = dialogService;

        Debug = debugFunctions;
        AvailableThemes = [ApplicationTheme.Light, ApplicationTheme.Dark];
        Theme = _themeManager.GetAppTheme();

        var pluginStates = pluginManager.GetPluginStates();
        foreach (var plugin in pluginManager.GetPlugins())
        {
            if (!pluginStates.TryGetValue(plugin, out var state))
            {
                state = new PluginState();
            }

            _plugins.Add(new PluginListItemViewModel(plugin, state, localizationManager));
        }

        Plugins = new ReadOnlyObservableCollection<PluginListItemViewModel>(_plugins);
        SelectedPlugin = Plugins.FirstOrDefault();

        _localizationManager.CultureChanged += OnCultureChanged;
    }

    public DebugFunctions Debug { get; }

    public ReadOnlyObservableCollection<PluginListItemViewModel> Plugins { get; }

    [ObservableProperty]
    private List<ApplicationTheme> _availableThemes = [];

    [ObservableProperty]
    private ApplicationTheme _theme;

    [ObservableProperty]
    private PluginListItemViewModel? _selectedPlugin;

    [ObservableProperty]
    private bool _isDebugTabActive;

    partial void OnThemeChanged(ApplicationTheme value)
    {
        _themeManager.Apply(value);
    }

    [RelayCommand]
    private void InitializeTray()
    {
        _trayService.Initialize("Star Resonance DPS");
    }

    [RelayCommand]
    private void MinimizeToTray()
    {
        _trayService.MinimizeToTray();
    }

    [RelayCommand]
    private void RestoreFromTray()
    {
        _trayService.Restore();
    }

    [RelayCommand]
    private void ExitFromTray()
    {
        _trayService.Exit();
    }

    partial void OnSelectedPluginChanged(PluginListItemViewModel? value)
    {
        if (value != null)
        {
            _lastSelectedPlugin = value;
            IsDebugTabActive = false;
        }
    }

    partial void OnIsDebugTabActiveChanged(bool value)
    {
        if (value)
        {
            if (SelectedPlugin != null)
            {
                _lastSelectedPlugin = SelectedPlugin;
            }
            SelectedPlugin = null;
        }
        else if (SelectedPlugin is null && _lastSelectedPlugin != null)
        {
            if (_plugins.Contains(_lastSelectedPlugin))
            {
                SelectedPlugin = _lastSelectedPlugin;
            }
        }
    }

    private void OnCultureChanged(object? sender, CultureInfo e)
    {
        foreach (var plugin in _plugins)
        {
            plugin.RefreshLocalization();
        }
    }

    [RelayCommand]
    private void CallPersonalDpsView() 
    {
        _windowManagement.PersonalDpsView.Show();
    }

    [RelayCommand]
    private void CallSettingsView()
    {
        _windowManagement.SettingsView.Show();
    }

    [RelayCommand]
    private void CallSkillBreakdownView()
    {
        _windowManagement.SkillBreakdownView.Show();
    }

    [RelayCommand]
    private void CallAboutView()
    {
        _windowManagement.AboutView.ShowDialog();
    }

    [RelayCommand]
    private void CallDamageReferenceView()
    {
        _windowManagement.DamageReferenceView.Show();
    }

    [RelayCommand]
    private void Shutdown()
    {
        var title = _localizationManager.GetString(ResourcesKeys.App_Exit_Confirm_Title);
        var content = _localizationManager.GetString(ResourcesKeys.App_Exit_Confirm_Content);

        var result = _dialogService.Show(title, content);
        if (result == true)
        {
            _appControlService.Shutdown();
        }
    }
}