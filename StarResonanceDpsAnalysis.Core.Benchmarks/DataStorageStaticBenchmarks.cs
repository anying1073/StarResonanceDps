using BenchmarkDotNet.Attributes;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;

namespace StarResonanceDpsAnalysis.Core.Benchmarks;

[MemoryDiagnoser]
public class DataStorageStaticBenchmarks
{
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
        DataStorage.ClearAllDpsData();
        DataStorage.ClearAllPlayerInfos();
        // Pre-populate some players
        for (int i = 0; i < 50; i++)
        {
            DataStorage.TestCreatePlayerInfoByUID(i);
        }
    }

    [Benchmark(Description = "Static.AddBattleLog")]
    public void AddBattleLog_Single()
    {
        DataStorage.AddBattleLog(_sampleLog);
    }

    [Benchmark(Description = "Static.AddBattleLog (1000 logs)")]
    public void AddBattleLog_Batch()
    {
        for (int i = 0; i < _logBatch.Length; i++)
        {
            DataStorage.AddBattleLog(_logBatch[i]);
        }
    }

    [Benchmark(Description = "Static.TestCreatePlayerInfoByUID (New)")]
    public void TestCreatePlayerInfoByUID_New()
    {
        DataStorage.TestCreatePlayerInfoByUID(9999L);
    }

    [Benchmark(Description = "Static.TestCreatePlayerInfoByUID (Existing)")]
    public void TestCreatePlayerInfoByUID_Existing()
    {
        DataStorage.TestCreatePlayerInfoByUID(1L);
    }

    [Benchmark(Description = "Static.GetOrCreateDpsDataByUID")]
    public void GetOrCreateDpsData()
    {
        DataStorage.GetOrCreateDpsDataByUID(2L);
    }
}
