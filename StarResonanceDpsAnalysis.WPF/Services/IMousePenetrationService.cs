using System.Windows;

namespace StarResonanceDpsAnalysis.WPF.Services;

public interface IMousePenetrationService
{
    void SetMousePenetrate(Window window, bool enable);
}