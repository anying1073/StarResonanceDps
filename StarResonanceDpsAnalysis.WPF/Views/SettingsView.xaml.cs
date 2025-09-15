using System.Windows;
using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Views;

/// <summary>
///     SettingForm.xaml 的交互逻辑
/// </summary>
public partial class SettingsView : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        _vm = vm;
        _vm.RequestClose += Vm_RequestClose;
    }

    private void Vm_RequestClose()
    {
        Close();
    }
}