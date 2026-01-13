using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Config;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Implementation of DPS update coordinator
/// Manages Active (timer-based) and Passive (event-based) update modes
/// </summary>
public class DpsUpdateCoordinator(ILogger<DpsUpdateCoordinator> logger, Dispatcher dispatcher)
    : IDpsUpdateCoordinator
{
    private readonly Dispatcher _dispatcher = dispatcher;
    private bool _isPaused;
    private DispatcherTimer? _updateTimer;

    public DpsUpdateMode UpdateMode { get; private set; }
    public int UpdateInterval { get; private set; }
    public bool IsUpdateEnabled => _updateTimer?.IsEnabled ?? false;

    public event EventHandler? UpdateRequested;

    public void Configure(DpsUpdateMode mode, int intervalMs)
    {
        logger.LogInformation("Configuring update mode: {Mode}, Interval: {Interval}ms", mode, intervalMs);

        Stop();
        UpdateMode = mode;
        UpdateInterval = Math.Clamp(intervalMs, 100, 5000);

        if (mode == DpsUpdateMode.Active)
        {
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(UpdateInterval)
            };
            _updateTimer.Tick += OnTimerTick;
        }
        else
        {
            _updateTimer = null;
        }
    }

    public void Start()
    {
        if (UpdateMode == DpsUpdateMode.Active && _updateTimer != null)
        {
            _updateTimer.Start();
            logger.LogDebug("Update timer started with interval {Interval}ms", UpdateInterval);
        }
    }

    public void Stop()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= OnTimerTick;
            logger.LogDebug("Update timer stopped");
        }
    }

    public void Pause()
    {
        _isPaused = true;
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            logger.LogDebug("Updates paused");
        }
    }

    public void Resume()
    {
        _isPaused = false;
        if (UpdateMode == DpsUpdateMode.Active && _updateTimer != null)
        {
            _updateTimer.Start();
            logger.LogDebug("Updates resumed");
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_isPaused)
        {
            UpdateRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}