using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OxyPlot;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Properties;
using System.Collections.ObjectModel;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using System.Windows.Threading;
using StarResonanceDpsAnalysis.Core.Data;

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
