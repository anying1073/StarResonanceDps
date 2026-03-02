using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;

public class TabContentPanel : Control
{
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(TabContentPanel),
            new PropertyMetadata(null));

    static TabContentPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TabContentPanel),
            new FrameworkPropertyMetadata(typeof(TabContentPanel)));
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly DependencyProperty AverageSortLabelKeyProperty =
    DependencyProperty.Register(
        nameof(AverageSortLabelKey),
        typeof(string),
        typeof(TabContentPanel),
        new PropertyMetadata("SkillBreakdown_Label_AverageDps"));

    public string AverageSortLabelKey
    {
        get => (string)GetValue(AverageSortLabelKeyProperty);
        set => SetValue(AverageSortLabelKeyProperty, value);
    }

    public static readonly DependencyProperty AverageColumnLabelKeyProperty =
        DependencyProperty.Register(
            nameof(AverageColumnLabelKey),
            typeof(string),
            typeof(TabContentPanel),
            new PropertyMetadata("SkillBreakdown_Label_AverageDamage"));

    public string AverageColumnLabelKey
    {
        get => (string)GetValue(AverageColumnLabelKeyProperty);
        set => SetValue(AverageColumnLabelKeyProperty, value);
    }

    public static readonly DependencyProperty NormalHitTypeLabelKeyProperty =
    DependencyProperty.Register(
        nameof(NormalHitTypeLabelKey),
        typeof(string),
        typeof(TabContentPanel),
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
            typeof(TabContentPanel),
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
            typeof(TabContentPanel),
            new PropertyMetadata("Common_HitType_Critical"));

    public string CriticalHitTypeLabelKey
    {
        get => (string)GetValue(CriticalHitTypeLabelKeyProperty);
        set => SetValue(CriticalHitTypeLabelKeyProperty, value);
    }

    public static readonly DependencyProperty HitCountLabelKeyProperty =
    DependencyProperty.Register(
        nameof(HitCountLabelKey),
        typeof(string),
        typeof(TabContentPanel),
        new PropertyMetadata("SkillBreakdown_Label_HitCount"));

    public string HitCountLabelKey
    {
        get => (string)GetValue(HitCountLabelKeyProperty);
        set => SetValue(HitCountLabelKeyProperty, value);
    }

    public static readonly DependencyProperty HitsLabelKeyProperty =
    DependencyProperty.Register(
        nameof(HitsLabelKey),
        typeof(string),
        typeof(TabContentPanel),
        new PropertyMetadata("SkillBreakdown_Label_TotalHits"));

    public string HitsLabelKey
    {
        get => (string)GetValue(HitsLabelKeyProperty);
        set => SetValue(HitsLabelKeyProperty, value);
    }

    #region SkillStats

    public static readonly DependencyProperty HitsLabelProperty = DependencyProperty.Register(
        nameof(HitsLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? HitsLabel
    {
        get => (string?)GetValue(HitsLabelProperty);
        set => SetValue(HitsLabelProperty, value);
    }

    public static readonly DependencyProperty HitsProperty = DependencyProperty.Register(
        nameof(Hits), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long Hits
    {
        get => (long)GetValue(HitsProperty);
        set => SetValue(HitsProperty, value);
    }

    public static readonly DependencyProperty CritRateLabelProperty = DependencyProperty.Register(
        nameof(CritRateLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? CritRateLabel
    {
        get => (string?)GetValue(CritRateLabelProperty);
        set => SetValue(CritRateLabelProperty, value);
    }

    public static readonly DependencyProperty CritRateProperty = DependencyProperty.Register(
        nameof(CritRate), typeof(double), typeof(TabContentPanel), new PropertyMetadata(default(double)));

    public double CritRate
    {
        get => (double)GetValue(CritRateProperty);
        set => SetValue(CritRateProperty, value);
    }

    public static readonly DependencyProperty CritCountProperty = DependencyProperty.Register(
        nameof(CritCount), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long CritCount
    {
        get => (long)GetValue(CritCountProperty);
        set => SetValue(CritCountProperty, value);
    }

    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(
        nameof(Total), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long Total
    {
        get => (long)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    public static readonly DependencyProperty TotalLabelProperty = DependencyProperty.Register(
        nameof(TotalLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? TotalLabel
    {
        get => (string?)GetValue(TotalLabelProperty);
        set => SetValue(TotalLabelProperty, value);
    }

    public static readonly DependencyProperty AverageProperty = DependencyProperty.Register(
        nameof(Average), typeof(double), typeof(TabContentPanel), new PropertyMetadata(default(double)));

    public double Average
    {
        get => (double)GetValue(AverageProperty);
        set => SetValue(AverageProperty, value);
    }

    public static readonly DependencyProperty AverageLabelProperty = DependencyProperty.Register(
        nameof(AverageLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? AverageLabel
    {
        get => (string?)GetValue(AverageLabelProperty);
        set => SetValue(AverageLabelProperty, value);
    }

    public static readonly DependencyProperty LuckyCountProperty = DependencyProperty.Register(
        nameof(LuckyCount), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long LuckyCount
    {
        get => (long)GetValue(LuckyCountProperty);
        set => SetValue(LuckyCountProperty, value);
    }

    public static readonly DependencyProperty LuckyRateLabelProperty = DependencyProperty.Register(
        nameof(LuckyRateLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? LuckyRateLabel
    {
        get => (string?)GetValue(LuckyRateLabelProperty);
        set => SetValue(LuckyRateLabelProperty, value);
    }

    public static readonly DependencyProperty LuckyRateProperty = DependencyProperty.Register(
        nameof(LuckyRate), typeof(double), typeof(TabContentPanel), new PropertyMetadata(default(double)));

    public double LuckyRate
    {
        get => (double)GetValue(LuckyRateProperty);
        set => SetValue(LuckyRateProperty, value);
    }

    public static readonly DependencyProperty CritLuckyCountProperty = DependencyProperty.Register(
       nameof(CritLuckyCount), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long CritLuckyCount
    {
        get => (long)GetValue(CritLuckyCountProperty);
        set => SetValue(CritLuckyCountProperty, value);
    }

    public static readonly DependencyProperty CritLuckyRateProperty = DependencyProperty.Register(
        nameof(CritLuckyRate), typeof(double), typeof(TabContentPanel), new PropertyMetadata(default(double)));

    public double CritLuckyRate
    {
        get => (double)GetValue(CritLuckyRateProperty);
        set => SetValue(CritLuckyRateProperty, value);
    }

    public static readonly DependencyProperty NormalCountProperty = DependencyProperty.Register(
        nameof(NormalCount), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long NormalCount
    {
        get => (long)GetValue(NormalCountProperty);
        set => SetValue(NormalCountProperty, value);
    }

    public static readonly DependencyProperty NormalRateProperty = DependencyProperty.Register(
        nameof(NormalRate), typeof(double), typeof(TabContentPanel), new PropertyMetadata(default(double)));

    public double NormalRate
    {
        get => (double)GetValue(NormalRateProperty);
        set => SetValue(NormalRateProperty, value);
    }

    /// <summary>
    /// 普通伤害
    /// </summary>
    public static readonly DependencyProperty NormalDamageProperty = DependencyProperty.Register(
        nameof(NormalDamage), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long NormalDamage
    {
        get => (long)GetValue(NormalDamageProperty);
        set => SetValue(NormalDamageProperty, value);
    }

    /// <summary>
    /// 暴击伤害
    /// </summary>
    public static readonly DependencyProperty CritDamageProperty = DependencyProperty.Register(
        nameof(CritDamage), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long CritDamage
    {
        get => (long)GetValue(CritDamageProperty);
        set => SetValue(CritDamageProperty, value);
    }

    /// <summary>
    /// 幸运伤害
    /// </summary>
    public static readonly DependencyProperty LuckyDamageProperty = DependencyProperty.Register(
        nameof(LuckyDamage), typeof(long), typeof(TabContentPanel), new PropertyMetadata(default(long)));

    public long LuckyDamage
    {
        get => (long)GetValue(LuckyDamageProperty);
        set => SetValue(LuckyDamageProperty, value);
    }

    /// <summary>
    /// 普通数据标签
    /// </summary>
    public static readonly DependencyProperty NormalLabelProperty = DependencyProperty.Register(
        nameof(NormalLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? NormalLabel
    {
        get => (string?)GetValue(NormalLabelProperty);
        set => SetValue(NormalLabelProperty, value);
    }

    /// <summary>
    /// 暴击数据标签
    /// </summary>
    public static readonly DependencyProperty CritLabelProperty = DependencyProperty.Register(
        nameof(CritLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? CritLabel
    {
        get => (string?)GetValue(CritLabelProperty);
        set => SetValue(CritLabelProperty, value);
    }

    /// <summary>
    /// 幸运数据标签
    /// </summary>
    public static readonly DependencyProperty LuckyLabelProperty = DependencyProperty.Register(
        nameof(LuckyLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? LuckyLabel
    {
        get => (string?)GetValue(LuckyLabelProperty);
        set => SetValue(LuckyLabelProperty, value);
    }

    #endregion

    #region SkillList

    public static readonly DependencyProperty SkillListTitleProperty = DependencyProperty.Register(
        nameof(SkillListTitle), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? SkillListTitle
    {
        get => (string?)GetValue(SkillListTitleProperty);
        set => SetValue(SkillListTitleProperty, value);
    }

    public static readonly DependencyProperty SkillItemsProperty = DependencyProperty.Register(nameof(SkillItems),
        typeof(ObservableCollection<SkillItemViewModel>), typeof(TabContentPanel),
        new PropertyMetadata(default(ObservableCollection<SkillItemViewModel>)));

    public ObservableCollection<SkillItemViewModel> SkillItems
    {
        get => (ObservableCollection<SkillItemViewModel>)GetValue(SkillItemsProperty);
        set => SetValue(SkillItemsProperty, value);
    }

    public static readonly DependencyProperty IconColorProperty = DependencyProperty.Register(nameof(IconColor),
        typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? IconColor
    {
        get => (string?)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    #endregion

    #region Plot

    public static readonly DependencyProperty PlotViewModelProperty = DependencyProperty.Register(
        nameof(PlotViewModel), typeof(PlotViewModel), typeof(TabContentPanel),
        new PropertyMetadata(default(PlotViewModel)));

    public PlotViewModel PlotViewModel
    {
        get => (PlotViewModel)GetValue(PlotViewModelProperty);
        set => SetValue(PlotViewModelProperty, value);
    }

    public static readonly DependencyProperty SeriesTitleProperty = DependencyProperty.Register(
        nameof(SeriesTitle), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? SeriesTitle
    {
        get => (string?)GetValue(SeriesTitleProperty);
        set => SetValue(SeriesTitleProperty, value);
    }

    public static readonly DependencyProperty PieChartTitleProperty = DependencyProperty.Register(
        nameof(PieChartTitle), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? PieChartTitle
    {
        get => (string?)GetValue(PieChartTitleProperty);
        set => SetValue(PieChartTitleProperty, value);
    }

    public static readonly DependencyProperty HitTypeChartTitleProperty = DependencyProperty.Register(
        nameof(HitTypeChartTitle), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? HitTypeChartTitle
    {
        get => (string?)GetValue(HitTypeChartTitleProperty);
        set => SetValue(HitTypeChartTitleProperty, value);
    }

    #endregion
}