using BenchmarkDotNet.Attributes;
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
        IsAttackerPlayer = true
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
                IsCritical = i % 5 == 0,
                IsLucky = i % 10 == 0
            };
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _dataStorage = new DataStorageV2();
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

    [Benchmark(Description = "GetOrCreateDpsDataByUid")]
    public void GetOrCreateDpsData()
    {
        _dataStorage.GetOrCreateDpsDataByUid(2L);
    }
}
