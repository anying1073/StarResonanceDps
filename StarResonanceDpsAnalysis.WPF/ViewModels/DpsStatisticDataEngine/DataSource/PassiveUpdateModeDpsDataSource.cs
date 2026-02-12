using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public sealed class PassiveUpdateModeDpsDataSource(
    DataSourceEngine dataSourceEngine,
    IDataStorage dataStorage,
    IDpsDataProcessor processor)
    : RealTimeDataSource(dataSourceEngine, dataStorage,
        DataSourceMode.Passive, processor)
{
    public override void SetEnable(bool enable)
    {
        lock (SyncRoot)
        {
            if (Enable == enable) return;
            Enable = enable;
            if (enable)
            {
                DataStorage.DpsDataUpdated += OnDataUpdated;
                DataStorage.NewSectionCreated += OnNewSection;
            }
            else
            {
                DataStorage.DpsDataUpdated -= OnDataUpdated;
                DataStorage.NewSectionCreated -= OnNewSection;
            }
        }
    }

    private void OnNewSection()
    {
        lock (SyncRoot)
        {
            if (!Enable) return;
            Reset();
        }
    }

    private void OnDataUpdated()
    {
        lock (SyncRoot)
        {
            if (!Enable) return;
            Refresh();
        }
    }
}