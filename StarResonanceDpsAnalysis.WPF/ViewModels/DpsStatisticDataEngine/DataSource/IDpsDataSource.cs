using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public interface IDpsDataSource
{
    DataSourceMode Mode { get; }
    ScopeTime Scope { get; set; }
    Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> GetData();

    IReadOnlyDictionary<long, PlayerInfo> GetPlayerInfoDictionary();

    IReadOnlyDictionary<long, PlayerStatistics> GetRawData();

    void Refresh();

    void Reset();

    void SetEnable(bool enable);
}