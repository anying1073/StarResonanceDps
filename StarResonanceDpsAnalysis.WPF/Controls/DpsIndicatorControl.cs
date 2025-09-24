using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarResonanceDpsAnalysis.WPF.Controls;

public class DpsIndicatorControl : Control
{
    static DpsIndicatorControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DpsIndicatorControl),
            new FrameworkPropertyMetadata(typeof(DpsIndicatorControl)));
    }

    public DpsIndicatorControl()
    {
        // Add mouse event handlers for debugging
        this.MouseEnter += (s, e) => Debug.WriteLine($"[DpsIndicatorControl] MouseEnter - PopupContent: {PopupContent?.GetType().Name ?? "null"}");
        this.MouseLeave += (s, e) => Debug.WriteLine($"[DpsIndicatorControl] MouseLeave");
    }

    // Percentage value in range 0..Maximum. Use double for proper binding with ProgressBar-like behavior.
    public static readonly DependencyProperty PercentageProperty = DependencyProperty.Register(
        nameof(Percentage), typeof(double), typeof(DpsIndicatorControl),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPercentageChanged));

    public double Percentage
    {
        get => (double)GetValue(PercentageProperty);
        set => SetValue(PercentageProperty, value);
    }

    // AnimatedPercentage: used by the template to animate visual changes when Percentage updates
    public static readonly DependencyProperty AnimatedPercentageProperty = DependencyProperty.Register(
        nameof(AnimatedPercentage), typeof(double), typeof(DpsIndicatorControl),
        new PropertyMetadata(0d));

    public double AnimatedPercentage
    {
        get => (double)GetValue(AnimatedPercentageProperty);
        set => SetValue(AnimatedPercentageProperty, value);
    }

    private static void OnPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DpsIndicatorControl ctl) return;

        var newVal = (double)e.NewValue;

        // Create smooth animation from current AnimatedPercentage to new Percentage
        var animation = new DoubleAnimation
        {
            To = newVal,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Begin animation on AnimatedPercentageProperty
        ctl.BeginAnimation(AnimatedPercentageProperty, animation);
    }

    // Maximum value used to scale Percentage (default 100)
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(DpsIndicatorControl),
        new PropertyMetadata(100d));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    // Background brush for the track
    public static readonly DependencyProperty IndicatorBackgroundProperty = DependencyProperty.Register(
        nameof(IndicatorBackground), typeof(Brush), typeof(DpsIndicatorControl),
        new PropertyMetadata(Brushes.LightGray));

    public Brush? IndicatorBackground
    {
        get => (Brush?)GetValue(IndicatorBackgroundProperty);
        set => SetValue(IndicatorBackgroundProperty, value);
    }

    // Foreground / fill brush for the indicator
    public static readonly DependencyProperty IndicatorForegroundProperty = DependencyProperty.Register(
        nameof(IndicatorForeground), typeof(Brush), typeof(DpsIndicatorControl),
        new PropertyMetadata(Brushes.DodgerBlue));

    public Brush? IndicatorForeground
    {
        get => (Brush?)GetValue(IndicatorForegroundProperty);
        set => SetValue(IndicatorForegroundProperty, value);
    }

    // CornerRadius for rounded track/indicator
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(DpsIndicatorControl),
        new PropertyMetadata(new CornerRadius(4)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    // Template used to render overlay content on top of the progress indicator.
    public static readonly DependencyProperty OverlayTemplateProperty = DependencyProperty.Register(
        nameof(OverlayTemplate), typeof(DataTemplate), typeof(DpsIndicatorControl),
        new PropertyMetadata(null));

    public DataTemplate? OverlayTemplate
    {
        get => (DataTemplate?)GetValue(OverlayTemplateProperty);
        set => SetValue(OverlayTemplateProperty, value);
    }

    // Content to be passed to the overlay template.
    public static readonly DependencyProperty OverlayContentProperty = DependencyProperty.Register(
        nameof(OverlayContent), typeof(object), typeof(DpsIndicatorControl),
        new PropertyMetadata(null));

    public object? OverlayContent
    {
        get => GetValue(OverlayContentProperty);
        set => SetValue(OverlayContentProperty, value);
    }

    public static readonly DependencyProperty PopupTemplateProperty = DependencyProperty.Register(
        nameof(PopupTemplate), typeof(DataTemplate), typeof(DpsIndicatorControl), 
        new PropertyMetadata(default(DataTemplate?), OnPopupTemplateChanged));

    public DataTemplate? PopupTemplate
    {
        get => (DataTemplate?)GetValue(PopupTemplateProperty);
        set => SetValue(PopupTemplateProperty, value);
    }

    private static void OnPopupTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Debug.WriteLine($"[DpsIndicatorControl] PopupTemplate changed: {e.NewValue?.GetType().Name ?? "null"}");
    }

    public static readonly DependencyProperty PopupContentProperty = DependencyProperty.Register(
        nameof(PopupContent), typeof(object), typeof(DpsIndicatorControl), 
        new PropertyMetadata(default(object?), OnPopupContentChanged));

    public object? PopupContent
    {
        get => (object?)GetValue(PopupContentProperty);
        set => SetValue(PopupContentProperty, value);
    }

    private static void OnPopupContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var oldPlayerName = (e.OldValue as dynamic)?.Player?.Name ?? "null";
        var newPlayerName = (e.NewValue as dynamic)?.Player?.Name ?? "null";
        Debug.WriteLine($"[DpsIndicatorControl] PopupContent changed: {oldPlayerName} -> {newPlayerName}");
    }
}