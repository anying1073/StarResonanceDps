namespace StarResonanceDpsAnalysis.Core.Data.Models;

public class SkillData(long skillId)
{
    private int _critTimes;
    private int _luckyTimes;
    private long _skillId = skillId;
    private long _totalValue;
    private int _useTimes;

    /// <summary>
    /// 技能ID
    /// </summary>
    public long SkillId
    {
        get => Interlocked.Read(ref _skillId);
        internal set => Interlocked.Exchange(ref _skillId, value);
    }

    /// <summary>
    /// 总数值 (DPS/HPS)
    /// </summary>
    public long TotalValue
    {
        get => Interlocked.Read(ref _totalValue);
        internal set => Interlocked.Exchange(ref _totalValue, value);
    }

    /// <summary>
    /// 技能使用次数
    /// </summary>
    public int UseTimes
    {
        get => Interlocked.CompareExchange(ref _useTimes, 0, 0);
        internal set => Interlocked.Exchange(ref _useTimes, value);
    }

    /// <summary>
    /// 暴击次数
    /// </summary>
    public int CritTimes
    {
        get => Interlocked.CompareExchange(ref _critTimes, 0, 0);
        internal set => Interlocked.Exchange(ref _critTimes, value);
    }

    /// <summary>
    /// 幸运一击次数
    /// </summary>
    public int LuckyTimes
    {
        get => Interlocked.CompareExchange(ref _luckyTimes, 0, 0);
        internal set => Interlocked.Exchange(ref _luckyTimes, value);
    }

    // Thread-safe increment methods
    internal void IncrementUseTimes()
    {
        Interlocked.Increment(ref _useTimes);
    }

    internal void IncrementCritTimes()
    {
        Interlocked.Increment(ref _critTimes);
    }

    internal void IncrementLuckyTimes()
    {
        Interlocked.Increment(ref _luckyTimes);
    }

    internal long IncrementTotalValue(long value)
    {
        return Interlocked.Add(ref _totalValue, value);
    }
}