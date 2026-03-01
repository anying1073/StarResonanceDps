using System;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

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
    TDummy
}

public partial class PersonalDpsViewModel : BaseDispatcherSupportViewModel
{
    private readonly IWindowManagementService _windowManagementService;
    private readonly IDataStorage _dataStorage;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<PersonalDpsViewModel>? _logger;
    private readonly IConfigManager _configManager;
    private readonly IApplicationControlService _appControlService;
    private readonly object _timerLock = new();
    private Timer? _remainingTimer;

    // 缓存上一次的显示数据（脱战后保持显示）
    private double _cachedTotalDamage = 0;
    private double _cachedDps = 0;
    private double _cachedTeamPercent = 0;

    // 标记是否正在等待新战斗开始
    private bool _awaitingNewBattle = false;

    // ⭐ 新增：超时标记（用于打桩模式下的倒计时功能）
    private bool _isTimedOut = false;
    
    // 脱战时间戳（用于防止脱战后立即重新计时）
    private DateTime? _disengagementTime = null;
    
    // 正在清空标志（用于防止Clear期间的数据更新干扰显示）
    private bool _isClearing = false;

    public PersonalDpsViewModel(
        IWindowManagementService windowManagementService,
        IDataStorage dataStorage,
        Dispatcher dispatcher,
        IConfigManager configManager,
        IApplicationControlService appControlService,
        ILogger<PersonalDpsViewModel>? logger = null) : base(dispatcher)
    {
        _windowManagementService = windowManagementService;
        _dataStorage = dataStorage;
        _dispatcher = dispatcher;
        _configManager = configManager;
        _appControlService = appControlService;
        _logger = logger;
        AppConfig = _configManager.CurrentConfig;

        // ⭐ 订阅配置更新事件以响应主题颜色变化
        _configManager.ConfigurationUpdated += OnConfigurationUpdated;

        _logger?.LogInformation("PersonalDpsViewModel initialized");
    }

