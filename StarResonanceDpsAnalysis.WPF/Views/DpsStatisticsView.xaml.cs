using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views;

/// <summary>
///     DpsStatisticsForm.xaml 的交互逻辑
/// </summary>
public partial class DpsStatisticsView : Window
{
    public static readonly DependencyProperty CollapseProperty =
        DependencyProperty.Register(
            nameof(Collapse),
            typeof(bool),
            typeof(DpsStatisticsView),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private double _beforeTrainingHeight;

    public DpsStatisticsView(DpsStatisticsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }

    public bool Collapse
    {
        get => (bool)GetValue(CollapseProperty);
        set => SetValue(CollapseProperty, value);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void PullButton_Click(object sender, RoutedEventArgs e)
    {
        Collapse = !Collapse;

        if (Collapse)
        {
            // 防止用户手动缩小窗体到一定大小后, 折叠功能看似失效的问题
            if (ActualHeight < 60)
            {
                Collapse = false;
                _beforeTrainingHeight = 360;
            }
            else
            {
                _beforeTrainingHeight = ActualHeight;
            }
        }

        // BaseStyle.CardHeaderHeight(25) + BaseStyle.ShadowWindowBorder.Margin((Top)5 + (Bottom)5)
        var baseHeight = 25 + 5 + 5;

        var sb = new Storyboard { FillBehavior = FillBehavior.HoldEnd };
        var duration = new Duration(TimeSpan.FromMilliseconds(300));
        var easingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
        var animationHeight = new DoubleAnimation
        {
            From = ActualHeight,
            To = Collapse ? baseHeight : _beforeTrainingHeight,
            Duration = duration,
            EasingFunction = easingFunction
        };
        Storyboard.SetTarget(animationHeight, this);
        Storyboard.SetTargetProperty(animationHeight, new PropertyPath(HeightProperty));
        sb.Children.Add(animationHeight);

        var pullButtonTransformDA = new DoubleAnimation
        {
            To = Collapse ? 180 : 0,
            Duration = duration,
            EasingFunction = easingFunction
        };
        Storyboard.SetTarget(pullButtonTransformDA, PullButton);
        Storyboard.SetTargetProperty(pullButtonTransformDA,
            new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[2].(RotateTransform.Angle)"));
        sb.Children.Add(pullButtonTransformDA);

        sb.Begin();
    }

    /// <summary>
    /// 训练模式选择
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TrainingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var me = (MenuItem)sender;
        var owner = ItemsControl.ItemsControlFromItemContainer(me);

        if (me.IsChecked)
        {
            // 这次点击后变成 true：把其它都关掉
            foreach (var obj in owner.Items)
            {
                if (owner.ItemContainerGenerator.ContainerFromItem(obj) is MenuItem mi && !ReferenceEquals(mi, me))
                    mi.IsChecked = false;
            }
            // me 已经是 true，不用再设
        }

        // 这次点击后变成 false：允许“全不选”，什么也不做
        e.Handled = true;
    }

    /// <summary>
    /// 测伤模式
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AxisMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var me = (MenuItem)sender;
        var owner = ItemsControl.ItemsControlFromItemContainer(me);

        if (me.IsChecked)
        {
            // 这次点击后变成 true：把其它都关掉
            foreach (var obj in owner.Items)
            {
                if (owner.ItemContainerGenerator.ContainerFromItem(obj) is MenuItem mi && !ReferenceEquals(mi, me))
                    mi.IsChecked = false;
            }
            // me 已经是 true，不用再设
        }

        // 这次点击后变成 false：允许“全不选”，什么也不做
        e.Handled = true;
    }
}
