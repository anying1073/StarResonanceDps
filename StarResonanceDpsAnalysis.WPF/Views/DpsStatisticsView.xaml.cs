using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using StarResonanceDpsAnalysis.WPF.Controls;
using StarResonanceDpsAnalysis.WPF.Converters;
using StarResonanceDpsAnalysis.WPF.Helpers;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views;

/// <summary>
///     DpsStatisticsForm.xaml 的交互逻辑
/// </summary>
public partial class DpsStatisticsView : Window
{
    private double _beforeTrainingHeight;

    public static readonly DependencyProperty CollapseProperty =
        DependencyProperty.Register(
            nameof(Collapse),
            typeof(bool),
            typeof(DpsStatisticsView),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /* If a steganography overlay is required, please set this metadata to below 0.1. */
    public static readonly DependencyProperty OverlayOpacityProperty =
        DependencyProperty.Register(
            nameof(OverlayOpacity),
            typeof(double),
            typeof(DpsStatisticsView),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty MaskSourceProperty =
        DependencyProperty.Register(
            nameof(MaskSource),
            typeof(ImageSource),
            typeof(DpsStatisticsView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MaskViewportProperty =
        DependencyProperty.Register(
            nameof(MaskViewport),
            typeof(Rect),
            typeof(DpsStatisticsView),
            new PropertyMetadata(new Rect(0, 0, 1d / 3d, 1d / 3d)));

    public bool Collapse
    {
        get => (bool)GetValue(CollapseProperty);
        set => SetValue(CollapseProperty, value);
    }

    public double OverlayOpacity
    {
        get => (double)GetValue(OverlayOpacityProperty);
        set => SetValue(OverlayOpacityProperty, value);
    }

    public ImageSource? MaskSource
    {
        get => (ImageSource?)GetValue(MaskSourceProperty);
        set => SetValue(MaskSourceProperty, value);
    }

    public Rect MaskViewport
    {
        get => (Rect)GetValue(MaskViewportProperty);
        set => SetValue(MaskViewportProperty, value);
    }

    public DpsStatisticsView(DpsStatisticsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        DropdownContextMenu.DataContext = vm;
        if (vm.AppConfig.DebugEnabled)
        {
            var menuItem = new MenuItem()
            {
                Command = vm.DebugFunctions.CallDebugWindowCommand,
                Header = "CallDebugPanel",
            };
            var items = DropdownContextMenu.Items;
            items.Insert(items.Count - 2, menuItem);
        }

        // 初始化默认值: 记录所有(0秒)
        vm.Options.MinimalDurationInSeconds = 0;

        MaskSource = MaskHelper.SteganographyImage;

        // 右键退出历史模式
        MouseRightButtonDown += OnWindowRightClick;
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
    /// 记录设置选择(互斥单选)
    /// </summary>
    private void RecordSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var me = (MenuItem)sender;
        var owner = ItemsControl.ItemsControlFromItemContainer(me);

        if (me.IsChecked && owner != null)
        {
            // 这次点击后变成 true：把其它都关掉
            foreach (var obj in owner.Items)
            {
                if (owner.ItemContainerGenerator.ContainerFromItem(obj) is MenuItem mi && !ReferenceEquals(mi, me))
                    mi.IsChecked = false;
            }
        }

        // ⭐ XAML中已设置StaysOpenOnClick="True",这里只需要阻止事件冒泡
        e.Handled = true;
    }

    /// <summary>
    /// 窗口右键处理 - 退出历史模式
    /// </summary>
    private void OnWindowRightClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DpsStatisticsViewModel vm && vm.IsViewingHistory)
        {

            // 如果正在查看历史,右键退出历史模式
            if (vm.ExitHistoryViewModeCommand.CanExecute(null))
            {
                vm.ExitHistoryViewModeCommand.Execute(null);
                e.Handled = true; // 阻止默认右键菜单
            }
        }
    }

    private void ButtonMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void HeaderDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
