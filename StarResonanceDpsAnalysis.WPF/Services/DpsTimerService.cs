using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace StarResonanceDpsAnalysis.WPF.Services;

/// <summary>
/// Implementation of DPS timer service using System.Diagnostics.Stopwatch
/// Tracks both section-level and total combat duration
/// </summary>
public class DpsTimerService : IDpsTimerService
{
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _updateTimer;
    private TimeSpan _sectionStartElapsed = TimeSpan.Zero;
    private TimeSpan _totalCombatDuration = TimeSpan.Zero;

    public TimeSpan BattleDuration { get; private set; }
    public TimeSpan TotalCombatDuration => _totalCombatDuration;
    public bool IsRunning => _stopwatch.IsRunning;

    public event EventHandler<TimeSpan>? DurationChanged;

    public DpsTimerService()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (s, e) =>
        {
            BattleDuration = _stopwatch.Elapsed;
            DurationChanged?.Invoke(this, BattleDuration);
        };
    }

    public void Start()
    {
        if (!_stopwatch.IsRunning)
        {
            _stopwatch.Start();
            _updateTimer.Start();
        }
    }

    public void Stop()
    {
        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
            _updateTimer.Stop();
        }
    }

    public void Reset()
    {
        _stopwatch.Reset();
        _sectionStartElapsed = TimeSpan.Zero;
        _totalCombatDuration = TimeSpan.Zero;
        BattleDuration = TimeSpan.Zero;
        DurationChanged?.Invoke(this, BattleDuration);
    }

    public TimeSpan GetSectionDuration()
    {
        if (!_stopwatch.IsRunning)
            return TimeSpan.Zero;

        var elapsed = _stopwatch.Elapsed - _sectionStartElapsed;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    public void StartNewSection()
    {
        // Save current section duration before starting new section
        var currentSectionDuration = GetSectionElapsed();
        if (currentSectionDuration > TimeSpan.Zero)
        {
            _totalCombatDuration += currentSectionDuration;
        }

        // Mark new section start
        _sectionStartElapsed = _stopwatch.Elapsed;
    }

    public TimeSpan GetSectionElapsed()
    {
        if (!_stopwatch.IsRunning)
            return TimeSpan.Zero;

        var elapsed = _stopwatch.Elapsed - _sectionStartElapsed;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }
}
