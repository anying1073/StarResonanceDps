using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Represents data statistics
/// </summary>
public partial class DataStatisticsViewModel : BaseViewModel
{
    [ObservableProperty] private double _average;
    [ObservableProperty] private int _critCount;
    [ObservableProperty] private int _hits;
    [ObservableProperty] private int _luckyCount;
    [ObservableProperty] private long _total;
    [ObservableProperty] private long _normalValue;
    [ObservableProperty] private long _critValue;
    [ObservableProperty] private long _luckyValue;

    public double LuckyRate => Hits > 0 ? (double)LuckyCount / Hits : double.NaN;

    public double CritRate => Hits > 0 ? (double)CritCount / Hits : double.NaN;
    
    public int NormalCount => Hits - CritCount - LuckyCount;
    
    public double NormalRate => Hits > 0 ? (double)NormalCount / Hits : double.NaN;

    partial void OnCritCountChanged(int value)
    {
        OnPropertyChanged(nameof(CritRate));
        OnPropertyChanged(nameof(NormalCount));
        OnPropertyChanged(nameof(NormalRate));
    }

    partial void OnLuckyCountChanged(int value)
    {
        OnPropertyChanged(nameof(LuckyRate));
        OnPropertyChanged(nameof(NormalCount));
        OnPropertyChanged(nameof(NormalRate));
    }

    partial void OnHitsChanged(int value)
    {
        OnPropertyChanged(nameof(LuckyRate));
        OnPropertyChanged(nameof(CritRate));
        OnPropertyChanged(nameof(NormalCount));
        OnPropertyChanged(nameof(NormalRate));
    }
}