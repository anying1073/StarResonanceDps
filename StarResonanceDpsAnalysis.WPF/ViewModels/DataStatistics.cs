using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Represents data statistics
/// </summary>
public partial class DataStatistics : BaseViewModel
{
    [ObservableProperty] private long _average;

    [ObservableProperty] private double _critRate;

    [ObservableProperty] private int _hits;

    [ObservableProperty] private long _total;
}
