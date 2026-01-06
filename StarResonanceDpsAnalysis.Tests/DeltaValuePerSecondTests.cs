using StarResonanceDpsAnalysis.Core.Statistics;
using Xunit;

namespace StarResonanceDpsAnalysis.Tests;

/// <summary>
/// Tests for DeltaValuePerSecond functionality
/// </summary>
public class DeltaValuePerSecondTests
{
    [Fact]
    public void UpdateDeltaValues_FirstCall_InitializesPreviousValues()
    {
        // Arrange
        var stats = new PlayerStatistics(12345);
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond; // 1 second
        stats.AttackDamage.Total = 1000;

        // Act
        stats.UpdateDeltaValues();

        // Assert - First call should initialize, delta should be 0
        Assert.Equal(0, stats.AttackDamage.DeltaValuePerSecond);
    }

    [Fact]
    public void UpdateDeltaValues_CalculatesDeltaDps_Correctly()
    {
        // Arrange
        var stats = new PlayerStatistics(12345);
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond; // 1 second
        stats.AttackDamage.Total = 1000;

        // First update - initialize
        stats.UpdateDeltaValues();

        // Simulate 1 second passing with 500 more damage
        stats.LastTick = TimeSpan.TicksPerSecond * 2; // 2 seconds
        stats.AttackDamage.Total = 1500;

        // Act
        stats.UpdateDeltaValues();

        // Assert - Delta should be 500 damage / 1 second = 500 DPS
        Assert.Equal(500, stats.AttackDamage.DeltaValuePerSecond);
    }

    [Fact]
    public void UpdateDeltaValues_BurstDamage_ShowsHighDelta()
    {
        // Arrange
        var stats = new PlayerStatistics(12345);
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond;
        stats.AttackDamage.Total = 1000;

        stats.UpdateDeltaValues();

        // Simulate burst damage: 5000 damage in 1 second
        stats.LastTick = TimeSpan.TicksPerSecond * 2;
        stats.AttackDamage.Total = 6000;

        // Act
        stats.UpdateDeltaValues();

        // Assert - Delta should show burst: 5000 DPS
        Assert.Equal(5000, stats.AttackDamage.DeltaValuePerSecond);
        
        // But cumulative average is lower: 6000 total / 2 seconds = 3000 DPS
        var cumulativeAvg = stats.AttackDamage.Total / (stats.LastTick / (double)TimeSpan.TicksPerSecond);
        Assert.Equal(3000, cumulativeAvg);
    }

    [Fact]
    public void UpdateDeltaValues_AllThreeStatTypes_UpdatedCorrectly()
    {
        // Arrange
        var stats = new PlayerStatistics(12345);
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond;
        stats.AttackDamage.Total = 1000;
        stats.Healing.Total = 500;
        stats.TakenDamage.Total = 200;

        stats.UpdateDeltaValues();

        // Simulate changes
        stats.LastTick = TimeSpan.TicksPerSecond * 2;
        stats.AttackDamage.Total = 2500;  // +1500 damage
        stats.Healing.Total = 1200;       // +700 healing
        stats.TakenDamage.Total = 400;    // +200 taken

        // Act
        stats.UpdateDeltaValues();

        // Assert
        Assert.Equal(1500, stats.AttackDamage.DeltaValuePerSecond);
        Assert.Equal(700, stats.Healing.DeltaValuePerSecond);
        Assert.Equal(200, stats.TakenDamage.DeltaValuePerSecond);
    }

    [Fact]
    public void UpdateDeltaValues_NoTimeElapsed_DoesNotUpdate()
    {
        // Arrange
        var stats = new PlayerStatistics(12345);
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond;
        stats.AttackDamage.Total = 1000;

        stats.UpdateDeltaValues();
        
        // Don't advance time
        stats.AttackDamage.Total = 1500;

        // Act
        stats.UpdateDeltaValues();

        // Assert - Should still be 0 since no time passed
        Assert.Equal(0, stats.AttackDamage.DeltaValuePerSecond);
    }

    [Fact]
    public void ResetDeltaTracking_ClearsDeltaValues()
    {
        // Arrange
        var stats = new PlayerStatistics(12345);
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond;
        stats.AttackDamage.Total = 1000;
        stats.UpdateDeltaValues();
        
        stats.LastTick = TimeSpan.TicksPerSecond * 2;
        stats.AttackDamage.Total = 2000;
        stats.UpdateDeltaValues();

        // Act
        stats.ResetDeltaTracking();

        // Assert
        Assert.Equal(0, stats.AttackDamage.DeltaValuePerSecond);
        Assert.Equal(0, stats.Healing.DeltaValuePerSecond);
        Assert.Equal(0, stats.TakenDamage.DeltaValuePerSecond);
    }

    [Fact]
    public void PeriodicSampleRecorder_RecordsDeltaValues()
    {
        // Arrange
        var stats = new PlayerStatistics(12345, timeSeriesCapacity: null); // Unlimited for test
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond;
        stats.AttackDamage.Total = 1000;
        stats.AttackDamage.ValuePerSecond = 1000; // Cumulative average

        var recorder = new PeriodicSampleRecorder();
        var statsDict = new Dictionary<long, PlayerStatistics> { { 12345, stats } };

        // First sample
        recorder.RecordSamples(statsDict, TimeSpan.FromSeconds(1));

        // Simulate burst
        stats.LastTick = TimeSpan.TicksPerSecond * 2;
        stats.AttackDamage.Total = 3000; // +2000 in 1 second
        stats.AttackDamage.ValuePerSecond = 1500; // Cumulative average

        // Act
        recorder.RecordSamples(statsDict, TimeSpan.FromSeconds(2));

        // Assert - using delta samples
        var samples = stats.GetDeltaDpsSamples();
        Assert.Equal(2, samples.Count);
        
        // Second sample should be delta (2000 DPS burst)
        Assert.Equal(2000, samples[1].Value);
    }

    [Fact]
    public void HybridSampleRecorder_RecordsDeltaValues()
    {
        // Arrange
        var stats = new PlayerStatistics(12345, timeSeriesCapacity: null);
        stats.StartTick = 0;
        stats.LastTick = TimeSpan.TicksPerSecond;
        stats.AttackDamage.Total = 1000;
        stats.AttackDamage.ValuePerSecond = 1000;

        var recorder = new HybridSampleRecorder();
        var statsDict = new Dictionary<long, PlayerStatistics> { { 12345, stats } };

        recorder.RecordSamples(statsDict, TimeSpan.FromSeconds(1));

        stats.LastTick = TimeSpan.TicksPerSecond * 2;
        stats.AttackDamage.Total = 3000;
        stats.AttackDamage.ValuePerSecond = 1500; // Cumulative average

        // Act
        recorder.RecordSamples(statsDict, TimeSpan.FromSeconds(2));

        // Assert - using delta samples
        var samples = stats.GetDeltaDpsSamples();
        Assert.Equal(2, samples.Count);
        
        // HybridSampleRecorder also records delta values
        Assert.Equal(2000, samples[1].Value);
    }
}
