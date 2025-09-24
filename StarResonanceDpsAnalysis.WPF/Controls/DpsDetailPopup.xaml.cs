using System.Windows;
using System.Windows.Controls;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Controls;

/// <summary>
///     Interaction logic for DpsDetailPopup.xaml
/// </summary>
public partial class DpsDetailPopup : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(SkillPopupControl),
            new PropertyMetadata("技能详情") // 默认值
        );

    public static readonly DependencyProperty SkillListProperty = DependencyProperty.Register(
        nameof(SkillList), typeof(IEnumerable<SkillItemViewModel>), typeof(DpsDetailPopup),
        new PropertyMetadata(default(IEnumerable<SkillItemViewModel>)));

    public DpsDetailPopup()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     弹窗标题，比如“技能统计”
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable<SkillItemViewModel> SkillList
    {
        get => (IEnumerable<SkillItemViewModel>)GetValue(SkillListProperty);
        set => SetValue(SkillListProperty, value);
    }
}