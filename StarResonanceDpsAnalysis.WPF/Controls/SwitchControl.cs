using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarResonanceDpsAnalysis.WPF.Controls;

public class SwitchControl : ToggleButton
{
    public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register(
        nameof(OnContent), typeof(object), typeof(SwitchControl), new PropertyMetadata("On"));

    public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register(
        nameof(OffContent), typeof(object), typeof(SwitchControl), new PropertyMetadata("Off"));

    public static readonly DependencyProperty ContentPaddingProperty = DependencyProperty.Register(
        nameof(ContentPadding), typeof(Thickness), typeof(SwitchControl), new PropertyMetadata(new Thickness(0d)));

    private Border? _track;
    private Border? _thumb;
    private Border? _onBorder;
    private Border? _offBorder;
    private ContentControl? _onLabel;
    private ContentControl? _offLabel;
    private TranslateTransform? _thumbTranslate;
    private TranslateTransform? _onBorderTranslate;
    private TranslateTransform? _offBorderTranslate;
    static SwitchControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SwitchControl),
            new FrameworkPropertyMetadata(typeof(SwitchControl)));
    }

    public object OnContent
    {
        get => GetValue(OnContentProperty);
        set => SetValue(OnContentProperty, value);
    }

    public object OffContent
    {
        get => GetValue(OffContentProperty);
        set => SetValue(OffContentProperty, value);
    }

    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        DetachTemplateHandlers();

        _track = null;
        _thumb = null;
        _onBorder = null;
        _offBorder = null;
        _onLabel = null;
        _offLabel = null;
        _thumbTranslate = null;
        _onBorderTranslate = null;
        _offBorderTranslate = null;
        _track = GetTemplateChild("PART_Track") as Border;
        _thumb = GetTemplateChild("PART_Thumb") as Border;
        if (_thumb != null)
        {
            if (_thumb.RenderTransform is TranslateTransform translate)
            {
                _thumbTranslate = translate;
            }
            else
            {
                translate = new TranslateTransform();
                _thumb.RenderTransform = translate;
                _thumbTranslate = translate;
            }

            _thumb.SizeChanged += OnTemplatePartSizeChanged;
        }
        else
        {
            _thumbTranslate = null;
        }

        if (_track != null)
        {
            _track.SizeChanged += OnTemplatePartSizeChanged;
        }

        _onBorder = GetTemplateChild("PART_OnBorder") as Border;
        _offBorder = GetTemplateChild("PART_OffBorder") as Border;
        _onLabel = GetTemplateChild("PART_OnLabel") as ContentControl;
        _offLabel = GetTemplateChild("PART_OffLabel") as ContentControl;

        _onBorderTranslate = EnsureTranslateTransform(_onBorder);
        _offBorderTranslate = EnsureTranslateTransform(_offBorder);

        if (_onLabel != null)
        {
            _onLabel.SizeChanged += OnLabelSizeChanged;
        }

        if (_offLabel != null)
        {
            _offLabel.SizeChanged += OnLabelSizeChanged;
        }

        Loaded -= OnLoaded;
        Loaded += OnLoaded;
        UpdateThumbPosition(animate: false);
    }
    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        UpdateThumbPosition(animate: true);
    }
    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        UpdateThumbPosition(animate: true);
    }
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        UpdateThumbPosition(animate: false);
    }
    private void OnTemplatePartSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbPosition(animate: false);
    }
    private void DetachTemplateHandlers()
    {
        if (_thumb != null)
        {
            _thumb.SizeChanged -= OnTemplatePartSizeChanged;
        }

        if (_track != null)
        {
            _track.SizeChanged -= OnTemplatePartSizeChanged;
        }

        if (_onLabel != null)
        {
            _onLabel.SizeChanged -= OnLabelSizeChanged;
        }

        if (_offLabel != null)
        {
            _offLabel.SizeChanged -= OnLabelSizeChanged;
        }
    }

    private void UpdateThumbPosition(bool animate)
    {
        UpdateLabelTransforms(animate);

        if (_thumbTranslate is null)
        {
            return;
        }

        var target = IsChecked == true ? CalculateCheckedOffset() : 0d;

        if (!animate)
        {
            _thumbTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            _thumbTranslate.X = target;
            return;
        }

        var animation = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromSeconds(0.2),
            AccelerationRatio = 0.2,
            DecelerationRatio = 0.8
        };

        _thumbTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void UpdateLabelTransforms(bool animate)
    {
        var onFontSize = _onLabel?.FontSize ?? FontSize;
        var offFontSize = _offLabel?.FontSize ?? FontSize;

        var onTarget = IsChecked == true ? 0d : -onFontSize;
        var offTarget = IsChecked == true ? offFontSize : 0d;

        AnimateTranslate(_onBorderTranslate, onTarget, 0.15, animate);
        AnimateTranslate(_offBorderTranslate, offTarget, 0.15, animate);
    }

    private double CalculateCheckedOffset()
    {
        if (_track is null || _thumb is null)
        {
            return 0d;
        }

        var trackWidth = _track.ActualWidth;
        var thumbWidth = _thumb.ActualWidth;
        var margin = _thumb.Margin;

        var offset = trackWidth - thumbWidth - margin.Left - margin.Right;
        return offset > 0 ? offset : 0d;
    }

    private void OnLabelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbPosition(animate: false);
    }

    private static TranslateTransform? EnsureTranslateTransform(Border? border)
    {
        if (border is null)
        {
            return null;
        }

        if (border.RenderTransform is TranslateTransform translate)
        {
            if (translate.IsFrozen)
            {
                translate = translate.CloneCurrentValue();
                border.RenderTransform = translate;
            }

            return translate;
        }

        translate = new TranslateTransform();
        border.RenderTransform = translate;
        return translate;
    }

    private static void AnimateTranslate(TranslateTransform? transform, double target, double durationSeconds, bool animate)
    {
        if (transform is null)
        {
            return;
        }

        transform.BeginAnimation(TranslateTransform.XProperty, null);

        if (!animate)
        {
            transform.X = target;
            return;
        }

        if (durationSeconds <= 0)
        {
            transform.X = target;
            return;
        }

        var animation = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            transform.X = target;
        };

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
