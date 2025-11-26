using System;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class PersonalDpsViewModel : BaseViewModel
{
    private readonly IWindowManagementService _windowManagementService;
    private readonly IDataStorage _dataStorage;
    private readonly Dispatcher _dispatcher;
    private readonly object _timerLock = new();
    private Timer? _remainingTimer;

    public PersonalDpsViewModel(
        IWindowManagementService windowManagementService,
        IDataStorage dataStorage,
        Dispatcher dispatcher)
    {
        _windowManagementService = windowManagementService;
        _dataStorage = dataStorage;
        _dispatcher = dispatcher;
        _dataStorage.BattleLogCreated += OnBattleLogCreated;
    }

    public TimeSpan TimeLimit { get; } = TimeSpan.FromMinutes(3);

    [ObservableProperty] private bool _startTraining;
    [ObservableProperty] private bool _enableTrainingMode;
    [ObservableProperty] private DateTime? _startTime;

    public double RemainingPercent
    {
        get
        {
            if (StartTime is null) return 100;

            var remaining = GetRemaining();
            return TimeLimit.TotalMilliseconds <= 0
                ? 0
                : Math.Max(0, remaining.TotalMilliseconds / TimeLimit.TotalMilliseconds * 100);
        }
    }

    public string RemainingTimeDisplay => FormatRemaining(GetRemaining());

    partial void OnStartTrainingChanged(bool value)
    {
        if (!value)
        {
            StartTime = null;
            StopTimer();
        }
    }

    partial void OnStartTimeChanged(DateTime? value)
    {
        RefreshRemaining();
        if (value is null)
        {
            StopTimer();
            return;
        }

        StartTimer();
    }

    private void RemainingTimerOnTick(object? state)
    {
        var remaining = GetRemaining();
        if (remaining <= TimeSpan.Zero)
        {
            StopTimer();
            _dispatcher.BeginInvoke(() => StartTraining = false);
            return;
        }
        _dispatcher.BeginInvoke(RefreshRemaining);
    }

    private TimeSpan GetRemaining()
    {
        if (StartTime is null) return TimeLimit;
        var remaining = TimeLimit - (DateTime.Now - StartTime.Value);
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private void OnBattleLogCreated(BattleLog log)
    {
        if (!StartTraining) return;
        if (log.AttackerUuid != _dataStorage.CurrentPlayerUUID) return;

        _dispatcher.BeginInvoke(() =>
        {
            if (!StartTraining) return;
            StartTime ??= DateTime.Now;
        });
    }

    private string FormatRemaining(TimeSpan time) => time.ToString(@"mm\:ss");

    private void RefreshRemaining()
    {
        OnPropertyChanged(nameof(RemainingPercent));
        OnPropertyChanged(nameof(RemainingTimeDisplay));
    }

    private void StartTimer()
    {
        lock (_timerLock)
        {
            _remainingTimer ??= new Timer(RemainingTimerOnTick, null, Timeout.Infinite, Timeout.Infinite);
            _remainingTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
        }
    }

    private void StopTimer()
    {
        lock (_timerLock)
        {
            _remainingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    [RelayCommand]
    private void ExportSrlogs()
    {
        // TODO: implement export logic
    }

    [RelayCommand]
    private void Clear()
    {
        // TODO: implement clear logic
    }

    [RelayCommand]
    private void CloseWindow()
    {
        _windowManagementService.PersonalDpsView.Close();
    }

    [RelayCommand]
    private void OpenDamageReferenceView()
    {
        _windowManagementService.DamageReferenceView.Show();
    }

    [RelayCommand]
    private void OpenSkillBreakdownView()
    {
        _windowManagementService.SkillBreakdownView.Show();
    }

    [RelayCommand]
    private void ShowStatisticsAndHidePersonal()
    {
        _windowManagementService.DpsStatisticsView.Show();
        _windowManagementService.PersonalDpsView.Hide();
    }
}
