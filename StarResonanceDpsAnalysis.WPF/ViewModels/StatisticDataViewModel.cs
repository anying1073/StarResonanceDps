using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

[DebuggerDisplay("Name:{Player?.Name};Value:{Value}")]
public partial class StatisticDataViewModel(DebugFunctions debug) : BaseViewModel, IComparable<StatisticDataViewModel>
{
    [ObservableProperty] private ulong _duration;
    [ObservableProperty] private long _index;
    [ObservableProperty] private double _percent;
    [ObservableProperty] private double _percentOfMax;
    [ObservableProperty] private PlayerInfoViewModel _player = new();
    [ObservableProperty] private ulong _value;

    public DebugFunctions Debug { get; } = debug;
    public SkillDataCollection Damage { get; } = new();
    public SkillDataCollection Heal { get; } = new();
    public SkillDataCollection TakenDamage { get; } = new();

    public int CompareTo(StatisticDataViewModel? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        return Value.CompareTo(other.Value);
    }

    public void Reset()
    {
        // Reset numeric fields
        Duration = 0;
        Index = 0;
        Percent = 0;
        PercentOfMax = 0;
        Value = 0;

        Damage.Reset();
        Heal.Reset();
        TakenDamage.Reset();

        // Reset player info
        Player = new PlayerInfoViewModel();
    }

    public partial class SkillDataCollection : BaseViewModel
    {
        [ObservableProperty] private IReadOnlyList<SkillItemViewModel> _filteredSkillList = [];
        [ObservableProperty] private IReadOnlyList<SkillItemViewModel> _totalSkillList = [];

        [ObservableProperty] private ObservableCollection<(TimeSpan duration, double section, double total)> _dps = new();

        public void Reset()
        {
            this.FilteredSkillList = [];
            this.TotalSkillList = [];
            if (Dps.Count > 0) this.Dps.Clear();
        }

        public event Action<IReadOnlyList<SkillItemViewModel>?>? SkillChanged;

        protected virtual void RaiseSkillChanged(IReadOnlyList<SkillItemViewModel>? obj)
        {
            SkillChanged?.Invoke(obj);
        }

        partial void OnFilteredSkillListChanged(IReadOnlyList<SkillItemViewModel>? value)
        {
            RaiseSkillChanged(value);
        }

        partial void OnTotalSkillListChanged(IReadOnlyList<SkillItemViewModel>? value)
        {
            RaiseSkillChanged(value);
        }
    }
}