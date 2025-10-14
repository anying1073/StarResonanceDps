namespace StarResonanceDpsAnalysis.WPF.Services;

using StarResonanceDpsAnalysis.WPF.Config;

/// <summary>
/// Provides lifecycle management and configuration update methods for global hotkey handling.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>
    /// Starts the global hotkey service. Call this method to begin listening for hotkey events.
    /// Should be called once during application startup. Not thread-safe; call from the main UI thread.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the global hotkey service. Call this method to stop listening for hotkey events and release resources.
    /// Should be called during application shutdown. Not thread-safe; call from the main UI thread.
    /// </summary>
    void Stop();

    /// <summary>
    /// Updates the hotkey service configuration.
    /// Honors hotkey-related fields in <see cref="AppConfig"/> (e.g., key bindings, enable/disable flags).
    /// Call this method whenever the configuration changes. Not thread-safe; call from the main UI thread.
    /// </summary>
    void UpdateFromConfig(AppConfig config);
}
