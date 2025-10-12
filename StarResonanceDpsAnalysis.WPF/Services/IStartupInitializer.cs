namespace StarResonanceDpsAnalysis.WPF.Services;

public interface IApplicationStartup
{
    Task InitializeAsync();
    void Shutdown();
}
