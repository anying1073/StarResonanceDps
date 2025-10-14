using System.Windows;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Provides an API to toggle mouse input penetration (click-through) behavior
/// for a WPF <see cref="Window"/>.
/// </summary>
/// <remarks>
/// When mouse penetration is enabled, the window will ignore mouse input and
/// allow clicks to pass through to windows underneath. This method should be
/// called on the UI thread that owns the provided window.
/// </remarks>
public interface IMousePenetrationService
{
    /// <summary>
    /// Enables or disables mouse penetration (click-through) for the specified <paramref name="window"/>.
    /// </summary>
    /// <param name="window">The target WPF window. Must not be <see langword="null"/>.</param>
    /// <param name="enable">
    /// <see langword="true"/> to allow mouse events to pass through the window to underlying windows;
    /// <see langword="false"/> to restore normal hit testing.
    /// </param>
    void SetMousePenetrate(Window window, bool enable);
}