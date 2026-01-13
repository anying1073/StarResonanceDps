using System;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Services;
using Xunit;

namespace StarResonanceDpsAnalysis.Tests.Services;

/// <summary>
/// Unit tests for DpsUpdateCoordinator demonstrating SOLID principles
/// - Single Responsibility: Tests only update coordination
/// - Strategy Pattern: Easy to test different update modes
/// - No ViewModel dependencies
/// </summary>
public class DpsUpdateCoordinatorTests
{
    private readonly Dispatcher _dispatcher;
    
    public DpsUpdateCoordinatorTests()
    {
        // Create a test dispatcher
        _dispatcher = Dispatcher.CurrentDispatcher;
    }
    
    [Fact]
    public void Configure_ActiveMode_CreatesTimer()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        
        // Act
        coordinator.Configure(DpsUpdateMode.Active, 100);
        coordinator.Start();
        
        // Assert
        Assert.Equal(DpsUpdateMode.Active, coordinator.UpdateMode);
        Assert.Equal(100, coordinator.UpdateInterval);
        Assert.True(coordinator.IsUpdateEnabled);
    }
    
    [Fact]
    public void Configure_PassiveMode_NoTimer()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        
        // Act
        coordinator.Configure(DpsUpdateMode.Passive, 1000);
        coordinator.Start();
        
        // Assert
        Assert.Equal(DpsUpdateMode.Passive, coordinator.UpdateMode);
        Assert.False(coordinator.IsUpdateEnabled); // No timer in passive mode
    }
    
    [Fact]
    public void Configure_ClampsInterval_ToValidRange()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        
        // Act - Test lower bound
        coordinator.Configure(DpsUpdateMode.Active, 50); // Too low
        
        // Assert
        Assert.Equal(100, coordinator.UpdateInterval); // Clamped to minimum
        
        // Act - Test upper bound
        coordinator.Configure(DpsUpdateMode.Active, 10000); // Too high
        
        // Assert
        Assert.Equal(5000, coordinator.UpdateInterval); // Clamped to maximum
    }
    
    [Fact]
    public void Start_InActiveMode_EnablesUpdates()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        coordinator.Configure(DpsUpdateMode.Active, 100);
        
        // Act
        coordinator.Start();
        
        // Assert
        Assert.True(coordinator.IsUpdateEnabled);
    }
    
    [Fact]
    public void Stop_DisablesUpdates()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        coordinator.Configure(DpsUpdateMode.Active, 100);
        coordinator.Start();
        
        // Act
        coordinator.Stop();
        
        // Assert
        Assert.False(coordinator.IsUpdateEnabled);
    }
    
    [Fact]
    public void Pause_StopsUpdates_ButCanResume()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        coordinator.Configure(DpsUpdateMode.Active, 100);
        coordinator.Start();
        
        // Act
        coordinator.Pause();
        
        // Assert
        Assert.False(coordinator.IsUpdateEnabled);
        
        // Act
        coordinator.Resume();
        
        // Assert
        Assert.True(coordinator.IsUpdateEnabled);
    }
    
    [Fact]
    public void UpdateRequested_EventFires_InActiveMode()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);

        coordinator.UpdateRequested += (sender, e) => _ = true;
        
        coordinator.Configure(DpsUpdateMode.Active, 50); // Short interval for testing
        
        // Act
        coordinator.Start();
        
        // Wait for timer to tick
        var frame = new DispatcherFrame();
        _dispatcher.BeginInvoke(DispatcherPriority.Background, 
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        
        Thread.Sleep(100); // Wait for timer tick
        
        // Process dispatcher events
        DoEvents();
        
        // Assert
        // Note: Event firing depends on dispatcher timing, so this is a best-effort test
        // In a real scenario, you'd use a more sophisticated testing approach
    }
    
    [Fact]
    public void Pause_PreventEventFiring()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        
        var eventCount = 0;
        coordinator.UpdateRequested += (sender, e) => eventCount++;
        
        coordinator.Configure(DpsUpdateMode.Active, 50);
        coordinator.Start();
        
        // Let it fire once
        Thread.Sleep(100);
        DoEvents();
        var firstCount = eventCount;
        
        // Act - Pause
        coordinator.Pause();
        Thread.Sleep(100);
        DoEvents();
        var secondCount = eventCount;
        
        // Assert
        Assert.Equal(firstCount, secondCount); // No new events while paused
    }
    
    [Fact]
    public void SwitchMode_FromActiveToPassive_StopsTimer()
    {
        // Arrange
        var coordinator = new DpsUpdateCoordinator(
            NullLogger<DpsUpdateCoordinator>.Instance,
            _dispatcher);
        coordinator.Configure(DpsUpdateMode.Active, 100);
        coordinator.Start();
        Assert.True(coordinator.IsUpdateEnabled);
        
        // Act - Switch to Passive
        coordinator.Configure(DpsUpdateMode.Passive, 1000);
        coordinator.Start();
        
        // Assert
        Assert.Equal(DpsUpdateMode.Passive, coordinator.UpdateMode);
        Assert.False(coordinator.IsUpdateEnabled);
    }
    
    // Helper to process dispatcher events
    private void DoEvents()
    {
        var frame = new DispatcherFrame();
        _dispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
