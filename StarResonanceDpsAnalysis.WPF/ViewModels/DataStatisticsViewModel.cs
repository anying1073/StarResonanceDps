using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDpsAnalysis.WPF.Extensions;

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
    [ObservableProperty] private int _critLuckyCount;
    [ObservableProperty] private long _total;
    [ObservableProperty] private long _normalValue;
    [ObservableProperty] private long _critValue;
    [ObservableProperty] private long _luckyValue;
    [ObservableProperty] private long _critLuckyValue;

    public double LuckyRate => MathExtension.Rate(LuckyCount, Hits);

    public double CritLuckyRate => MathExtension.Rate(CritLuckyCount, Hits);

    public double CritRate => MathExtension.Rate(CritCount, Hits);

    public int NormalCount => Hits - CritCount;

    public int TotalLuckyCount => LuckyCount + CritLuckyCount;

    public double NormalRate => MathExtension.Rate(NormalCount, Hits);

    partial void OnCritCountChanged(int value)
    {
        OnPropertyChanged(nameof(CritRate));
        OnPropertyChanged(nameof(NormalCount));
        OnPropertyChanged(nameof(NormalRate));
    }

    partial void OnLuckyCountChanged(int value)
    {
        OnPropertyChanged(nameof(LuckyRate));
        OnPropertyChanged(nameof(CritLuckyRate));
        OnPropertyChanged(nameof(TotalLuckyCount));
        OnPropertyChanged(nameof(NormalCount));
        OnPropertyChanged(nameof(NormalRate));
    }

    partial void OnCritLuckyCountChanged(int value)
    {
        OnPropertyChanged(nameof(CritLuckyRate));
        OnPropertyChanged(nameof(TotalLuckyCount));
        OnPropertyChanged(nameof(NormalCount));
        OnPropertyChanged(nameof(NormalRate));
    }

    partial void OnHitsChanged(int value)
    {
        OnPropertyChanged(nameof(LuckyRate));
        OnPropertyChanged(nameof(CritRate));
        OnPropertyChanged(nameof(NormalCount));
        OnPropertyChanged(nameof(NormalRate));
        OnPropertyChanged(nameof(CritLuckyRate));
    }
}