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

    public static readonly DependencyProperty LuckyCountLabelProperty = DependencyProperty.Register(
        nameof(LuckyCountLabel), typeof(string), typeof(TabContentPanel), new PropertyMetadata(default(string?)));

    public string? LuckyCountLabel
    {
        get => (string?)GetValue(LuckyCountLabelProperty);
        set => SetValue(LuckyCountLabelProperty, value);
    }

    public static readonly DependencyProperty LuckyRateProperty = DependencyProperty.Register(
        nameof(LuckyRate), typeof(double), typeof(TabContentPanel), new PropertyMetadata(default(double)));

    public double LuckyRate
    {
        get => (double)GetValue(LuckyRateProperty);
        set => SetValue(LuckyRateProperty, value);
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