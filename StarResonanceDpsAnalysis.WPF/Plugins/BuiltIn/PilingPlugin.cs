using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Plugins.Interfaces;
using StarResonanceDpsAnalysis.WPF.Properties;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.Plugins.BuiltIn;

internal class PilingPlugin : IPlugin
{
    private readonly IWindowManagementService _windowManagementService;
    private readonly LocalizationManager _localizationManager;

    public PilingPlugin(IWindowManagementService windowManagementService, LocalizationManager localizationManager)
    {
        _windowManagementService = windowManagementService;
        _localizationManager = localizationManager;
    }

    public string PackageName => "StarResonanceDpsAnalysis.WPF.Plugins.BuiltIn.PilingPlugin";

    public string PackageVersion => "3.0.0";

    public string GetPluginName(CultureInfo cultureInfo) =>
        _localizationManager.GetString(ResourcesKeys.MainView_Plugin_Piling_Title, cultureInfo);

    public string GetPluginDescription(CultureInfo cultureInfo) =>
        _localizationManager.GetString(ResourcesKeys.MainView_Plugin_Piling_Description, cultureInfo);

    public void OnRequestRun()
    {
        _windowManagementService.PersonalDpsView.Show();
    }

    public void OnRequestSetting()
    {
        _windowManagementService.SettingsView.Show();
    }
}

