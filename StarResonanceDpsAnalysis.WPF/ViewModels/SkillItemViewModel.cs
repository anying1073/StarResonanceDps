using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class SkillItemViewModel : BaseViewModel
{
    [ObservableProperty] private long _skillId;
    [ObservableProperty] private string _skillName = string.Empty;

    [ObservableProperty] private double _valuePerSecond;
    [ObservableProperty] private int _hitCount;
    [ObservableProperty] private long _totalValue;
    [ObservableProperty] private long _normalValue;
    [ObservableProperty] private double _average;
    [ObservableProperty] private double _critRate;
    [ObservableProperty] private double _luckyRate;
    [ObservableProperty] private long _critValue;
    [ObservableProperty] private int _critCount;
    [ObservableProperty] private long _luckyValue;
    [ObservableProperty] private int _luckyCount;
    [ObservableProperty] private double _rateToTotal;
    [ObservableProperty] private double _rateToMax;
}