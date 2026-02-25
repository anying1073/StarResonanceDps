using System.Windows;
using System.Windows.Controls;
using OxyPlot;

namespace StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;

public class ChartPanel : Control
{
    static ChartPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ChartPanel),
            new FrameworkPropertyMetadata(typeof(ChartPanel)));
    }

    public static readonly DependencyProperty PieChartTitleProperty = DependencyProperty.Register(
        nameof(PieChartTitle), typeof(string), typeof(ChartPanel), new PropertyMetadata(default(string?)));

    public string? PieChartTitle
    {
        get => (string?)GetValue(PieChartTitleProperty);
        set => SetValue(PieChartTitleProperty, value);
    }

    public static readonly DependencyProperty LuckyCountProperty = DependencyProperty.Register(
        nameof(LuckyCount), typeof(long), typeof(ChartPanel), new PropertyMetadata(default(long)));

    public long LuckyCount
    {
        get => (long)GetValue(LuckyCountProperty);
        set => SetValue(LuckyCountProperty, value);
    }

    public static readonly DependencyProperty LuckyRateProperty = DependencyProperty.Register(
        nameof(LuckyRate), typeof(double), typeof(ChartPanel), new PropertyMetadata(default(double)));

    public double LuckyRate
    {
        get => (double)GetValue(LuckyRateProperty);
        set => SetValue(LuckyRateProperty, value);
    }

    public static readonly DependencyProperty LuckyPercentageProperty = DependencyProperty.Register(
        nameof(LuckyPercentage), typeof(double), typeof(ChartPanel), new PropertyMetadata(default(double)));

    public double LuckyPercentage
    {
        get => (double)GetValue(LuckyPercentageProperty);
        set => SetValue(LuckyPercentageProperty, value);
    }

    public static readonly DependencyProperty CritLuckyCountProperty = DependencyProperty.Register(
        nameof(CritLuckyCount), typeof(long), typeof(ChartPanel), new PropertyMetadata(default(long)));

    public long CritLuckyCount
    {
        get => (long)GetValue(CritLuckyCountProperty);
        set => SetValue(CritLuckyCountProperty, value);
    }

    public static readonly DependencyProperty CritLuckyRateProperty = DependencyProperty.Register(
        nameof(CritLuckyRate), typeof(double), typeof(ChartPanel), new PropertyMetadata(default(double)));

    public double CritLuckyRate
    {
        get => (double)GetValue(CritLuckyRateProperty);
        set => SetValue(CritLuckyRateProperty, value);
    }

    public static readonly DependencyProperty CritLuckyPercentageProperty = DependencyProperty.Register(
        nameof(CritLuckyPercentage), typeof(double), typeof(ChartPanel), new PropertyMetadata(default(double)));

    public double CritLuckyPercentage
    {
        get => (double)GetValue(CritLuckyPercentageProperty);
        set => SetValue(CritLuckyPercentageProperty, value);
    }

    public static readonly DependencyProperty CritCountProperty = DependencyProperty.Register(
        nameof(CritCount), typeof(long), typeof(ChartPanel), new PropertyMetadata(default(long)));

    public long CritCount
    {
        get => (long)GetValue(CritCountProperty);
        set => SetValue(CritCountProperty, value);
    }

    public static readonly DependencyProperty CritRateProperty = DependencyProperty.Register(
        nameof(CritRate), typeof(double), typeof(ChartPanel), new PropertyMetadata(default(double)));

    public double CritRate
    {
        get => (double)GetValue(CritRateProperty);
        set => SetValue(CritRateProperty, value);
    }

    public static readonly DependencyProperty NormalCountProperty = DependencyProperty.Register(
        nameof(NormalCount), typeof(long), typeof(ChartPanel), new PropertyMetadata(default(long)));

    public long NormalCount
    {
        get => (long)GetValue(NormalCountProperty);
        set => SetValue(NormalCountProperty, value);
    }

    public static readonly DependencyProperty NormalRateProperty = DependencyProperty.Register(
        nameof(NormalRate), typeof(double), typeof(ChartPanel), new PropertyMetadata(default(double)));

    public double NormalRate
    {
        get => (double)GetValue(NormalRateProperty);
        set => SetValue(NormalRateProperty, value);
    }

    public static readonly DependencyProperty TimeSeriesTitleProperty = DependencyProperty.Register(
        nameof(TimeSeriesTitle), typeof(string), typeof(ChartPanel), new PropertyMetadata(default(string?)));

    public string? TimeSeriesTitle
    {
        get => (string?)GetValue(TimeSeriesTitleProperty);
        set => SetValue(TimeSeriesTitleProperty, value);
    }

    public static readonly DependencyProperty ChartTitleProperty = DependencyProperty.Register(
        nameof(ChartTitle), typeof(string), typeof(ChartPanel), new PropertyMetadata(default(string?)));

    public string? ChartTitle
    {
        get => (string?)GetValue(ChartTitleProperty);
        set => SetValue(ChartTitleProperty, value);
    }

    public static readonly DependencyProperty HitTypeChartTitleProperty = DependencyProperty.Register(
        nameof(HitTypeChartTitle), typeof(string), typeof(ChartPanel), new PropertyMetadata(default(string?)));

    public string? HitTypeChartTitle
    {
        get => (string?)GetValue(HitTypeChartTitleProperty);
        set => SetValue(HitTypeChartTitleProperty, value);
    }

    #region Plots

    public static readonly DependencyProperty SeriesPlotModelProperty = DependencyProperty.Register(
        nameof(SeriesPlotModel), typeof(IPlotModel), typeof(ChartPanel), new PropertyMetadata(default(IPlotModel)));

    public IPlotModel SeriesPlotModel
    {
        get => (IPlotModel)GetValue(SeriesPlotModelProperty);
        set => SetValue(SeriesPlotModelProperty, value);
    }

    public static readonly DependencyProperty PiePlotModelProperty = DependencyProperty.Register(
        nameof(PiePlotModel), typeof(IPlotModel), typeof(ChartPanel), new PropertyMetadata(default(IPlotModel)));

    public IPlotModel PiePlotModel
    {
        get => (IPlotModel)GetValue(PiePlotModelProperty);
        set => SetValue(PiePlotModelProperty, value);
    }

    #endregion

}
