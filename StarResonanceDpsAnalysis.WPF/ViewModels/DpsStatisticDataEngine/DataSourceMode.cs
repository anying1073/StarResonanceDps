namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

public enum DataSourceMode
{
    /// <summary>
    /// 被动模式：基于事件触发更新
    /// </summary>
    Passive = 0,

    /// <summary>
    /// 主动模式：基于定时器定期更新
    /// </summary>
    Active = 1,

    Paused = 1 << 3,
    Snapshot = Paused | 1,
}