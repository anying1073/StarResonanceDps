using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;

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
        new PropertyMetadata(default(ObservableCollection<SkillItemViewModel>)));

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
}