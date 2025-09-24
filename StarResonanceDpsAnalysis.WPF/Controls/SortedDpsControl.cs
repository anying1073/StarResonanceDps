using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Controls;

public class SortedDpsControl : Control
{
    // New ItemsSource property (preferred)
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(SortedDpsControl),
        new PropertyMetadata(null));

    // ItemTemplate for customizing item visuals
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(SortedDpsControl), new PropertyMetadata(null));


    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(SortedDpsControl), new PropertyMetadata(default(double)));

    // Selected item
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(ProgressBarData), typeof(SortedDpsControl), new PropertyMetadata(null));

    // Item click command
    public static readonly DependencyProperty ItemClickCommandProperty = DependencyProperty.Register(
        nameof(ItemClickCommand), typeof(ICommand), typeof(SortedDpsControl), new PropertyMetadata(null));

    static SortedDpsControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SortedDpsControl),
            new FrameworkPropertyMetadata(typeof(SortedDpsControl)));
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public ProgressBarData? SelectedItem
    {
        get => (ProgressBarData?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ICommand? ItemClickCommand
    {
        get => (ICommand?)GetValue(ItemClickCommandProperty);
        set => SetValue(ItemClickCommandProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (SortedDpsControl)d;
        // Keep ItemsSource in sync with Data
        if (!Equals(ctl.ItemsSource, e.NewValue))
        {
            ctl.SetValue(ItemsSourceProperty, e.NewValue);
        }
    }
}