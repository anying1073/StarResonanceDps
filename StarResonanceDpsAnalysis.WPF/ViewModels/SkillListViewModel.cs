using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// ViewModel for SkillListPanel component
/// </summary>
public partial class SkillListViewModel : ObservableObject
{
    [ObservableProperty] private string _iconColor = "#2297F4";
    [ObservableProperty] private ObservableCollection<SkillItemViewModel> _skillItems = new();
}
