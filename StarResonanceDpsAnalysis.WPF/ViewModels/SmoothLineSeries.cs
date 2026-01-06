using OxyPlot;
using OxyPlot.Series;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Custom LineSeries that provides smooth curve rendering using cubic spline interpolation
/// </summary>
public class SmoothLineSeries : LineSeries
{
    /// <summary>
    /// Number of interpolated points between each pair of data points
    /// Higher values = smoother curves but more computation
    /// </summary>
    public int InterpolationSteps { get; set; } = 10;

    protected override void RenderLine(IRenderContext rc, IList<ScreenPoint> pointsToRender)
    {
        if (pointsToRender.Count < 3)
        {
            // Not enough points for smoothing, use default rendering
            base.RenderLine(rc, pointsToRender);
            return;
        }

        // Create smoothed points using Catmull-Rom spline interpolation
        var smoothedPoints = InterpolateCatmullRom(pointsToRender, InterpolationSteps);
        
        // Render the smoothed line
        base.RenderLine(rc, smoothedPoints);
    }

    /// <summary>
    /// Interpolates points using Catmull-Rom spline for smooth curves
    /// This creates natural-looking curves that pass through all data points
    /// </summary>
    private static List<ScreenPoint> InterpolateCatmullRom(IList<ScreenPoint> points, int steps)
    {
        if (points.Count < 2)
            return new List<ScreenPoint>(points);

        var result = new List<ScreenPoint>(points.Count * steps);
        
        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i < points.Count - 2 ? points[i + 2] : points[i + 1];

            // Add the current point
            result.Add(p1);

            // Interpolate between p1 and p2
            for (int j = 1; j < steps; j++)
            {
                double t = (double)j / steps;
                var interpolated = CatmullRomInterpolate(p0, p1, p2, p3, t);
                result.Add(interpolated);
            }
        }

        // Add the last point
        result.Add(points[points.Count - 1]);

        return result;
    }

    /// <summary>
    /// Catmull-Rom spline interpolation formula
    /// Creates smooth curves that pass through control points p1 and p2
    /// </summary>
    private static ScreenPoint CatmullRomInterpolate(
        ScreenPoint p0, ScreenPoint p1, ScreenPoint p2, ScreenPoint p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        double x = 0.5 * (
            (2 * p1.X) +
            (-p0.X + p2.X) * t +
            (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
            (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3
        );

        double y = 0.5 * (
            (2 * p1.Y) +
            (-p0.Y + p2.Y) * t +
            (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3
        );

        return new ScreenPoint(x, y);
    }
}
