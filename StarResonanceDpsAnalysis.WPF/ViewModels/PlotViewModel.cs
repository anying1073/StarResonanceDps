using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using StarResonanceDpsAnalysis.WPF.Models;

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
            Title = options?.SeriesPlotTitle,
            Background = OxyColors.Transparent,
            PlotAreaBorderColor = OxyColor.FromRgb(224, 224, 224),
            Axes =
            {
                new LinearAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = options?.XAxisTitle,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColor.FromRgb(240, 240, 240)
                },
                new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = options?.YAxisTitle,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineColor = OxyColor.FromRgb(240, 240, 240)
                }
            },
            Series = { LineSeriesData }
        };

        PieSeriesData = new PieSeries
        {
            StrokeThickness = 3,
            InsideLabelPosition = 0.6,
            AngleSpan = 360,
            StartAngle = -90,
            InsideLabelColor = OxyColors.White,
            InsideLabelFormat = "{1}\n{2:0}%",
            Stroke = OxyColors.White,
            FontSize = 12,
            FontWeight = 700 // Bold
        };
        _piePlotModel = new PlotModel
        {
            Title = options?.PiePlotTitle,
            Background = OxyColors.Transparent,
            Series = { PieSeriesData }
        };
        
        // Add Legend
        _piePlotModel.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.BottomCenter,
            LegendOrientation = LegendOrientation.Horizontal,
            LegendPlacement = LegendPlacement.Outside,
            LegendBorderThickness = 0,
            LegendFontSize = 12,
            LegendTextColor = OxyColor.Parse("#666666"),
            LegendSymbolPlacement = LegendSymbolPlacement.Left
        });

        // Bar chart model for hit type percentages (Normal, Critical, Lucky)
        (_hitTypeBarPlotModel, _hitTypeBarCategoryAxis) = CreateHitTypeBarPlotModel(options);
    }

    public SmoothLineSeries LineSeriesData { get; }
    public PieSeries PieSeriesData { get; }

    public void SetPieSeriesData(IReadOnlyList<SkillItemViewModel> skills)
    {
        var colors = GenerateDistinctColors(skills.Count);

        PieSeriesData.Slices.Clear();
        for (var i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            var value = _statisticType switch
            {
                StatisticType.Damage => skill.Damage,
                StatisticType.Healing => skill.Heal,
                StatisticType.TakenDamage => skill.TakenDamage,
                StatisticType.NpcTakenDamage => skill.TakenDamage,
                _ => throw new ArgumentOutOfRangeException()
            };
            PieSeriesData.Slices.Add(new PieSlice(skill.SkillName, value.TotalValue)
            {
                IsExploded = false,
                Fill = colors[i]
            });
        }

        RefreshPie();
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

    private static (PlotModel model, CategoryAxis cateAxis) CreateHitTypeBarPlotModel(PlotOptions? options)
    {
        var model = new PlotModel
        {
            Background = OxyColors.Transparent,
            PlotAreaBorderColor = OxyColor.FromRgb(224, 224, 224)
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
        SeriesPlotModel.Title = plotOptions.SeriesPlotTitle;
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

        PiePlotModel.Title = plotOptions.PiePlotTitle;
        PiePlotModel.InvalidatePlot(true);

        _hitTypeBarCategoryAxis.ItemsSource = new List<string>
        {
            plotOptions.HitTypeNormal ?? "Normal", plotOptions.HitTypeCritical ?? "Critical",
            plotOptions.HitTypeLucky ?? "Lucky"
        };
        HitTypeBarPlotModel.InvalidatePlot(true);
    }
}