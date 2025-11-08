using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StarResonanceDpsAnalysis.WPF.Controls;

public partial class RingProgressBar : UserControl
{
    private static readonly DependencyPropertyKey RingDiameterPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(RingDiameter),
            typeof(double),
            typeof(RingProgressBar),
            new PropertyMetadata(0d, OnRingDiameterChanged));

    public static readonly DependencyProperty RingDiameterProperty = RingDiameterPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey InnerDiameterPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(InnerDiameter),
            typeof(double),
            typeof(RingProgressBar),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty InnerDiameterProperty = InnerDiameterPropertyKey.DependencyProperty;

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(double),
        typeof(RingProgressBar),
        new PropertyMetadata(0d, OnSizePropertyChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(RingProgressBar),
        new PropertyMetadata(0d, OnVisualPropertyChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(RingProgressBar),
        new PropertyMetadata(100d, OnVisualPropertyChanged));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness),
        typeof(double),
        typeof(RingProgressBar),
        new PropertyMetadata(6d, OnVisualPropertyChanged));

    public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(
        nameof(ProgressBrush),
        typeof(Brush),
        typeof(RingProgressBar),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xFF, 0x63, 0x47))));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(RingProgressBar),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF))));

    public static readonly DependencyProperty InnerContentProperty = DependencyProperty.Register(
        nameof(InnerContent),
        typeof(object),
        typeof(RingProgressBar),
        new PropertyMetadata(null));

    public static readonly DependencyProperty InnerContentTemplateProperty = DependencyProperty.Register(
        nameof(InnerContentTemplate),
        typeof(DataTemplate),
        typeof(RingProgressBar),
        new PropertyMetadata(null));

    public RingProgressBar()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double RingDiameter => (double)GetValue(RingDiameterProperty);

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double InnerDiameter => (double)GetValue(InnerDiameterProperty);

    public object? InnerContent
    {
        get => GetValue(InnerContentProperty);
        set => SetValue(InnerContentProperty, value);
    }

    public DataTemplate? InnerContentTemplate
    {
        get => (DataTemplate?)GetValue(InnerContentTemplateProperty);
        set => SetValue(InnerContentTemplateProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateRingSize();
        UpdateArc();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRingSize();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RingProgressBar control)
        {
            control.UpdateInnerDiameter();
            control.UpdateArc();
        }
    }

    private static void OnSizePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RingProgressBar control)
        {
            control.UpdateRingSize();
        }
    }

    private static void OnRingDiameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RingProgressBar control)
        {
            control.UpdateInnerDiameter();
            control.UpdateArc();
        }
    }

    private void UpdateArc()
    {
        if (!IsLoaded)
        {
            return;
        }

        var diameter = RingDiameter;

        if (diameter <= 0)
        {
            return;
        }

        var progressPath = ProgressPath;
        var percent = CalculateProgressRatio();

        if (percent <= 0)
        {
            progressPath.Data = null;
            return;
        }

        var angle = Math.Min(359.999, percent * 360d);
        var stroke = Math.Max(0, RingThickness);
        var radius = Math.Max(0, (diameter - stroke) / 2d);

        if (radius <= 0)
        {
            progressPath.Data = null;
            return;
        }

        var center = new Point(diameter / 2d, diameter / 2d);
        var startPoint = new Point(center.X, center.Y - radius);
        var endPoint = GetPointOnCircle(center, radius, angle);

        var figure = new PathFigure
        {
            IsClosed = false,
            IsFilled = false,
            StartPoint = startPoint
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            RotationAngle = 0,
            IsLargeArc = angle > 180d,
            SweepDirection = SweepDirection.Clockwise
        });

        progressPath.Data = new PathGeometry(new[] { figure });
    }

    private void UpdateRingSize()
    {
        var configuredSize = Size;
        double diameter;

        if (!double.IsNaN(configuredSize) && configuredSize > 0)
        {
            diameter = configuredSize;
        }
        else
        {
            var width = SanitizeDimension(ActualWidth);
            var height = SanitizeDimension(ActualHeight);

            if (width <= 0 || height <= 0)
            {
                diameter = 0;
            }
            else
            {
                diameter = Math.Min(width, height);
            }
        }

        SetValue(RingDiameterPropertyKey, diameter);
    }

    private static double SanitizeDimension(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return value;
    }


    private void UpdateInnerDiameter()
    {
        var diameter = RingDiameter;
        var thickness = Math.Max(0, RingThickness);
        var inner = diameter - (thickness * 2);
        if (inner < 0)
        {
            inner = 0;
        }

        SetValue(InnerDiameterPropertyKey, inner);
    }

    private double CalculateProgressRatio()
    {
        var max = Maximum;
        var value = Value;

        if (double.IsNaN(max) || max <= 0 || double.IsNaN(value))
        {
            return 0;
        }

        var ratio = value / max;
        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            return 0;
        }

        return Math.Max(0, Math.Min(1, ratio));
    }

    private static Point GetPointOnCircle(Point center, double radius, double angleDegree)
    {
        var radians = angleDegree * Math.PI / 180d;
        var x = center.X + radius * Math.Sin(radians);
        var y = center.Y - radius * Math.Cos(radians);
        return new Point(x, y);
    }
}
