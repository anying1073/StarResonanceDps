using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Represents data statistics
/// </summary>
public partial class DataStatistics : BaseViewModel
{
    [ObservableProperty] private double _average;
    [ObservableProperty] private int _critCount;
    [ObservableProperty] private int _hits;
    [ObservableProperty] private int _luckyCount;
    [ObservableProperty] private long _total;
    [ObservableProperty] private long _normalValue;
    [ObservableProperty] private long _critValue;
    [ObservableProperty] private long _luckyValue;
    [ObservableProperty] private ObservableCollection<SkillItemViewModel> _skills = new();

    public double LuckyRate => Hits > 0 ? (double)LuckyCount / Hits : double.NaN;

    public double CritRate => Hits > 0 ? (double)CritCount / Hits : double.NaN;

    partial void OnCritCountChanged(int value)
    {
        OnPropertyChanged(nameof(CritRate));
    }

    partial void OnLuckyCountChanged(int value)
    {
        OnPropertyChanged(nameof(LuckyRate));
    }

    partial void OnHitsChanged(int value)
    {
        OnPropertyChanged(nameof(LuckyRate));
        OnPropertyChanged(nameof(CritRate));
    }
}