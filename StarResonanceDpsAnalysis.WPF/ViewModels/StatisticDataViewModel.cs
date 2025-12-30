using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.WPF.Localization;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

[DebuggerDisplay("Name:{Player?.Name};Value:{Value}")]
public partial class StatisticDataViewModel(DebugFunctions debug, LocalizationManager localizationManager) : BaseViewModel, IComparable<StatisticDataViewModel>
{
    [ObservableProperty] private long _durationTicks;
    [ObservableProperty] private long _index;
    [ObservableProperty] private double _percent;
    [ObservableProperty] private double _percentOfMax;
    [ObservableProperty] private PlayerInfoViewModel _player = new(localizationManager);
    [ObservableProperty] private ulong _value;

    [ObservableProperty] private int _skillListRefreshTrigger = 0;

    // Action to notify parent about hover state change
    public Action<bool>? SetHoverStateAction { get; set; }

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
        DurationTicks = 0;
        Index = 0;
        Percent = 0;
        PercentOfMax = 0;
        Value = 0;

        Damage.Reset();
        Heal.Reset();
        TakenDamage.Reset();
    }

    /// <summary>
    /// Refreshes the filtered skill lists for all skill types (Damage, Heal, TakenDamage)
    /// and triggers the UI update.
    /// </summary>
    /// <param name="limit">The maximum number of skills to display (0 for all)</param>
    public void RefreshSkillLists(int limit)
    {
        Damage.RefreshFilteredList(limit);
        Heal.RefreshFilteredList(limit);
        TakenDamage.RefreshFilteredList(limit);
        SkillListRefreshTrigger++;
    }

    [RelayCommand]
    private void MouseEnter(int limit)
    {
        SetHoverStateAction?.Invoke(true);
        RefreshSkillLists(limit);
    }

    [RelayCommand]
    private void MouseLeave()
    {
        SetHoverStateAction?.Invoke(false);
    }

    public partial class SkillDataCollection : BaseViewModel
    {
        [ObservableProperty] private IReadOnlyList<SkillItemViewModel> _filteredSkillList = [];
        [ObservableProperty] private IReadOnlyList<SkillItemViewModel> _totalSkillList = [];

        // Note: DPS time series data is now stored in PlayerStatistics in the Core layer
        // This property is kept for backward compatibility with existing views
        [ObservableProperty] private ObservableCollection<(TimeSpan time, double value)> _dps = new();

        public void Reset()
        {
            this.FilteredSkillList = [];
            this.TotalSkillList = [];
            if (Dps.Count > 0) this.Dps.Clear();
        }

        /// <summary>
        /// Take top n skills from TotalSkillList to FilteredSkillList<br/>
        /// 从TotalSkillList重新过滤出FilteredSkillList
        /// </summary>
        /// <param name="limit">显示条数限制,0表示显示全部</param>
        public void RefreshFilteredList(int limit = 0)
        {
            var newFiltered = limit > 0
                ? TotalSkillList.Take(limit).ToList()
             : TotalSkillList.ToList();

            FilteredSkillList = newFiltered;
        }

        public event Action<IReadOnlyList<SkillItemViewModel>?>? SkillChanged;

        protected virtual void RaiseSkillChanged(IReadOnlyList<SkillItemViewModel> obj)
        {
            SkillChanged?.Invoke(obj);
        }

        partial void OnFilteredSkillListChanged(IReadOnlyList<SkillItemViewModel> value)
        {
            RaiseSkillChanged(value);
        }

        partial void OnTotalSkillListChanged(IReadOnlyList<SkillItemViewModel> value)
        {
            RaiseSkillChanged(value);
        }
    }
}