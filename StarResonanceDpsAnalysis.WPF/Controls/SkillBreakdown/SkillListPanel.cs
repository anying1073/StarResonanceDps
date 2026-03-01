using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;

public enum SkillSortMode
{
    Total,
    AverageDamage
}

public class SkillListPanel : Control
{
    public static readonly DependencyProperty IconColorProperty = DependencyProperty.Register(nameof(IconColor),
        typeof(string), typeof(SkillListPanel), new PropertyMetadata(default(string?)));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(SkillListPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SectionTitleProperty = DependencyProperty.Register(
        nameof(SectionTitle), typeof(string), typeof(SkillListPanel), new PropertyMetadata(default(string?)));

    public static readonly DependencyProperty SkillItemsProperty = DependencyProperty.Register(nameof(SkillItems),
        typeof(ObservableCollection<SkillItemViewModel>), typeof(SkillListPanel),
        new PropertyMetadata(default(ObservableCollection<SkillItemViewModel>), OnSkillItemsChanged));

    public static readonly DependencyProperty SortModeProperty = DependencyProperty.Register(
        nameof(SortMode),
        typeof(SkillSortMode),
        typeof(SkillListPanel),
        new PropertyMetadata(SkillSortMode.Total, OnSortModeChanged));

    static SkillListPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SkillListPanel),
            new FrameworkPropertyMetadata(typeof(SkillListPanel)));
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public string? SectionTitle
    {
        get => (string?)GetValue(SectionTitleProperty);
        set => SetValue(SectionTitleProperty, value);
    }

    public ObservableCollection<SkillItemViewModel> SkillItems
    {
        get => (ObservableCollection<SkillItemViewModel>)GetValue(SkillItemsProperty);
        set => SetValue(SkillItemsProperty, value);
    }

    public string? IconColor
    {
        get => (string?)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public SkillSortMode SortMode
    {
        get => (SkillSortMode)GetValue(SortModeProperty);
        set => SetValue(SortModeProperty, value);
    }

    private static void OnSkillItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SkillListPanel panel)
        {
            panel.ApplySorting();
        }
    }

    private static void OnSortModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SkillListPanel panel)
        {
            panel.ApplySorting();
        }
    }

    private void ApplySorting()
    {
        if (SkillItems == null) return;

        var view = CollectionViewSource.GetDefaultView(SkillItems);
        if (view == null) return;

        view.SortDescriptions.Clear();

        switch (SortMode)
        {
            case SkillSortMode.Total:
                view.SortDescriptions.Add(new SortDescription(nameof(SkillItemViewModel.TotalValue), ListSortDirection.Descending));
                break;
            case SkillSortMode.AverageDamage:
                view.SortDescriptions.Add(new SortDescription(nameof(SkillItemViewModel.Average), ListSortDirection.Descending));
                break;
        }

        view.Refresh();
    }

    public static readonly DependencyProperty AverageSortLabelKeyProperty =
    DependencyProperty.Register(
        nameof(AverageSortLabelKey),
        typeof(string),
        typeof(SkillListPanel),
        new PropertyMetadata("SkillBreakdown_Label_AverageDps"));

    public string AverageSortLabelKey
    {
        get => (string)GetValue(AverageSortLabelKeyProperty);
        set => SetValue(AverageSortLabelKeyProperty, value);
    }

    public static readonly DependencyProperty AverageColumnLabelKeyProperty =
        DependencyProperty.Register(
            nameof(AverageColumnLabelKey),
            typeof(string),
            typeof(SkillListPanel),
            new PropertyMetadata("SkillBreakdown_Label_AverageDamage"));

    public string AverageColumnLabelKey
    {
        get => (string)GetValue(AverageColumnLabelKeyProperty);
        set => SetValue(AverageColumnLabelKeyProperty, value);
    }

    public static readonly DependencyProperty HitCountLabelKeyProperty =
    DependencyProperty.Register(
        nameof(HitCountLabelKey),
        typeof(string),
        typeof(SkillListPanel),
        new PropertyMetadata("SkillBreakdown_Label_HitCount"));

    public string HitCountLabelKey
    {
        get => (string)GetValue(HitCountLabelKeyProperty);
        set => SetValue(HitCountLabelKeyProperty, value);
    }
}