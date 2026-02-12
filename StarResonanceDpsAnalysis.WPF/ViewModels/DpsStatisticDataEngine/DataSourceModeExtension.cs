using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

internal static class DataSourceModeExtension
{
    public static DataSourceMode ToDataSourceMode(this DpsUpdateMode mode)
    {
        return mode switch
        {
            DpsUpdateMode.Passive => DataSourceMode.Passive,
            DpsUpdateMode.Active => DataSourceMode.Active,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
}