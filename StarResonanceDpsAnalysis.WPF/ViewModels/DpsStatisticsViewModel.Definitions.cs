using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Options/Settings class for DPS Statistics
/// </summary>
public partial class DpsStatisticsOptions : BaseViewModel
{
    [ObservableProperty] private int _minimalDurationInSeconds;

    [RelayCommand]
    private void SetMinimalDuration(int duration)
    {
        MinimalDurationInSeconds = duration;
    }
}

/// <summary>
/// Type definitions and records for DpsStatisticsViewModel
/// </summary>
public partial class DpsStatisticsViewModel
{
    public partial record struct Data
    {
        public int HitCount { get; set; }
        public long TotalValue { get; set; }
        public long NormalValue { get; set; }
        public double Average { get; set; }
        public double CritRate { get; set; }
        public long CritValue { get; set; }
        public int CritCount { get; set; }
        public long LuckyValue { get; set; }
        public int LuckyCount { get; set; }

        public readonly void Deconstruct(out int hitCount, out long totalValue, out long normalValue,
            out double average, out double critRate, out long critValue, out int critCount, out long luckyValue,
            out int luckyCount)
        {
            hitCount = HitCount;
            totalValue = TotalValue;
            normalValue = NormalValue;
            average = Average;
            critRate = CritRate;
            critValue = CritValue;
            critCount = CritCount;
            luckyValue = LuckyValue;
            luckyCount = LuckyCount;
        }
    }
}