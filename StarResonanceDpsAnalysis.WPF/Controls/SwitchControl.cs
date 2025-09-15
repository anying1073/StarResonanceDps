using System.Windows;
using System.Windows.Controls.Primitives;

namespace StarResonanceDpsAnalysis.WPF.Controls;

public class SwitchControl : ToggleButton
{
    static SwitchControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SwitchControl),
            new FrameworkPropertyMetadata(typeof(SwitchControl)));
    }

    public static readonly DependencyProperty OnContentProperty = DependencyProperty.Register(
        nameof(OnContent), typeof(object), typeof(SwitchControl), new PropertyMetadata("On"));

    public object OnContent
    {
        get => GetValue(OnContentProperty);
        set => SetValue(OnContentProperty, value);
    }

    public static readonly DependencyProperty OffContentProperty = DependencyProperty.Register(
        nameof(OffContent), typeof(object), typeof(SwitchControl), new PropertyMetadata("Off"));

    public object OffContent
    {
        get => GetValue(OffContentProperty);
        set => SetValue(OffContentProperty, value);
    }
}