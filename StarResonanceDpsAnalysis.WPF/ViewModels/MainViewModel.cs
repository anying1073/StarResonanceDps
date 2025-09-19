using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Themes;
using StarResonanceDpsAnalysis.WPF.Views;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DebugFunctions(DpsStatisticsViewModel dpsStatisticsViewModel)
{
    [RelayCommand]
    private void CallDebugWindow()
    {
        var debugWindow = new DebugView();
        debugWindow.Show();
    }
}

public partial class MainViewModel(ApplicationThemeManager themeManager, DebugFunctions debugFunctions, SkillBreakdownView skillBreakdownView, IWindowManagementService windowManagement) : BaseViewModel
{
    public DebugFunctions Debug { get; init; } = debugFunctions;

    [ObservableProperty] private ApplicationTheme _theme = themeManager.GetAppTheme();
    [ObservableProperty] private List<ApplicationTheme> _availableThemes = [ApplicationTheme.Light, ApplicationTheme.Dark];

    partial void OnThemeChanged(ApplicationTheme value)
    {
        themeManager.Apply(value);
    }

    [RelayCommand]
    private void CallDpsStatisticsView()
    {
        windowManagement.DpsStatisticsView.Show();
    }

    [RelayCommand]
    private void CallSettingsView()
    {
        windowManagement.SettingsView.Show();
    }

    [RelayCommand]
    private void CallSkillBreakdownView()
    {
        windowManagement.SkillBreakdownView.Show();
    }
}