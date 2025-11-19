using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public record PlotOptions
{
    public string? SeriesPlotTitle { get; init; }
    public string? XAxisTitle { get; init; }
    public string? YAxisTitle { get; init; }
    public string? LineSeriesTitle { get; init; }
    public string? PiePlotTitle { get; init; }
}

public partial class PlotViewModel : BaseViewModel
{
    public PlotViewModel(PlotOptions? options)
    {
        LineSeriesData = new LineSeries
        {
            Title = options?.LineSeriesTitle,
            Color = OxyColor.FromRgb(230, 74, 25),
            StrokeThickness = 2,
            MarkerType = MarkerType.None
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
            StrokeThickness = 2,
            InsideLabelPosition = 0.5,
            AngleSpan = 360,
            StartAngle = 0,
            InsideLabelColor = OxyColors.White,
            InsideLabelFormat = "{1}: {2:0}",
            Stroke = OxyColors.White
        };
        _piePlotModel = new PlotModel
        {
            Title = options?.PiePlotTitle,
            Background = OxyColors.Transparent,
            Series = { PieSeriesData }
        };
    }

    public LineSeries LineSeriesData { get; }
    public PieSeries PieSeriesData { get; }
    [ObservableProperty] private PlotModel _seriesPlotModel;
    [ObservableProperty] private PlotModel _piePlotModel;

    public void SetPieSeriesData(IReadOnlyList<SkillItemViewModel> skills)
    {
        var colors = GenerateDistinctColors(skills.Count);

        for (var i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            PieSeriesData.Slices.Add(new PieSlice(skill.SkillName, skill.TotalDamage)
            {
                IsExploded = false,
                Fill = colors[i]
            });
        }

        RefreshPie();
    }

    public void RefreshSeries()
    {
        _seriesPlotModel.InvalidatePlot(true);
    }

    public void RefreshPie()
    {
        _piePlotModel.InvalidatePlot(true);
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
                newMax = System.Math.Min(dataMin + visibleRange, dataMax);
            }

            if (newMax > dataMax)
            {
                newMax = dataMax;
                newMin = System.Math.Max(dataMax - visibleRange, dataMin);
            }

            axis.Zoom(newMin, newMax);
        }

        model.InvalidatePlot(true);
    }
}
