namespace StarResonanceDpsAnalysis.WPF.Services;

using StarResonanceDpsAnalysis.WPF.Config;

public interface IGlobalHotkeyService
{
    void Start();
    void Stop();
    void UpdateFromConfig(AppConfig config);
}
