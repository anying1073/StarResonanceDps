using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;
using StarResonanceDpsAnalysis.WPF.Helpers;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Properties;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public record PlotOptions
{
    public string? SeriesPlotTitle { get; init; }
    public string? XAxisTitle { get; init; }
    public string? YAxisTitle { get; init; }
    public string? LineSeriesTitle { get; init; }
    public string? PiePlotTitle { get; init; }
    public string? DistributionPlotTitle { get; set; }
    public string? HitTypeNormal { get; set; }
    public string? HitTypeCritical { get; set; }
    public string? HitTypeLucky { get; set; }
    public required StatisticType StatisticType { get; set; }
    public string? HitTypeCriticalLucky { get; set; }
}

public partial class PlotViewModel : BaseViewModel
{
    private readonly StatisticType _statisticType;
    private readonly CategoryAxis _hitTypeBarCategoryAxis;
    
    [ObservableProperty] private PlotModel _hitTypeBarPlotModel;
    [ObservableProperty] private PlotModel _piePlotModel;
    [ObservableProperty] private PlotModel _seriesPlotModel;

    public PlotViewModel(PlotOptions? options)
    {
        _statisticType = options?.StatisticType ?? StatisticType.TakenDamage;
        
        // ⭐ Use custom smoothed line series for better visual appearance with Catmull-Rom spline interpolation
        LineSeriesData = new SmoothLineSeries
        {
            Title = options?.LineSeriesTitle,
            Color = OxyColor.FromRgb(230, 74, 25),
            StrokeThickness = 2,
            MarkerType = MarkerType.None,
            CanTrackerInterpolatePoints = true,
            InterpolationSteps = 8 // Number of interpolated points between each data point (8-12 recommended)
        };
        
        _seriesPlotModel = new PlotModel
        {
            // Title = options?.SeriesPlotTitle,
            Background = OxyColors.Transparent,
            PlotAreaBorderColor = OxyColor.FromRgb(224, 224, 224),
            Axes =
            {
                new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = options?.XAxisTitle,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                    AbsoluteMinimum = 0,
                    Minimum = 0,
                    MinimumPadding = 0,
                    MaximumPadding = 0.01
                },
                new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = options?.YAxisTitle,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColor.FromRgb(240, 240, 240),
                    AbsoluteMinimum = 0,
                    Minimum = 0,
                    MinimumPadding = 0,
                    MaximumPadding = 0.2,
                    // ⭐ 添加自定义格式化器，避免科学计数法显示
                    LabelFormatter = value =>
                        NumberFormatHelper.FormatHumanReadable(
                            value,
                            DamageDisplayMode,
                            SeriesPlotModel?.ActualCulture ?? CultureInfo.CurrentUICulture)
                }
            },
            Series = { LineSeriesData }
        };

        PieSeriesData = new SkillPieSeries
        {
            StrokeThickness = 2,
            InsideLabelPosition = 0.78,
            AngleSpan = 360,
            StartAngle = -90,
            InsideLabelColor = OxyColors.White,
            InsideLabelFormat = "{2:0.0}%",
            OutsideLabelFormat = "{1}",
            TickDistance = 2,
            TickRadialLength = 8,
            TickHorizontalLength = 8,
            TickLabelDistance = 4,
            Stroke = OxyColors.White,
            FontSize = 11,
            FontWeight = 600,
            // ⭐ Set tooltip format to show skill name and percentage on hover
            TrackerFormatString = "{1}({4}):\n{2:N0}({5})\n{3:0.0}%",
        };
        _piePlotModel = new PlotModel
        {
            // Title = options?.PiePlotTitle, // Use XAML title instead
            Background = OxyColors.Transparent,
            Padding = new OxyThickness(0, 16, 0, 16),
            Series = { PieSeriesData }
        };
        
        // Add Legend to show all skills - move to right side for better layout
        _piePlotModel.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.RightMiddle,
            LegendOrientation = LegendOrientation.Vertical,
            LegendPlacement = LegendPlacement.Outside,
            LegendBorderThickness = 0,
            LegendFontSize = 10,
            LegendTextColor = OxyColor.Parse("#666666"),
            LegendSymbolPlacement = LegendSymbolPlacement.Left,
            LegendItemSpacing = 8,
            LegendSymbolLength = 16,
            LegendSymbolMargin = 8
        });

        // Bar chart model for hit type percentages (Normal, Critical, Lucky)
        (_hitTypeBarPlotModel, _hitTypeBarCategoryAxis) = CreateHitTypeBarPlotModel(options);
    }

    public SmoothLineSeries LineSeriesData { get; }
    public SkillPieSeries PieSeriesData { get; }
    public NumberDisplayMode DamageDisplayMode { get; set; } = NumberDisplayMode.KMB;
    
    /// <summary>
    /// ⭐ 公开StatisticType属性供外部访问
    /// </summary>
    public StatisticType StatisticType => _statisticType;

    private const double PieMergeRangeStartPercent = 60.0;
    private const double PieMergeRangeEndPercent = 90.0;
    private const double PieMergeMinKeepPercent = 6.0;
    private const double PieMergedMaxPercent = 35.0;
    public void SetPieSeriesData(IReadOnlyList<SkillItemViewModel> skills)
    {
        /*
         
        Core idea: Sort items by percentage,
        compute the average value within the [PieMergeRangeStartPercent] ~ [PieMergeRangeEndPercent] range, round it up,
        then merge items whose percentage is below this threshold and below [PieMergeMinKeepPercent] into "Others",
        stopping when the merged slice would exceed [PieMergedMaxPercent],
        and for slices below [PieMergeMinKeepPercent], alternate hiding inside/outside labels to reduce overlap.

        核心思路: 先按占比排序,
        计算 [PieMergeRangeStartPercent] ~ [PieMergeRangeEndPercent] 段的平均值在向上取整后,
        将低于该阈值且占比低于 [PieMergeMinKeepPercent] 的项合并为 "其它",
        合并过程中若将超过 [PieMergedMaxPercent] 则停止合并,
        对于占比低于 [PieMergeMinKeepPercent] 的项, 内外标签交替隐藏以减少重叠

         */

        PieSeriesData.Slices.Clear();
        PieSeriesData.SliceInfoMap.Clear();
        PieSeriesData.HideInsideLabelSlices.Clear();
        PieSeriesData.HideOutsideLabelSlices.Clear();

        if (skills == null || skills.Count == 0)
        {
            RefreshPie();
            return;
        }

        var totalValue = skills.Sum(item => (double)item.TotalValue);
        if (totalValue <= 0)
        {
            RefreshPie();
            return;
        }

        var percentBySkill = new Dictionary<SkillItemViewModel, double>(skills.Count);
        foreach (var skill in skills)
        {
            percentBySkill[skill] = skill.TotalValue / totalValue * 100.0;
        }

        var sortedByPercent = skills
            .Select(skill => (Skill: skill, Percent: percentBySkill[skill]))
            .OrderByDescending(item => item.Percent)
            .ToList();

        var thresholdPercent = CalculatePieMergeThreshold(sortedByPercent);
        var roundedThreshold = Math.Round(thresholdPercent, 1);
        var mergedPercent = 0.0;
        var mergeSet = new HashSet<SkillItemViewModel>();

        for (var i = sortedByPercent.Count - 1; i >= 0; i--)
        {
            var candidate = sortedByPercent[i];
            if (candidate.Percent >= PieMergeMinKeepPercent ||
                candidate.Percent >= roundedThreshold)
            {
                continue;
            }

            mergeSet.Add(candidate.Skill);
            mergedPercent += candidate.Percent;

            if (mergedPercent > PieMergedMaxPercent)
            {
                break;
            }
        }

        var mergedTotalValue = 0L;
        var sliceIndex = 0;
        var mergedLabel = GetPieMergedLabel();
        var culture = CultureInfo.CurrentUICulture;
        var displayMode = DamageDisplayMode;
        var lowPercentSlices = new List<(PieSlice Slice, double Percent)>();

        foreach (var skill in skills)
        {
            if (mergeSet.Contains(skill))
            {
                mergedTotalValue += skill.TotalValue;
                continue;
            }

            var slice = new PieSlice(skill.SkillName, skill.TotalValue);
            PieSeriesData.Slices.Add(slice);
            PieSeriesData.SliceInfoMap[slice] = new SkillPieSeries.SliceInfo(
                skill.SkillName,
                skill.SkillId,
                skill.TotalValue,
                NumberFormatHelper.FormatHumanReadable(skill.TotalValue, displayMode, culture));

            var percent = percentBySkill[skill];
            if (percent < PieMergeMinKeepPercent)
            {
                lowPercentSlices.Add((slice, percent));
            }

            slice.IsExploded = false;
            slice.Fill = GetPaletteColor(sliceIndex++);
        }

        if (mergedTotalValue > 0)
        {
            var mergedSlice = new PieSlice(mergedLabel, mergedTotalValue)
            {
                IsExploded = false,
                Fill = GetPaletteColor(sliceIndex)
            };

            PieSeriesData.Slices.Add(mergedSlice);
            PieSeriesData.SliceInfoMap[mergedSlice] = new SkillPieSeries.SliceInfo(
                mergedLabel,
                0,
                mergedTotalValue,
                NumberFormatHelper.FormatHumanReadable(mergedTotalValue, displayMode, culture));
        }

        if (lowPercentSlices.Count > 0)
        {
            var showOutside = true;
            foreach (var item in lowPercentSlices.OrderBy(item => item.Percent))
            {
                if (showOutside)
                {
                    PieSeriesData.HideInsideLabelSlices.Add(item.Slice);
                }
                else
                {
                    PieSeriesData.HideOutsideLabelSlices.Add(item.Slice);
                }

                showOutside = !showOutside;
            }
        }

        RefreshPie();
    }

    private static double CalculatePieMergeThreshold(IReadOnlyList<(SkillItemViewModel Skill, double Percent)> sorted)
    {
        var cumulativePercent = 0.0;
        var selectedPercents = new List<double>();

        foreach (var item in sorted)
        {
            var nextCumulative = cumulativePercent + item.Percent;
            if (nextCumulative >= PieMergeRangeStartPercent && cumulativePercent < PieMergeRangeEndPercent)
            {
                selectedPercents.Add(item.Percent);
            }

            cumulativePercent = nextCumulative;
            if (cumulativePercent >= PieMergeRangeEndPercent)
            {
                break;
            }
        }

        return selectedPercents.Count == 0 ? 0 : selectedPercents.Average();
    }

    private static string GetPieMergedLabel()
    {
        var label = Resources.ResourceManager.GetString(
            ResourcesKeys.SkillBreakdown_Label_Others,
            CultureInfo.CurrentUICulture);

        if (!string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        label = Resources.ResourceManager.GetString(
            ResourcesKeys.SkillBreakdown_Label_Others,
            CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(label) ? "Others" : label;
    }

    public void SetHitTypeDistribution(double normalPercent, double criticalPercent, double luckyPercent)
    {
        if (HitTypeBarPlotModel.Series.Count == 0)
        {
            return;
        }

        if (HitTypeBarPlotModel.Series[0] is not BarSeries barSeries)
        {
            return;
        }

        barSeries.Items.Clear();
        barSeries.Items.Add(new BarItem(normalPercent));
        barSeries.Items.Add(new BarItem(criticalPercent));
        barSeries.Items.Add(new BarItem(luckyPercent));

        HitTypeBarPlotModel.InvalidatePlot(true);
    }

    public void RefreshSeries()
    {
        SeriesPlotModel.InvalidatePlot(true);
    }

    public void RefreshPie()
    {
        PiePlotModel.InvalidatePlot(true);
    }

    private static List<OxyColor> GenerateDistinctColors(int count)
    {
        var colors = new List<OxyColor>();
        var hueStep = 360.0 / count;

        for (var i = 0; i < count; i++)
        {
            var hue = i * hueStep;
            colors.Add(OxyColor.FromHsv(hue / 360.0, 0.7, 0.9));
        }

        return colors;
    }

    private static OxyColor GetPaletteColor(int index)
    {
        var palette = new[]
        {
            OxyColor.Parse("#F44336"), // 1 赤
            OxyColor.Parse("#F0622D"), // 2 赤橙
            OxyColor.Parse("#FB8C00"), // 3 橙
            OxyColor.Parse("#F9A825"), // 4 黄橙
            OxyColor.Parse("#FDD835"), // 5 黄
            OxyColor.Parse("#C0CA33"), // 6 黄緑
            OxyColor.Parse("#7CB342"), // 7 緑
            OxyColor.Parse("#26A69A"), // 8 青緑
            OxyColor.Parse("#26C6DA"), // 9 水
            OxyColor.Parse("#29B6F6"), // 10 空
            OxyColor.Parse("#1E88E5"), // 11 青
            OxyColor.Parse("#3949AB"), // 12 藍
            OxyColor.Parse("#5E35B1"), // 13 青紫
            OxyColor.Parse("#9C27B0")  // 14 紫
        };

        /*
        // Google Material Design Colors (approx)
        var palette = new[]
        {
            OxyColor.Parse("#F44336"), // Red
            OxyColor.Parse("#4CAF50"), // Green
            OxyColor.Parse("#2196F3"), // Blue
            OxyColor.Parse("#FFC107"), // Amber
            OxyColor.Parse("#9C27B0"), // Purple
            OxyColor.Parse("#00BCD4"), // Cyan
            OxyColor.Parse("#FF9800"), // Orange
            OxyColor.Parse("#E91E63"), // Pink
            OxyColor.Parse("#3F51B5"), // Indigo
            OxyColor.Parse("#009688"), // Teal
            OxyColor.Parse("#CDDC39"), // Lime
            OxyColor.Parse("#673AB7"), // Deep Purple
            OxyColor.Parse("#795548"), // Brown
            OxyColor.Parse("#607D8B")  // Blue Grey
        };
        */

        return palette[index % palette.Length];
    }

    private static (PlotModel model, CategoryAxis cateAxis) CreateHitTypeBarPlotModel(PlotOptions? options)
    {
        var model = new PlotModel
        {
            Background = OxyColors.Transparent,
            PlotAreaBorderColor = OxyColor.FromRgb(224, 224, 224),
        };

        // Categories on Y axis (required by BarSeriesBase)
        var categoryAxis = new CategoryAxis
        {
            Position = AxisPosition.Left,
            ItemsSource = new[]
            {
                options?.HitTypeNormal ?? "Normal", options?.HitTypeCritical ?? "Critical",
                options?.HitTypeLucky ?? "Lucky"
            }
        };

        // Values on X axis
        var valueAxis = new LinearAxis
        {
            Position = AxisPosition.Top,
            Minimum = 0,
            Maximum = 100,
            Title = options?.DistributionPlotTitle,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(240, 240, 240)
        };

        model.Axes.Add(categoryAxis);
        model.Axes.Add(valueAxis);

        var series = new BarSeries
        {
            FillColor = OxyColor.FromRgb(34, 151, 244)
        };

        series.Items.Add(new BarItem(0));
        series.Items.Add(new BarItem(0));
        series.Items.Add(new BarItem(0));

        model.Series.Add(series);

        return (model, categoryAxis);
    }

    public void ResetModelZoom()
    {
        var model = SeriesPlotModel;
        if (model.Axes.Count == 0) return;

        foreach (var axis in model.Axes)
        {
            if (axis.Position is AxisPosition.Bottom or AxisPosition.Top)
            {
                axis.Reset();
            }
        }

        model.InvalidatePlot(true);
    }

    public void ApplyZoomToModel(double zoomLevel)
    {
        var model = SeriesPlotModel;
        if (model.Axes.Count == 0) return;

        foreach (var axis in model.Axes)
        {
            if (axis.Position != AxisPosition.Bottom && axis.Position != AxisPosition.Top)
                continue;

            var dataMin = axis.DataMinimum;
            var dataMax = axis.DataMaximum;
            var dataRange = dataMax - dataMin;

            if (dataRange <= 0) continue;

            var visibleRange = dataRange / zoomLevel;
            var center = (dataMin + dataMax) / 2.0;
            var newMin = center - visibleRange / 2.0;
            var newMax = center + visibleRange / 2.0;

            if (newMin < dataMin)
            {
                newMin = dataMin;
                newMax = Math.Min(dataMin + visibleRange, dataMax);
            }

            if (newMax > dataMax)
            {
                newMax = dataMax;
                newMin = Math.Max(dataMax - visibleRange, dataMin);
            }

            axis.Zoom(newMin, newMax);
        }

        model.InvalidatePlot(true);
    }

    public void UpdateOption(PlotOptions plotOptions)
    {
        // SeriesPlotModel.Title = plotOptions.SeriesPlotTitle;
        LineSeriesData.Title = plotOptions.LineSeriesTitle;

        foreach (var axis in SeriesPlotModel.Axes)
        {
            axis.Title = axis.Position switch
            {
                AxisPosition.Bottom => plotOptions.XAxisTitle,
                AxisPosition.Left => plotOptions.YAxisTitle,
                _ => axis.Title
            };
        }

        SeriesPlotModel.InvalidatePlot(true);

        // PiePlotModel.Title = plotOptions.PiePlotTitle;
        PiePlotModel.InvalidatePlot(true);

        _hitTypeBarCategoryAxis.ItemsSource = new List<string>
        {
            plotOptions.HitTypeNormal ?? "Normal", plotOptions.HitTypeCritical ?? "Critical",
            plotOptions.HitTypeLucky ?? "Lucky"
        };
        HitTypeBarPlotModel.InvalidatePlot(true);
    }
}
