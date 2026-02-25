using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine.DataSource;

public sealed partial class ActiveUpdateModeDpsDataSource : RealTimeDataSource
{
    private readonly ILogger _logger;
    private readonly DispatcherTimer _timer;

    public ActiveUpdateModeDpsDataSource(DataSourceEngine dataSourceEngine, IDataStorage dataStorage, ILogger logger,
        IDpsDataProcessor processor, IDpsTimerService timerService) : base(dataSourceEngine, dataStorage,
        DataSourceMode.Active, processor, timerService)
    {
        _logger = logger;
        _timer = new DispatcherTimer();
        SetUpdateInterval(500);
        _timer.Tick += TimerOnTick;

        dataStorage.SectionEnded += StopRefresh;
        dataStorage.NewSectionCreated += StartRefresh;
    }

    [Conditional("DEBUG")]
    private void TickLog()
    {
        var currentSecond = DateTime.Now.Second;
        if (currentSecond % 10 == 0)
        {
            LogTickTrace();
        }
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        TickLog();
        Refresh();
    }

    public override void SetEnable(bool enable)
    {
        base.SetEnable(enable);
        lock (SyncRoot)
        {
            if (!enable)
            {
                ClearCache();
            }
        }
    }

    public void SetUpdateInterval(int updateInterval)
    {
        updateInterval = Math.Clamp(updateInterval, 100, 5000);
        _timer.Interval = TimeSpan.FromMilliseconds(updateInterval);
    }

    private void StartRefresh()
    {
        Refresh();
        _timer.Start();
    }

    private void StopRefresh()
    {
        _timer.Stop();
    }

    [LoggerMessage(LogLevel.Trace, "Timer tick triggered")]
    private partial void LogTickTrace();
}