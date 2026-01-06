using StarResonanceDpsAnalysis.Core.Statistics;
using Xunit;

namespace StarResonanceDpsAnalysis.Tests;

public class DeltaTrackingStopTests
{
    [Fact]
    public void StopDeltaTracking_StopsUpdatingDeltaValues()
    {
        // Arrange
        var stats = new PlayerStatistics(1);
        stats.StartTick = 0;
        
        // Add some initial damage
        stats.AttackDamage.Total = 1000;
        stats.LastTick = TimeSpan.FromSeconds(1).Ticks;
        stats.UpdateDeltaValues(); // Initialize tracking
        
        // Add more damage
        stats.AttackDamage.Total = 2000;
        stats.LastTick = TimeSpan.FromSeconds(2).Ticks;
        stats.UpdateDeltaValues(); // Should calculate delta
        
        var deltaBeforeStop = stats.AttackDamage.DeltaValuePerSecond;
        Assert.True(deltaBeforeStop > 0, "Delta should be calculated before stop");
        
        // Act - Stop delta tracking
        stats.StopDeltaTracking();
        
        // Add even more damage
        stats.AttackDamage.Total = 3000;
        stats.LastTick = TimeSpan.FromSeconds(3).Ticks;
        stats.UpdateDeltaValues(); // Should NOT update delta
        
        // Assert
        var deltaAfterStop = stats.AttackDamage.DeltaValuePerSecond;
        Assert.Equal(deltaBeforeStop, deltaAfterStop);
    }
    
    [Fact]
    public void ResumeDeltaTracking_ResumesUpdatingDeltaValues()
    {
        // Arrange
        var stats = new PlayerStatistics(1);
        stats.StartTick = 0;
        stats.AttackDamage.Total = 1000;
        stats.LastTick = TimeSpan.FromSeconds(1).Ticks;
        stats.UpdateDeltaValues();
        
        stats.StopDeltaTracking();
        
        stats.AttackDamage.Total = 2000;
        stats.LastTick = TimeSpan.FromSeconds(2).Ticks;
        stats.UpdateDeltaValues(); // Should not update while stopped
        
        var deltaWhileStopped = stats.AttackDamage.DeltaValuePerSecond;
        
        // Act - Resume tracking
        stats.ResumeDeltaTracking();
        
        stats.AttackDamage.Total = 3000;
        stats.LastTick = TimeSpan.FromSeconds(3).Ticks;
        stats.UpdateDeltaValues(); // Should calculate new delta
        
        // Assert
        var deltaAfterResume = stats.AttackDamage.DeltaValuePerSecond;
        Assert.NotEqual(deltaWhileStopped, deltaAfterResume);
        Assert.True(deltaAfterResume > 0, "Delta should be recalculated after resume");
    }
    
    [Fact]
    public void ResetDeltaTracking_ReEnablesTracking()
    {
        // Arrange
        var stats = new PlayerStatistics(1);
        stats.StartTick = 0;
        stats.AttackDamage.Total = 1000;
        stats.LastTick = TimeSpan.FromSeconds(1).Ticks;
        stats.UpdateDeltaValues();
        
        stats.StopDeltaTracking();
        
        // Act - Reset should re-enable tracking
        stats.ResetDeltaTracking();
        
        stats.AttackDamage.Total = 2000;
        stats.LastTick = TimeSpan.FromSeconds(2).Ticks;
        stats.UpdateDeltaValues();
        
        // Assert
        // After reset, delta should be 0 initially, then recalculate
        // Since reset clears the snapshot, first update after reset will initialize
        Assert.Equal(0, stats.AttackDamage.DeltaValuePerSecond); // First update initializes
        
        // Add more damage and update again
        stats.AttackDamage.Total = 3000;
        stats.LastTick = TimeSpan.FromSeconds(3).Ticks;
        stats.UpdateDeltaValues();
        
        Assert.True(stats.AttackDamage.DeltaValuePerSecond > 0, "Delta should calculate after reset");
    }
    
    [Fact]
    public void StopDeltaTracking_PreservesCurrentDeltaValues()
    {
        // Arrange
        var stats = new PlayerStatistics(1);
        stats.StartTick = 0;
        stats.AttackDamage.Total = 1000;
        stats.Healing.Total = 500;
        stats.TakenDamage.Total = 200;
        stats.LastTick = TimeSpan.FromSeconds(1).Ticks;
        stats.UpdateDeltaValues(); // Initialize
        
        stats.AttackDamage.Total = 2000;
        stats.Healing.Total = 1000;
        stats.TakenDamage.Total = 400;
        stats.LastTick = TimeSpan.FromSeconds(2).Ticks;
        stats.UpdateDeltaValues(); // Calculate deltas
        
        var dmgDelta = stats.AttackDamage.DeltaValuePerSecond;
        var healDelta = stats.Healing.DeltaValuePerSecond;
        var takenDelta = stats.TakenDamage.DeltaValuePerSecond;
        
        // Act - Stop tracking
        stats.StopDeltaTracking();
        
        // Add more data and try to update (should not change deltas)
        stats.AttackDamage.Total = 5000;
        stats.Healing.Total = 3000;
        stats.TakenDamage.Total = 1000;
        stats.LastTick = TimeSpan.FromSeconds(5).Ticks;
        stats.UpdateDeltaValues();
        
        // Assert - Deltas should remain unchanged
        Assert.Equal(dmgDelta, stats.AttackDamage.DeltaValuePerSecond);
        Assert.Equal(healDelta, stats.Healing.DeltaValuePerSecond);
        Assert.Equal(takenDelta, stats.TakenDamage.DeltaValuePerSecond);
    }
}
