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

    public static readonly DependencyProperty LuckyRateLabelProperty = DependencyProperty.Register(
        nameof(LuckyRateLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? LuckyRateLabel
    {
        get => (string?)GetValue(LuckyRateLabelProperty);
        set => SetValue(LuckyRateLabelProperty, value);
    }

    public static readonly DependencyProperty LuckyRateProperty = DependencyProperty.Register(
        nameof(LuckyRate), typeof(double), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(double)));

    public double LuckyRate
    {
        get => (double)GetValue(LuckyRateProperty);
        set => SetValue(LuckyRateProperty, value);
    }

    // ? 新增：普通伤害
    public static readonly DependencyProperty NormalDamageProperty = DependencyProperty.Register(
        nameof(NormalDamage), typeof(long), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(long)));

    public long NormalDamage
    {
        get => (long)GetValue(NormalDamageProperty);
        set => SetValue(NormalDamageProperty, value);
    }

    // ? 新增：暴击伤害
    public static readonly DependencyProperty CritDamageProperty = DependencyProperty.Register(
        nameof(CritDamage), typeof(long), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(long)));

    public long CritDamage
    {
        get => (long)GetValue(CritDamageProperty);
        set => SetValue(CritDamageProperty, value);
    }

    // ? 新增：幸运伤害
    public static readonly DependencyProperty LuckyDamageProperty = DependencyProperty.Register(
        nameof(LuckyDamage), typeof(long), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(long)));

    public long LuckyDamage
    {
        get => (long)GetValue(LuckyDamageProperty);
        set => SetValue(LuckyDamageProperty, value);
    }

    // ? 新增：普通数据标签（用于显示"普通伤害"/"普通治疗"/"普通承伤"）
    public static readonly DependencyProperty NormalLabelProperty = DependencyProperty.Register(
        nameof(NormalLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? NormalLabel
    {
        get => (string?)GetValue(NormalLabelProperty);
        set => SetValue(NormalLabelProperty, value);
    }

    // ? 新增：暴击数据标签
    public static readonly DependencyProperty CritLabelProperty = DependencyProperty.Register(
        nameof(CritLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? CritLabel
    {
        get => (string?)GetValue(CritLabelProperty);
        set => SetValue(CritLabelProperty, value);
    }

    // ? 新增：幸运数据标签
    public static readonly DependencyProperty LuckyLabelProperty = DependencyProperty.Register(
        nameof(LuckyLabel), typeof(string), typeof(SkillStatsSummaryPanel), new PropertyMetadata(default(string?)));

    public string? LuckyLabel
    {
        get => (string?)GetValue(LuckyLabelProperty);
        set => SetValue(LuckyLabelProperty, value);
    }

    public static readonly DependencyProperty NormalHitTypeLabelKeyProperty =
    DependencyProperty.Register(
        nameof(NormalHitTypeLabelKey),
        typeof(string),
        typeof(SkillStatsSummaryPanel),
        new PropertyMetadata("Common_HitType_Normal"));

    public string NormalHitTypeLabelKey
    {
        get => (string)GetValue(NormalHitTypeLabelKeyProperty);
        set => SetValue(NormalHitTypeLabelKeyProperty, value);
    }

    public static readonly DependencyProperty LuckyHitTypeLabelKeyProperty =
        DependencyProperty.Register(
            nameof(LuckyHitTypeLabelKey),
            typeof(string),
            typeof(SkillStatsSummaryPanel),
            new PropertyMetadata("Common_HitType_Lucky"));

    public string LuckyHitTypeLabelKey
    {
        get => (string)GetValue(LuckyHitTypeLabelKeyProperty);
        set => SetValue(LuckyHitTypeLabelKeyProperty, value);
    }

    public static readonly DependencyProperty CriticalHitTypeLabelKeyProperty =
        DependencyProperty.Register(
            nameof(CriticalHitTypeLabelKey),
            typeof(string),
            typeof(SkillStatsSummaryPanel),
            new PropertyMetadata("Common_HitType_Critical"));

    public string CriticalHitTypeLabelKey
    {
        get => (string)GetValue(CriticalHitTypeLabelKeyProperty);
        set => SetValue(CriticalHitTypeLabelKeyProperty, value);
    }

    public static readonly DependencyProperty HitsLabelKeyProperty =
    DependencyProperty.Register(
        nameof(HitsLabelKey),
        typeof(string),
        typeof(SkillStatsSummaryPanel),
        new PropertyMetadata("SkillBreakdown_Label_HitCount"));

    public string HitsLabelKey
    {
        get => (string)GetValue(HitsLabelKeyProperty);
        set => SetValue(HitsLabelKeyProperty, value);
    }
}