    public TimeSpan TimeLimit { get; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// ⭐ 主题颜色（用于渐变和文字）
    /// </summary>
    public string ThemeColor => _configManager.CurrentConfig.ThemeColor;

    [ObservableProperty] private AppConfig _appConfig = new();

    [ObservableProperty] private bool _startTraining;
    [ObservableProperty] private bool _enableTrainingMode;
    [ObservableProperty] private DateTime? _startTime;

    [ObservableProperty] private double _totalDamage;
    [ObservableProperty] private double _dps;
    [ObservableProperty] private double _teamDamagePercent = 0;

    // 木桩类型选择（默认为中间木桩）
    [ObservableProperty] private DummyTargetType _selectedDummyTarget = DummyTargetType.Center;

    public double RemainingPercent
    {
        get
        {
            if (StartTime is null) return 0;

            var elapsed = GetElapsed();
            return TimeLimit.TotalMilliseconds <= 0
                ? 0
                : Math.Min(1, elapsed.TotalMilliseconds / TimeLimit.TotalMilliseconds);
        }
    }

    public string RemainingTimeDisplay => FormatElapsed(GetElapsed());

    partial void OnStartTrainingChanged(bool value)
    {
        // ⭐ 联动打桩模式开关
        EnableTrainingMode = value;
        
        if (!value)
        {
            StartTime = null;
            StopTimer();
            RefreshRemaining();
        }
        else
        {
            // ⭐ 功能2：开启打桩模式时清空伤害统计
            _logger?.LogInformation("打桩模式开启，清空伤害统计");
            
            ResetDisplay();
            
            // 清空DataStorage的当前段落数据（只清空section，不清空full）
            _dataStorage.ClearDpsData();
            
            // 重置标记
            _awaitingNewBattle = false;
            _isTimedOut = false; // ⭐ 重置超时标记
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

    partial void OnEnableTrainingModeChanged(bool value)
    {
        _logger?.LogInformation("EnableTrainingMode changed to {Value}", value);
        
        // ⭐ 修复问题1：联动 StartTraining，确保开关同步
        StartTraining = value;
        
        UpdatePersonalDpsDisplay();
    }

    partial void OnSelectedDummyTargetChanged(DummyTargetType value)
    {
        _logger?.LogInformation("SelectedDummyTarget changed to {Value}", value);
        
        // ⭐ 保存用户选择到配置
        _configManager.CurrentConfig.DefaultDummyTarget = (int)value;
        // 异步保存配置
        _ = _configManager.SaveAsync();
        
        // 木桩切换时可能需要重置数据
        if (EnableTrainingMode && StartTraining)
        {
            _logger?.LogInformation("木桩类型切换，需要重新计算伤害数据");
            // 触发数据刷新
            UpdatePersonalDpsDisplay();
        }
    }

    // ⭐ 新增：根据木桩类型判断是否应该统计该NPC的伤害
    private bool ShouldCountNpcDamage(long targetNpcId)
    {
        // 如果未开启打桩模式，统计所有伤害
        if (!EnableTrainingMode)
            return true;

        // 根据选择的木桩类型过滤
        return SelectedDummyTarget switch
        {
            DummyTargetType.Center => targetNpcId == 75,   // 中间木桩 ID=75
            DummyTargetType.TDummy => targetNpcId == 179,  // T木桩 ID=179 ⭐ 修正
            _ => true // 默认统计所有
        };
    }

    [RelayCommand]
    private void Loaded()
    {
        // 订阅DPS数据更新事件
        _dataStorage.DpsDataUpdated += OnDpsDataUpdated;
        _dataStorage.BattleLogCreated += OnBattleLogCreated;

        // ⭐ 订阅脱战事件
        _dataStorage.NewSectionCreated += OnNewSectionCreated;

        // ⭐ 从配置加载上次选择的木桩类型
        var savedDummyTarget = _configManager.CurrentConfig.DefaultDummyTarget;
        if (Enum.IsDefined(typeof(DummyTargetType), savedDummyTarget))
        {
            SelectedDummyTarget = (DummyTargetType)savedDummyTarget;
            _logger?.LogInformation("从配置加载木桩类型: {Type}", SelectedDummyTarget);
        }

        // 立即尝试更新一次显示
        UpdatePersonalDpsDisplay();
    }

    [RelayCommand]
    private void UnLoaded()
    {
        // 订阅DPS数据更新事件
        _dataStorage.DpsDataUpdated -= OnDpsDataUpdated;
        _dataStorage.BattleLogCreated -= OnBattleLogCreated;

        // ⭐ 订阅脱战事件
        _dataStorage.NewSectionCreated -= OnNewSectionCreated;
    }

    /// <summary>
    /// ⭐ 修改: DPS数据更新事件处理
    /// 逻辑与 DpsStatisticsViewModel 一致：脱战后保持显示，下次战斗开始才清空
    /// </summary>
    private void OnDpsDataUpdated()
    {
        // 如果正在清空，直接忽略所有处理
        if (_isClearing)
        {
            _logger?.LogDebug("正在清空，忽略DpsDataUpdated事件");
            return;
        }
        
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(OnDpsDataUpdated);
            return;
        }

        _logger?.LogDebug("OnDpsDataUpdated called in PersonalDpsViewModel");

        var currentPlayerUid = _configManager.CurrentConfig.Uid;
        var dpsDataDict = _dataStorage.GetStatistics(false);

        // 检查是否有数据
        bool hasDataNow = dpsDataDict.Count > 0 &&
                          (currentPlayerUid == 0 || dpsDataDict.ContainsKey(currentPlayerUid));

        // ⭐ 关键逻辑：如果正在等待新战斗 且 现在有数据了 → 说明新战斗开始，清空缓存
        if (_awaitingNewBattle && hasDataNow)
        {
            // 检查是否是脱战后立即的数据更新（可能是延迟的数据包）
            if (_disengagementTime.HasValue)
            {
                var timeSinceDisengagement = DateTime.Now - _disengagementTime.Value;
                if (timeSinceDisengagement.TotalSeconds < 2.0)
                {
                    _logger?.LogDebug("脱战后{Seconds:F1}秒内的数据更新，忽略（可能是延迟数据）", timeSinceDisengagement.TotalSeconds);
                    UpdatePersonalDpsDisplay();
                    return;
                }
            }
            
            _logger?.LogInformation("个人模式检测到新战斗开始，清空上一场缓存数据");

            // 清空缓存的显示数据
            ResetDisplay();

            // 重置等待标记和脱战时间戳
            _awaitingNewBattle = false;
            _disengagementTime = null;

            // ⭐ 修改：只在非打桩模式下才重置训练状态
            // 打桩模式下应该保持训练状态，让倒计时继续
            if (!EnableTrainingMode)
            {
                StartTime = null;
                StartTraining = false;
                StopTimer();
                RefreshRemaining();
            }
            else
            {
                _logger?.LogInformation("打桩模式下检测到新战斗，保持训练状态");
                // 打桩模式下只重置计时器，不关闭训练
                StartTime = null;
                StopTimer();
                RefreshRemaining();
            }
        }

        // 总是更新显示
        UpdatePersonalDpsDisplay();
    }

    /// <summary>
    /// ⭐ 修改: 更新个人DPS显示（支持缓存和木桩过滤）
    /// </summary>
    private void UpdatePersonalDpsDisplay()
    {
        try
        {
            // 如果正在清空，不更新显示
            if (_isClearing)
            {
                _logger?.LogDebug("正在清空，不更新显示");
                return;
            }
            
            // ⭐ 功能3：倒计时超时后不更新显示（冻结在最后的值）
            if (_isTimedOut)
            {
                _logger?.LogDebug("倒计时已超时，不更新显示");
                return;
            }

            var currentPlayerUid = _configManager.CurrentConfig.Uid;

            // 如果UID为0,尝试自动检测第一个非NPC玩家
            if (currentPlayerUid == 0)
            {
                var dpsDict = _dataStorage.GetStatistics(false);

                var firstPlayer = dpsDict.Values.FirstOrDefault(d => !d.IsNpc);
                if (firstPlayer != null)
                {
                    currentPlayerUid = firstPlayer.Uid;
                    _logger?.LogInformation("Auto-detected player UID: {UID}", currentPlayerUid);
                }
            }

            _logger?.LogDebug("UpdatePersonalDpsDisplay: CurrentPlayerUUID={UUID}, DataCount={Count}",
                currentPlayerUid, _dataStorage.GetStatisticsCount(false));

            if (currentPlayerUid == 0)
            {
                // ⭐ 修改: 无UID时使用缓存值（而不是直接清零）
                ApplyCachedDisplay();
                _logger?.LogWarning("CurrentPlayerUUID is still 0, using cached values");
                return;
            }

            var dpsDataDict = _dataStorage.GetStatistics(false);

            if (!dpsDataDict.TryGetValue(currentPlayerUid, out var currentPlayerData))
            {
                // ⭐ 修改: 找不到玩家数据时使用缓存值（脱战后数据被清空会走这里）
                ApplyCachedDisplay();
                _logger?.LogDebug("Player UID {UID} not found, using cached values (normal after disengagement)", currentPlayerUid);
                return;
            }

            // ⭐ 修复：打桩模式下过滤特定NPC的伤害
            ulong totalDamage;
            if (EnableTrainingMode && StartTraining)
            {
                // 打桩模式下，只统计对特定木桩的伤害
                totalDamage = 0;
                
                try
                {
                    // 从DataStorage获取该玩家的所有战斗日志
                    var battleLogs = _dataStorage.GetBattleLogsForPlayer(currentPlayerUid, false);
                    
                    foreach (var log in battleLogs)
                    {
                        // 只统计攻击类日志（非治疗，且攻击者是当前玩家）
                        if (log.IsHeal || log.AttackerUuid != currentPlayerUid) 
                            continue;
                        
                        // 检查目标是否是玩家（如果是玩家则跳过）
                        if (log.IsTargetPlayer) 
                            continue;
                        
                        // 获取NPC ID
                        var npcId = log.TargetUuid; 
                        
                        // 检查是否应该统计这个NPC的伤害
                        if (ShouldCountNpcDamage(npcId))
                        {
                            totalDamage += (ulong)Math.Max(0, log.Value);
                        }
                    }
                    
                    _logger?.LogDebug("打桩模式：木桩类型={Type}, 过滤后伤害={Damage}", SelectedDummyTarget, totalDamage);
                }
                catch (NotSupportedException)
                {
                    // 如果使用的是旧的DataStorage实现，回退到显示所有伤害
                    _logger?.LogWarning("当前DataStorage不支持GetBattleLogsForPlayer，显示所有伤害");
                    totalDamage = Math.Max(0, currentPlayerData.AttackDamage.Total).ConvertToUnsigned();
                }
            }
            else
            {
                // 非打桩模式，统计所有伤害
                totalDamage = Math.Max(0, currentPlayerData.AttackDamage.Total).ConvertToUnsigned();
            }

            // 计算经过的秒数
            var elapsedTicks = currentPlayerData.ElapsedTicks();
            var elapsedSeconds = elapsedTicks > 0 ? TimeSpan.FromTicks(elapsedTicks).TotalSeconds : 0;

            var dps = elapsedSeconds > 0 ? totalDamage / elapsedSeconds : 0;

            _logger?.LogDebug("Player DPS: TotalDamage={Damage}, ElapsedTicks={Ticks}, ElapsedSeconds={Elapsed:F1}, DPS={DPS:F0}",
                totalDamage, elapsedTicks, elapsedSeconds, dps);

            // 计算团队总伤害占比
            double percent = 0;
            
            // 打桩模式下不计算团队百分比（保持00%）
            if (EnableTrainingMode && StartTraining)
            {
                percent = 1.0; // 100%
                _logger?.LogDebug("打桩模式：团队百分比固定为100%");
            }
            else
            {
                var allPlayerData = dpsDataDict.Values.Where(d => !d.IsNpc).ToList();
                var teamTotalDamage = (ulong)allPlayerData.Sum(d => Math.Max(0, d.AttackDamage.Total));

                _logger?.LogDebug("Team Stats: TeamTotal={TeamTotal}, PlayerCount={Count}",
                    teamTotalDamage, allPlayerData.Count);

                if (teamTotalDamage > 0)
                {
                    percent = (double)totalDamage / teamTotalDamage;
                    percent = Math.Min(1, Math.Max(0, percent));
                }
            }

            // ⭐ 更新缓存（战斗中的最新数据）
            SetDisplay(totalDamage, dps, percent);

            _logger?.LogDebug("Display Updated: TotalDamage={Damage}, DPS={Dps}, Percent={Percent:P1}",
                totalDamage, dps, percent);
        }
        catch (Exception ex)
        {
            // 出错时使用缓存值
            ApplyCachedDisplay();
            _logger?.LogError(ex, "Error updating personal DPS, using cached values");
            Console.WriteLine($"Error updating personal DPS: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void ResetDisplay()
    {
        SetDisplay(0, 0, 0);
    }

    private void SetDisplay(double totalDamage, double dps, double percent)
    {
        _cachedTotalDamage = totalDamage;
        _cachedDps = dps;
        _cachedTeamPercent = percent;

        TotalDamage = totalDamage;
        Dps = dps;
        TeamDamagePercent = percent;
    }

    private void ApplyCachedDisplay()
    {
        TotalDamage = _cachedTotalDamage;
        Dps = _cachedDps;
        TeamDamagePercent = _cachedTeamPercent;
    }

    private void RemainingTimerOnTick(object? state)
    {
        var elapsed = GetElapsed();
        
        // 打桩模式下，3分钟后停止
        if (EnableTrainingMode && StartTraining && elapsed >= TimeLimit)
        {
            _isTimedOut = true;
            _logger?.LogInformation("打桩模式3分钟计时结束，停止记录伤害");
            
            StopTimer();
            InvokeOnDispatcher(() =>
            {
                StartTraining = false;
                RefreshRemaining();
            });
            return;
        }
        
        InvokeOnDispatcher(RefreshRemaining);
    }

    private TimeSpan GetElapsed()
    {
        if (StartTime is null) return TimeSpan.Zero;
        return DateTime.Now - StartTime.Value;
    }
    
    private TimeSpan GetRemaining()
    {
        if (StartTime is null) return TimeLimit;
        var remaining = TimeLimit - (DateTime.Now - StartTime.Value);
        return remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private void OnBattleLogCreated(BattleLog log)
    {
        var currentPlayerUid = _configManager.CurrentConfig.Uid;
        if (currentPlayerUid == 0) currentPlayerUid = log.AttackerUuid;

        if (log.AttackerUuid != currentPlayerUid) return;

        // ⭐ BUG修复：如果是治疗日志，不触发计时（只有伤害数据才触发）
        if (log.IsHeal)
        {
            _logger?.LogDebug("治疗日志，不触发计时");
            return;
        }

        // 如果正在等待新战斗（已经脱战），检查是否真的是新战斗
        if (_awaitingNewBattle)
        {
            // 如果刚脱战不久（2秒内），不启动计时（可能是延迟的战斗日志）
            if (_disengagementTime.HasValue)
            {
                var timeSinceDisengagement = DateTime.Now - _disengagementTime.Value;
                if (timeSinceDisengagement.TotalSeconds < 2.0)
                {
                    _logger?.LogDebug("脱战后{Seconds:F1}秒内的战斗日志，忽略（_awaitingNewBattle=true）", timeSinceDisengagement.TotalSeconds);
                    return;
                }
            }
            _logger?.LogDebug("正在等待新战斗，不启动计时（_awaitingNewBattle=true）");
            return;
        }

        // 检查是否是攻击NPC的日志（已经排除了治疗）
        if (!log.IsTargetPlayer)
        {
            var targetNpcId = log.TargetUuid;
            
            // 打桩模式下，只有攻击指定木桩才启动计时
            if (EnableTrainingMode && StartTraining && !ShouldCountNpcDamage(targetNpcId))
            {
                _logger?.LogDebug("打桩模式：攻击的NPC ID={NpcId}不是指定木桩，不启动计时", targetNpcId);
                return;
            }
            
            _logger?.LogDebug("检测到攻击 NPC ID={NpcId}，准备启动计时", targetNpcId);
        }
        else
        {
            // 攻击玩家的情况（PVP）
            if (!EnableTrainingMode)
            {
                _logger?.LogDebug("非打桩模式：检测到攻击玩家，准备启动计时");
            }
            else
            {
                // 打桩模式下攻击玩家不启动计时
                _logger?.LogDebug("打桩模式：攻击玩家，不启动计时");
                return;
            }
        }

        _dispatcher.BeginInvoke(() =>
        {
            // 打桩模式下如果已经超时，不再重新开始计时
            if (EnableTrainingMode && _isTimedOut)
            {
                _logger?.LogDebug("打桩模式已超时，不再重新开始计时");
                return;
            }
            
            // 只有在尚未开始计时时才设置StartTime
            if (StartTime == null)
            {
                StartTime = DateTime.Now;
                _logger?.LogInformation("开始计时：{Time}，_awaitingNewBattle={Awaiting}", StartTime, _awaitingNewBattle);
            }
            else
            {
                _logger?.LogDebug("计时已经在进行中，StartTime={StartTime}", StartTime);
            }
        });
    }

    /// <summary>
    /// ⭐ 修改: 处理脱战事件
    /// 设置等待标记，但不清空显示（保持上一场数据）
    /// </summary>
    private void OnNewSectionCreated()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(OnNewSectionCreated);
            return;
        }

        _logger?.LogInformation("==== 个人模式检测到脱战 ====");

        // ⭐ 关键：设置等待标记，但保持当前显示不变（使用缓存）
        _awaitingNewBattle = true;
        _disengagementTime = DateTime.Now; // 记录脱战时间
        
        // 非打桩模式下，脱战后停止计时
        if (!EnableTrainingMode)
        {
            _logger?.LogWarning("非打桩模式：脱战前 StartTime={StartTime}", StartTime);
            
            // 停止计时器
            StopTimer();
            
            // 重置 StartTime
            StartTime = null;
            
            // 刷新 UI
            RefreshRemaining();
            
            _logger?.LogWarning("非打桩模式：脱战后 StartTime={StartTime}，计时器已停止，显示应为 00:00", StartTime);
        }
        else
        {
            _logger?.LogInformation("打桩模式：脱战后保持计时，StartTime={StartTime}", StartTime);
        }

        // 只刷新显示（会使用缓存值）
        UpdatePersonalDpsDisplay();
        
        _logger?.LogInformation("==== 脱战处理完成 ====");
    }

    private string FormatElapsed(TimeSpan time) => time.ToString(@"mm\:ss");
    
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
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Clear);
            return;
        }

        _logger?.LogWarning("==== 个人模式Clear命令开始执行 ====");

        // 设置清空标志，阻止数据更新事件干扰
        _isClearing = true;
        
        try
        {
            // 1. 清空所有状态标记
            _awaitingNewBattle = false;
            _disengagementTime = null;
            _isTimedOut = false;
            
            _logger?.LogInformation("1. 状态标记已清空");
            
            // 2. 重置计时器
            StopTimer();
            StartTime = null;
            
            _logger?.LogInformation("2. 计时器已停止，StartTime={StartTime}", StartTime);
            
            // 3. 清空训练状态
            StartTraining = false;
            EnableTrainingMode = false;
            
            _logger?.LogInformation("3. 训练状态已清空");
            
            // 4. 清空显示数据（直接设置字段和属性）
            _cachedTotalDamage = 0;
            _cachedDps = 0;
            _cachedTeamPercent = 0;
            TotalDamage = 0;
            Dps = 0;
            TeamDamagePercent = 0;
            
            _logger?.LogInformation("4. 显示数据已清空：TotalDamage={Total}, Dps={Dps}, Percent={Percent}", 
                TotalDamage, Dps, TeamDamagePercent);
            
            // 5. 刷新UI
            RefreshRemaining();
            
            _logger?.LogInformation("5. UI已刷新");
            
            // 6. 最后清空数据存储
            _logger?.LogInformation("6. 开始清空数据存储...");
            _dataStorage.ClearDpsData();
            _logger?.LogInformation("6. 数据存储已清空");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Clear执行过程中发生错误");
        }
        finally
        {
            // 确保清空标志被重置
            _isClearing = false;
            _logger?.LogWarning("==== 个人模式Clear命令执行完成 ====");
        }
    }

    [RelayCommand]
    private void CloseWindow()
    {
        _appControlService.Shutdown();
    }

    [RelayCommand]
    private void OpenDamageReferenceView()
    {
        _windowManagementService.DamageReferenceView.Show();
    }

    [RelayCommand]
    private void OpenSkillLog()
    {
        _logger?.LogInformation("打开技能日记窗口");
        _windowManagementService.SkillLogView.Show();
        _windowManagementService.SkillLogView.Activate();
    }

    [RelayCommand]
    private void OpenSkillBreakdownView()
    {
        try
        {
            var currentPlayerUid = _configManager.CurrentConfig.Uid;
            
            // 如果UID为0，尝试自动检测第一个非NPC玩家
            if (currentPlayerUid == 0)
            {
                var dpsDict = _dataStorage.GetStatistics(false);
                var firstPlayer = dpsDict.Values.FirstOrDefault(d => !d.IsNpc);
                if (firstPlayer != null)
                {
                    currentPlayerUid = firstPlayer.Uid;
                    _logger?.LogInformation("Auto-detected player UID for skill breakdown: {UID}", currentPlayerUid);
                }
            }
            
            if (currentPlayerUid == 0)
            {
                _logger?.LogWarning("无法打开技能详情：未找到玩家UID");
                return;
            }
            
            var vm = _windowManagementService.SkillBreakdownView.DataContext as SkillBreakdownViewModel;
            if (vm == null)
            {
                _logger?.LogError("SkillBreakdownViewModel is null");
                return;
            }
            
            // 获取玩家统计数据
            var playerStats = _dataStorage.GetStatistics(false);
            if (!playerStats.TryGetValue(currentPlayerUid, out var stats))
            {
                _logger?.LogWarning("未找到玩家 {UID} 的统计数据", currentPlayerUid);
                return;
            }
            
            _logger?.LogInformation("Opening SkillBreakdownView for player {UID}", currentPlayerUid);
            
            // 尝试获取玩家信息（可能不存在）
            PlayerInfo? playerInfo = null;
            try
            {
                // 从存储的玩家信息中查找
                var allStats = _dataStorage.GetStatistics(false);
                if (allStats.TryGetValue(currentPlayerUid, out var playerStat))
                {
                    // 使用统计数据中的昵称等信息
                    _logger?.LogDebug("Found player stats for UID {UID}", currentPlayerUid);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get player info for UID {UID}", currentPlayerUid);
            }
            
            // 初始化 ViewModel
            vm.InitializeFrom(stats, playerInfo, StatisticType.Damage);
            
            // 显示窗口
            _windowManagementService.SkillBreakdownView.Show();
            _windowManagementService.SkillBreakdownView.Activate();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error opening skill breakdown view");
        }
    }

    [RelayCommand]
    private void ShowStatisticsAndHidePersonal()
    {
        _windowManagementService.DpsStatisticsView.Show();
        _windowManagementService.PersonalDpsView.Hide();
    }
}
