using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Benchmarks;

[MemoryDiagnoser]
public class DataStorageV2Benchmarks
{
    private DataStorageV2 _dataStorage = null!;
    private readonly BattleLog _sampleLog = new()
    {
        TimeTicks = 1,
        AttackerUuid = 123,
        TargetUuid = 456,
        Value = 100,
        IsAttackerPlayer = true,
        IsTargetPlayer = false
    };
    private readonly BattleLog[] _logBatch = new BattleLog[1000];

    [GlobalSetup]
    public void GlobalSetup()
    {
        for (int i = 0; i < _logBatch.Length; i++)
        {
            _logBatch[i] = new BattleLog
            {
                TimeTicks = i,
                AttackerUuid = (long)i % 50, // Simulate 50 players
                TargetUuid = 999, // Single NPC target
                Value = 100 + i,
                IsAttackerPlayer = true,
                IsTargetPlayer = false,
                IsCritical = i % 5 == 0,
                IsLucky = i % 10 == 0
            };
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _dataStorage = new DataStorageV2(NullLogger<DataStorageV2>.Instance);
        // Pre-populate some players
        for (int i = 0; i < 50; i++)
        {
            _dataStorage.EnsurePlayer(i);
        }
    }

    [Benchmark(Description = "AddBattleLog (Immediate Event Firing)")]
    public void AddBattleLog_Single()
    {
        _dataStorage.AddBattleLog(_sampleLog);
    }

    [Benchmark(Description = "AddBattleLogInternal + Flush (Batched Event Firing)")]
    public void AddBattleLog_Batched()
    {
        for (int i = 0; i < _logBatch.Length; i++)
        {
            _dataStorage.AddBattleLogInternal(_logBatch[i]);
        }
        _dataStorage.FlushPendingEvents();
    }

    [Benchmark(Description = "EnsurePlayer (New Player)")]
    public void EnsurePlayer_New()
    {
        _dataStorage.EnsurePlayer(9999L);
    }

    [Benchmark(Description = "EnsurePlayer (Existing Player)")]
    public void EnsurePlayer_Existing()
    {
        _dataStorage.EnsurePlayer(1L);
    }

    [Benchmark(Description = "Get DPS Data (via ReadOnlyFullDpsDatas)")]
    public void GetDpsData()
    {
        // New approach: access via StatisticsAdapter through readonly properties
        var dpsDatas = _dataStorage.GetStatistics(true);
        _ = dpsDatas.Count;
    }
}
