using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class SkillLogViewModel : ObservableObject
{
    private readonly ISkillLogService _skillLogService;
    private readonly IConfigManager? _configManager;
    private readonly IWindowManagementService? _windowManagementService;

    public ObservableCollection<SkillLogItem> Logs => _skillLogService.Logs;

    // Design-time constructor
    public SkillLogViewModel()
    {
        var dataStorage = new DataStorageV2(Microsoft.Extensions.Logging.Abstractions.NullLogger<DataStorageV2>.Instance);
        var configManager = new DesignConfigManager();
        _skillLogService = new SkillLogService(dataStorage, configManager);
        
        // Add dummy data for design time
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            _skillLogService.AddLog(new SkillLogItem { Timestamp = System.DateTime.Now, SkillName = "Test Skill", TotalValue = 1234, Count = 1, CritCount = 1 });
            _skillLogService.AddLog(new SkillLogItem { Timestamp = System.DateTime.Now.AddSeconds(1), SkillName = "Another Skill", TotalValue = 2345, Count = 1, LuckyCount = 1 });
        }
    }

    public SkillLogViewModel(ISkillLogService skillLogService, IConfigManager configManager, IWindowManagementService windowManagementService)
    {
        _skillLogService = skillLogService;
        _configManager = configManager;
        _windowManagementService = windowManagementService;
    }

    [RelayCommand]
    private void Clear()
    {
        _skillLogService.Clear();
    }

    [RelayCommand]
    private void Close()
    {
        // 查找并关闭 SkillLogView 窗口
        var window = Application.Current?.Windows.OfType<SkillLogView>().FirstOrDefault();
        window?.Close();
    }
}
