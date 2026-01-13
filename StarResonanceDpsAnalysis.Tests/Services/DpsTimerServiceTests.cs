using System;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.WPF.Services;
using Xunit;

namespace StarResonanceDpsAnalysis.Tests.Services;

/// <summary>
/// Unit tests for DpsTimerService demonstrating SOLID principles
/// - Single Responsibility: Tests only timing logic
/// - No dependencies on UI or other services
/// - Independently testable
/// </summary>
public class DpsTimerServiceTests
{
    [Fact]
    public void Start_WhenNotRunning_StartsTimer()
    {
        // Arrange
        var service = new DpsTimerService();
        
        // Act
        service.Start();
        
        // Assert
        Assert.True(service.IsRunning);
    }
    
    [Fact]
    public void Stop_WhenRunning_StopsTimer()
    {
        // Arrange
        var service = new DpsTimerService();
        service.Start();
        
        // Act
        service.Stop();
        
        // Assert
        Assert.False(service.IsRunning);
    }
    
    [Fact]
    public void Reset_ClearsAllTimers()
    {
        // Arrange
        var service = new DpsTimerService();
        service.Start();
        Thread.Sleep(100);
        service.StartNewSection();
        
        // Act
        service.Reset();
        
        // Assert
        Assert.False(service.IsRunning);
        Assert.Equal(TimeSpan.Zero, service.BattleDuration);
        Assert.Equal(TimeSpan.Zero, service.TotalCombatDuration);
    }
    
    [Fact]
    public void StartNewSection_AccumulatesPreviousDuration()
    {
        // Arrange
        var service = new DpsTimerService();
        service.Start();
        Thread.Sleep(100); // Simulate first section
        var firstDuration = service.GetSectionElapsed();
        
        // Act
        service.StartNewSection();
        Thread.Sleep(50); // Simulate second section
        
        // Assert
        Assert.True(service.TotalCombatDuration >= firstDuration);
        Assert.True(service.TotalCombatDuration.TotalMilliseconds >= 100);
    }
    
    [Fact]
    public void GetSectionElapsed_ReturnsFrozenDuration()
    {
        // Arrange
        var service = new DpsTimerService();
        service.Start();
        Thread.Sleep(100);
        
        // Act
        var sectionDuration = service.GetSectionElapsed();
        Thread.Sleep(50); // Timer continues
        var currentDuration = service.GetSectionDuration();
        
        // Assert
        Assert.True(sectionDuration.TotalMilliseconds >= 100);
        Assert.True(currentDuration.TotalMilliseconds >= 150); // Should have continued
    }
    
    [Fact]
    public void GetSectionDuration_ReturnsCurrentDuration()
    {
        // Arrange
        var service = new DpsTimerService();
        service.Start();
        Thread.Sleep(100);
        
        // Act
        var duration = service.GetSectionDuration();
        
        // Assert
        Assert.True(duration.TotalMilliseconds >= 100);
    }
    
    [Fact]
    public void MultipleStartCalls_DoNotResetTimer()
    {
        // Arrange
        var service = new DpsTimerService();
        service.Start();
        Thread.Sleep(100);
        var firstDuration = service.GetSectionDuration();
        
        // Act
        service.Start(); // Should not reset
        Thread.Sleep(50);
        var secondDuration = service.GetSectionDuration();
        
        // Assert
        Assert.True(secondDuration >= firstDuration);
    }
}
