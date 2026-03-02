using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using StarResonanceDpsAnalysis.WPF.Controls.SkillBreakdown;

namespace StarResonanceDpsAnalysis.WPF.Views;

/// <summary>
/// SkillBreakdownView.xaml 的交互逻辑
/// </summary>
public partial class SkillBreakdownView : Window
{
    private readonly SkillBreakdownViewModel _viewModel;

    public SkillBreakdownView(SkillBreakdownViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        DataContext = vm;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncSelectorWithTab();
    }

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Sync the custom selector buttons with the TabControl
        SyncSelectorWithTab();
    }

    private void TabSelector_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || !int.TryParse(tb.Tag?.ToString(), out var index))
            return;

        MainTabControl.SelectedIndex = index;

        if (MainTabControl.SelectedContent is not TabContentPanel panel)
            return;

        switch (index)
        {
            case 0: // Damage
                panel.HitsLabelKey = "SkillBreakdown_Label_TotalHits";
                panel.HitCountLabelKey = "SkillBreakdown_Label_HitCount";
                panel.AverageSortLabelKey = "SkillBreakdown_Label_AverageDps";
                panel.AverageColumnLabelKey = "SkillBreakdown_Label_AverageDamage";
                panel.NormalHitTypeLabelKey = "Common_HitType_Normal";
                panel.LuckyHitTypeLabelKey = "Common_HitType_Lucky";
                panel.CriticalHitTypeLabelKey = "Common_HitType_Critical";
                break;

            case 1: // Healing
                panel.HitsLabelKey = "SkillBreakdown_Label_TotalHeals";
                panel.HitCountLabelKey = "SkillBreakdown_Label_HealCount";
                panel.AverageSortLabelKey = "SkillBreakdown_Label_AverageHps";
                panel.AverageColumnLabelKey = "SkillBreakdown_Label_AverageHealing";
                panel.NormalHitTypeLabelKey = "Common_HitType_NormalHeal";
                panel.LuckyHitTypeLabelKey = "Common_HitType_LuckyHeal";
                panel.CriticalHitTypeLabelKey = "Common_HitType_CriticalHeal";
                break;

            case 2: // Taken
                panel.HitsLabelKey = "SkillBreakdown_Label_TotalHitsTaken";
                panel.HitCountLabelKey = "SkillBreakdown_Label_DamageTakenCount";
                panel.AverageSortLabelKey = "SkillBreakdown_Label_AverageDtps";
                panel.AverageColumnLabelKey = "SkillBreakdown_Label_AverageTaken";
                panel.NormalHitTypeLabelKey = "Common_HitType_Normal";
                panel.LuckyHitTypeLabelKey = "Common_HitType_Lucky";
                panel.CriticalHitTypeLabelKey = "Common_HitType_Critical";
                break;

            default:
                panel.HitsLabelKey = "SkillBreakdown_Label_TotalHits";
                panel.HitCountLabelKey = "SkillBreakdown_Label_HitCount";
                panel.AverageSortLabelKey = "SkillBreakdown_Label_AverageDps";
                panel.AverageColumnLabelKey = "SkillBreakdown_Label_AverageDamage";
                panel.NormalHitTypeLabelKey = "Common_HitType_Normal";
                panel.LuckyHitTypeLabelKey = "Common_HitType_Lucky";
                panel.CriticalHitTypeLabelKey = "Common_HitType_Critical";
                break;
        }
    }

    private void SyncSelectorWithTab()
    {
        if (TabControlIndexChanger == null || MainTabControl == null) return;

        var selectedIndex = MainTabControl.SelectedIndex;

        foreach (var child in LogicalTreeHelper.GetChildren(TabControlIndexChanger))
        {
            if (child is not ToggleButton t || !int.TryParse(t.Tag?.ToString(), out var tagIndex)) continue;
            t.IsChecked = tagIndex == selectedIndex;
        }
    }

    private void Footer_RefreshClick(object sender, RoutedEventArgs e)
    {
    }

    private void Footer_CancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }
}