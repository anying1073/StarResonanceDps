using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;

public class SkillStatsSummaryPanel : Control
{
    static SkillStatsSummaryPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SkillStatsSummaryPanel),
            new FrameworkPropertyMetadata(typeof(SkillStatsSummaryPanel)));
    }


    public static readonly DependencyProperty HitsLabelProperty = DependencyProperty.Register(
        nameof(HitsLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? HitsLabel
    {
        get => (string?)GetValue(HitsLabelProperty);
        set => SetValue(HitsLabelProperty, value);
    }

    public static readonly DependencyProperty HitsProperty = DependencyProperty.Register(
        nameof(Hits), typeof(long), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(long)));

    public long Hits
    {
        get => (long)GetValue(HitsProperty);
        set => SetValue(HitsProperty, value);
    }

    public static readonly DependencyProperty CritRateLabelProperty = DependencyProperty.Register(
        nameof(CritRateLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? CritRateLabel
    {
        get => (string?)GetValue(CritRateLabelProperty);
        set => SetValue(CritRateLabelProperty, value);
    }

    public static readonly DependencyProperty CritRateProperty = DependencyProperty.Register(
        nameof(CritRate), typeof(double), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(double)));

    public double CritRate
    {
        get => (double)GetValue(CritRateProperty);
        set => SetValue(CritRateProperty, value);
    }

    public static readonly DependencyProperty CritCountProperty = DependencyProperty.Register(
        nameof(CritCount), typeof(long), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(long)));

    public long CritCount
    {
        get => (long)GetValue(CritCountProperty);
        set => SetValue(CritCountProperty, value);
    }

    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(
        nameof(Total), typeof(long), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(long)));

    public long Total
    {
        get => (long)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    public static readonly DependencyProperty TotalLabelProperty = DependencyProperty.Register(
        nameof(TotalLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? TotalLabel
    {
        get => (string?)GetValue(TotalLabelProperty);
        set => SetValue(TotalLabelProperty, value);
    }

    public static readonly DependencyProperty AverageProperty = DependencyProperty.Register(
        nameof(Average), typeof(double), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(double)));

    public double Average
    {
        get => (double)GetValue(AverageProperty);
        set => SetValue(AverageProperty, value);
    }

    public static readonly DependencyProperty AverageLabelProperty = DependencyProperty.Register(
        nameof(AverageLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? AverageLabel
    {
        get => (string?)GetValue(AverageLabelProperty);
        set => SetValue(AverageLabelProperty, value);
    }

    public static readonly DependencyProperty LuckyCountProperty = DependencyProperty.Register(
        nameof(LuckyCount), typeof(long), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(long)));

    public long LuckyCount
    {
        get => (long)GetValue(LuckyCountProperty);
        set => SetValue(LuckyCountProperty, value);
    }

    public static readonly DependencyProperty LuckyCountLabelProperty = DependencyProperty.Register(
        nameof(LuckyCountLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? LuckyCountLabel
    {
        get => (string?)GetValue(LuckyCountLabelProperty);
        set => SetValue(LuckyCountLabelProperty, value);
    }

    public static readonly DependencyProperty LuckyRateProperty = DependencyProperty.Register(
        nameof(LuckyRate), typeof(double), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(double)));

    public double LuckyRate
    {
        get => (double)GetValue(LuckyRateProperty);
        set => SetValue(LuckyRateProperty, value);
    }
}
