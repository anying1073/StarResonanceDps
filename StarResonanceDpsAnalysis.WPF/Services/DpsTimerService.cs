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
    private readonly DispatcherTimer _updateTimer;
    private DateTime _startedTime;
    private TimeSpan _sectionStartElapsed = TimeSpan.Zero;
    private TimeSpan _totalCombatDuration = TimeSpan.Zero;

    public TimeSpan BattleDuration { get; private set; }
    public TimeSpan TotalCombatDuration => _totalCombatDuration;
    public bool IsRunning => _updateTimer.IsEnabled;

    public event EventHandler<TimeSpan>? DurationChanged;

    public DpsTimerService()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (s, e) =>
        {
            DurationChanged?.Invoke(this, BattleDuration);
        };
    }

    public void Start()
    {
        if (!IsRunning)
        {
            _startedTime = DateTime.UtcNow;
            _updateTimer.Start();
        }
    }

    public void Stop()
    {
        _updateTimer.Stop();
    }

    public void Reset()
    {
        Stop();
        _sectionStartElapsed = TimeSpan.Zero;
        _totalCombatDuration = TimeSpan.Zero;
        BattleDuration = TimeSpan.Zero;
        DurationChanged?.Invoke(this, BattleDuration);
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
        _sectionStartElapsed = GetElapsedTime();
    }

    public TimeSpan GetSectionElapsed()
    {
        if (!IsRunning) return TimeSpan.Zero;

        var elapsed = GetElapsedTime() - _sectionStartElapsed;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    private TimeSpan GetElapsedTime()
    {
        return DateTime.UtcNow - _startedTime;
    }
}
