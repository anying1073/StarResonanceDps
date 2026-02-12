namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

public sealed class DataSourceEngineParam
{
    public DataSourceMode? Mode { get; set; }
    public int? ActiveUpdateInterval { get; set; }

    public string? BattleSnapshotFilePath { get; set; }
}