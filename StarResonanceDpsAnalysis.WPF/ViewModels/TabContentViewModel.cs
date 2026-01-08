using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for TabContentPanel component
/// </summary>
public partial class TabContentViewModel : BaseViewModel
{
    [ObservableProperty] private PlotViewModel _plot;
    [ObservableProperty] private DataStatisticsViewModel _stats = new();
    [ObservableProperty] private SkillListViewModel _skillList = new();

    public TabContentViewModel(PlotViewModel plot)
    {
        _plot = plot;
    }
}
