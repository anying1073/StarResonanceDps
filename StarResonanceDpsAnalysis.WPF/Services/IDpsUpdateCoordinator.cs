using System;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Service for managing DPS update modes (Active/Passive)
/// Follows Single Responsibility Principle - only handles update mode logic
/// </summary>
public interface IDpsUpdateCoordinator
{
    /// <summary>
    /// Current update mode
    /// </summary>
    Config.DpsUpdateMode UpdateMode { get; }
    
    /// <summary>
    /// Update interval in milliseconds (for Active mode)
    /// </summary>
    int UpdateInterval { get; }
    
    /// <summary>
    /// Whether updates are currently enabled
    /// </summary>
    bool IsUpdateEnabled { get; }
    
    /// <summary>
    /// Configure update mode
    /// </summary>
    void Configure(Config.DpsUpdateMode mode, int intervalMs);
    
    /// <summary>
    /// Start updates
    /// </summary>
    void Start();
    
    /// <summary>
    /// Stop updates
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Pause updates (for section changes)
    /// </summary>
    void Pause();
    
    /// <summary>
    /// Resume updates
    /// </summary>
    void Resume();
    
    /// <summary>
    /// Event fired when update is needed
    /// </summary>
    event EventHandler? UpdateRequested;
}
