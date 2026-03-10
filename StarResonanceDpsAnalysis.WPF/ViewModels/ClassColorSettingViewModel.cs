using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class ClassColorSettingViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly Action<Classes, Color> _onChanged;
    
    // Store original color for reset? Or pass it in.
    private readonly Color _defaultColor;

    public Classes Class { get; }
    public string Name { get; }

    [ObservableProperty]
    private Color _color;

    [ObservableProperty]
    private bool _isLast;

    public ClassColorSettingViewModel(Classes cls, string name, Color color, Color defaultColor, AppConfig config, Action<Classes, Color> onChanged)
    {
        Class = cls;
        Name = name;
        _color = color;
        _defaultColor = defaultColor;
        _config = config;
        _onChanged = onChanged;
    }

    partial void OnColorChanged(Color value)
    {
        // Store as Hex string
        _config.CustomClassColors[Class] = value.ToString();
        _onChanged(Class, value);
    }

    [RelayCommand]
    private void Reset()
    {
        this.Color = _defaultColor;
    }
}
