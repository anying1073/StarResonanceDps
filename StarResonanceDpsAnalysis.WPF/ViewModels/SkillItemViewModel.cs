using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class SkillItemViewModel : BaseViewModel
{
    [ObservableProperty] private long _skillId;
    [ObservableProperty] private string _skillName = string.Empty;
    [ObservableProperty] private long _totalDamage;
    [ObservableProperty] private long _totalHeal;
    [ObservableProperty] private long _totalTakenDamage;
    [ObservableProperty] private int _hitCount;
    [ObservableProperty] private int _critCount;
    [ObservableProperty] private int _avgDamage;
    [ObservableProperty] private int _avgHeal;
    [ObservableProperty] private int _avgTakenDamage;
    [ObservableProperty] private double _critRate;
    [ObservableProperty] private double _dpsValue;
    [ObservableProperty] private double _hpsValue;
    [ObservableProperty] private double _dtpsValue;
}