using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;
using StarResonanceDpsAnalysis.WPF.ViewModels.DpsStatisticDataEngine;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// 木桩类型枚举
/// </summary>
public enum DummyTargetType
{
    /// <summary>
    /// 中间木桩
    /// </summary>
    Center,

    /// <summary>
    /// T木桩
    /// </summary>
    TDummy,

    /// <summary>
    /// AOE木桩（适合测试范围伤害）
    /// </summary>
    AoeDummy,
}

public partial class PersonalDpsViewModel : BaseDispatcherSupportViewModel
{
    private readonly DataSourceEngine _engine;
    private readonly IMessageDialogService _messageDialogService;
    private readonly IConfigManager _configManager;
    private readonly IDataStorage _dataStorage;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<PersonalDpsViewModel>? _logger;
    private readonly object _timerLock = new();
    private readonly IWindowManagementService _windowManagementService;
    [ObservableProperty] private AppConfig _appConfig = new();
    [ObservableProperty] private double _totalDamage;
    [ObservableProperty] private double _dps;
    private bool _isLoaded;
    private readonly DispatcherTimer _timer;

    // 木桩类型选择（默认为中间木桩）
    [ObservableProperty] private DummyTargetType _selectedDummyTarget = DummyTargetType.Center;

    public PersonalDpsViewModel(IWindowManagementService windowManagementService,
        IDataStorage dataStorage,
        Dispatcher dispatcher,
        IConfigManager configManager,
        DataSourceEngine engine,
        IMessageDialogService messageDialogService,
        ILogger<PersonalDpsViewModel>? logger = null) : base(dispatcher)
    {
        _windowManagementService = windowManagementService;
        _dataStorage = dataStorage;
        _dispatcher = dispatcher;
        _configManager = configManager;
        _engine = engine;
        _messageDialogService = messageDialogService;
        _logger = logger;
        _timer = new DispatcherTimer()
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _timer.Tick += RemainingTimerOnTick;
        AppConfig = _configManager.CurrentConfig;
        TimeLimit = TimeSpan.FromSeconds(AppConfig.TrainingDuration);
        _engine.Configure(new DataSourceEngineParam()
        {
            TrainingTimeLimit = TimeLimit,
        });

        _logger?.LogInformation("PersonalDpsViewModel initialized");
    }

    public TimeSpan TimeLimit { get; }

    /// <summary>
    /// ⭐ 主题颜色（用于渐变和文字）
    /// </summary>
    public string ThemeColor => _configManager.CurrentConfig.ThemeColor;

    public double RemainingPercent
    {
        get
        {
            var elapsed = _engine.CurrentSource.BattleDuration;
            return TimeLimit.TotalMilliseconds <= 0
                ? 0
                : Math.Min(1, elapsed.TotalMilliseconds / TimeLimit.TotalMilliseconds);
        }
    }

    public string RemainingTimeDisplay => FormatRemaining(GetRemaining());

    partial void OnSelectedDummyTargetChanged(DummyTargetType value)
    {
        _logger?.LogInformation("SelectedDummyTarget changed to {Value}", value);

        _engine.Configure(new DataSourceEngineParam()
        {
            DummyTarget = value
        });

        // ⭐ 保存用户选择到配置
        _configManager.CurrentConfig.DefaultDummyTarget = value;
        // 异步保存配置
        _ = _configManager.SaveAsync();
    }

    private void EngineOnProcessedDataReady(object? sender,
        Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> data)
    {
        InvokeOnDispatcher(() => UpdateDisplayFromSource(data));
    }

    private void UpdateDisplayFromSource(Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>> data)
    {
        if (!data.TryGetValue(StatisticType.Damage, out var damageData) || damageData.Count == 0)
        {
            ResetDisplay();
            return;
        }

        var currentPlayerUid = _configManager.CurrentConfig.Uid;
        if (currentPlayerUid <= 0)
        {
            currentPlayerUid = damageData.Keys.FirstOrDefault();
        }

        if (currentPlayerUid <= 0 || !damageData.TryGetValue(currentPlayerUid, out var playerData))
        {
            ResetDisplay();
            return;
        }

        var total = (double)playerData.Value;
        var dps = playerData.ValuePerSecond;
        SetDisplay(total, dps);
    }

    private void ResetDisplay()
    {
        SetDisplay(0, 0);
    }

    private void SetDisplay(double totalDamage, double dps)
    {
        TotalDamage = totalDamage;
        Dps = dps;
        OnPropertyChanged(nameof(TotalDamage));
        OnPropertyChanged(nameof(Dps));
    }

    private void RemainingTimerOnTick(object? state, EventArgs eventArgs)
    {
        var elapsed = _engine.CurrentSource.BattleDuration;

        // 打桩模式下，3分钟后停止
        if (elapsed >= TimeLimit)
        {
            _logger?.LogInformation("打桩模式3分钟计时结束，停止记录伤害");

            StopTimer();
        }

        BeginInvokeOnDispatcher(RefreshRemaining);
    }

    private TimeSpan GetRemaining()
    {
        var remaining = TimeLimit - _engine.CurrentSource.BattleDuration;
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private static string FormatRemaining(TimeSpan time)
    {
        return time.ToString(@"mm\:ss");
    }

    private void RefreshRemaining()
    {
        OnPropertyChanged(nameof(RemainingPercent));
        OnPropertyChanged(nameof(RemainingTimeDisplay));
    }


    private void StartTimer()
    {
        lock (_timerLock)
        {
            _timer.Start();
        }
    }

    private void StopTimer()
    {
        lock (_timerLock)
        {
            _timer.Stop();
        }
    }

    /// <summary>
    /// ⭐ 配置更新事件处理（支持主题颜色实时更新）
    /// </summary>
    private void OnConfigurationUpdated(object? sender, AppConfig newConfig)
    {
        _dispatcher.BeginInvoke(() =>
        {
            AppConfig = newConfig;
            OnPropertyChanged(nameof(ThemeColor));
        });
    }

    [RelayCommand]
    public void Clear()
    {
        // 确保在UI线程上执行
        InvokeOnDispatcher(Do);
        return;

        void Do()
        {
            _logger?.LogWarning("==== 个人模式Clear命令开始执行 ====");

            try
            {
                _engine.CurrentSource.Reset();
                ResetDisplay();
                RefreshRemaining();
                StartTimer();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Clear执行过程中发生错误");
            }
            finally
            {
                // 确保清空标志被重置
                _logger?.LogWarning("==== 个人模式Clear命令执行完成 ====");
            }
        }
    }
}