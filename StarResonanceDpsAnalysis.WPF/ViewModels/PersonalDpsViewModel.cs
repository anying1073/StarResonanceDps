using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Converters;
using StarResonanceDpsAnalysis.WPF.Extensions;
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

public partial class PersonalDpsViewModel : BaseViewModel
{
    private readonly IWindowManagementService _windowManagementService;
    private readonly IDataStorage _dataStorage;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<PersonalDpsViewModel>? _logger;
    private readonly IConfigManager _configManager;
    private readonly object _timerLock = new();
    private Timer? _remainingTimer;

    // ⭐ 新增: 缓存上一次的显示数据（脱战后保持显示）
    private string _cachedDpsDisplay = "0 (0)";
    private double _cachedTeamPercent = 0;
    private string _cachedPercentDisplay = "0%";

    // ⭐ 新增: 标记是否正在等待新战斗开始
    private bool _awaitingNewBattle = false;

    public PersonalDpsViewModel(
        IWindowManagementService windowManagementService,
        IDataStorage dataStorage,
        Dispatcher dispatcher,
        IConfigManager configManager,
        ILogger<PersonalDpsViewModel>? logger = null)
    {
        _windowManagementService = windowManagementService;
        _dataStorage = dataStorage;
        _dispatcher = dispatcher;
        _configManager = configManager;
        _logger = logger;

        //throw new NotImplementedException();

        /*
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

        _logger?.LogInformation("PersonalDpsViewModel initialized");
        */
    }

    public TimeSpan TimeLimit { get; } = TimeSpan.FromMinutes(3);

    [ObservableProperty] private bool _startTraining;
    [ObservableProperty] private bool _enableTrainingMode;
    [ObservableProperty] private DateTime? _startTime;

    [ObservableProperty] private string _currentDpsDisplay = "0 (0)";
    [ObservableProperty] private double _teamDamagePercent = 0;
    [ObservableProperty] private string _teamPercentDisplay = "0%";

    // ⭐ 新增: 木桩类型选择（默认为中间木桩）
    [ObservableProperty] private DummyTargetType _selectedDummyTarget = DummyTargetType.Center;

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
        // ⭐ 联动打桩模式开关
        EnableTrainingMode = value;
        
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

    partial void OnEnableTrainingModeChanged(bool value)
    {
        _logger?.LogInformation("EnableTrainingMode changed to {Value}", value);
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

    /// <summary>
    /// ⭐ 修改: DPS数据更新事件处理
    /// 逻辑与 DpsStatisticsViewModel 一致：脱战后保持显示，下次战斗开始才清空
    /// </summary>
    private void OnDpsDataUpdated()
    {
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
            _logger?.LogInformation("个人模式检测到新战斗开始，清空上一场缓存数据");

            // 清空缓存的显示数据
            _cachedDpsDisplay = "0 (0)";
            _cachedTeamPercent = 0;
            _cachedPercentDisplay = "0%";

            // 重置等待标记
            _awaitingNewBattle = false;

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
                CurrentDpsDisplay = _cachedDpsDisplay;
                TeamDamagePercent = _cachedTeamPercent;
                TeamPercentDisplay = _cachedPercentDisplay;
                _logger?.LogWarning("CurrentPlayerUUID is still 0, using cached values");
                return;
            }

            var dpsDataDict = _dataStorage.GetStatistics(false);

            if (!dpsDataDict.TryGetValue(currentPlayerUid, out var currentPlayerData))
            {
                // ⭐ 修改: 找不到玩家数据时使用缓存值（脱战后数据被清空会走这里）
                CurrentDpsDisplay = _cachedDpsDisplay;
                TeamDamagePercent = _cachedTeamPercent;
                TeamPercentDisplay = _cachedPercentDisplay;
                _logger?.LogDebug("Player UID {UID} not found, using cached values (normal after disengagement)", currentPlayerUid);
                return;
            }

            // ⭐ 新增：如果开启打桩模式，需要过滤特定NPC的伤害
            ulong totalDamage;
            if (EnableTrainingMode && StartTraining)
            {
                // TODO: remove temporarily, need to add more mechanism to fix
                throw new NotImplementedException();
                /*
                // 打桩模式下，只统计对特定木桩的伤害
                totalDamage = 0;
                var battleLogs = currentPlayerData.GetOrCreateSkill();
                
                foreach (var log in battleLogs)
                {
                    // 只统计攻击类日志（非治疗）
                    if (log.IsHeal) continue;
                    
                    // 检查目标是否是NPC
                    if (!log.IsTargetPlayer)
                    {
                        // 获取NPC ID（从TargetUuid获取）
                        // 假设 NPC 的 TargetUuid 就是 NPC ID，可能需要根据实际协议调整
                        var npcId = log.TargetUuid; 
                        
                        // 检查是否应该统计这个NPC的伤害
                        if (ShouldCountNpcDamage(npcId))
                        {
                            totalDamage += (ulong)Math.Max(0, log.Value);
                        }
                    }
                }
                
                _logger?.LogDebug("打桩模式：木桩类型={Type}, 过滤后伤害={Damage}", SelectedDummyTarget, totalDamage);
                */
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

            var formattedDisplay = $"{FormatNumberByConfig(totalDamage)} ({FormatNumberByConfig((ulong)dps)})";

            // 计算团队总伤害占比
            var allPlayerData = dpsDataDict.Values.Where(d => !d.IsNpc).ToList();
            var teamTotalDamage = (ulong)allPlayerData.Sum(d => Math.Max(0, d.AttackDamage.Total));

            _logger?.LogDebug("Team Stats: TeamTotal={TeamTotal}, PlayerCount={Count}",
                teamTotalDamage, allPlayerData.Count);

            double percent = 0;
            string percentDisplay = "0%";

            if (teamTotalDamage > 0)
            {
                percent = (double)totalDamage / teamTotalDamage * 100.0;
                percent = Math.Min(100, Math.Max(0, percent));
                percentDisplay = $"{percent:F1}%";
            }

            // ⭐ 更新缓存（战斗中的最新数据）
            _cachedDpsDisplay = formattedDisplay;
            _cachedTeamPercent = percent;
            _cachedPercentDisplay = percentDisplay;

            // 更新UI显示
            CurrentDpsDisplay = formattedDisplay;
            TeamDamagePercent = percent;
            TeamPercentDisplay = percentDisplay;

            _logger?.LogDebug("Display Updated: DPS={Display}, Percent={Percent}",
                CurrentDpsDisplay, TeamPercentDisplay);
        }
        catch (Exception ex)
        {
            // 出错时使用缓存值
            CurrentDpsDisplay = _cachedDpsDisplay;
            TeamDamagePercent = _cachedTeamPercent;
            TeamPercentDisplay = _cachedPercentDisplay;
            _logger?.LogError(ex, "Error updating personal DPS, using cached values");
            Console.WriteLine($"Error updating personal DPS: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// ⭐ 根据配置格式化数字显示
    /// </summary>
    private string FormatNumberByConfig(ulong value)
    {
        var damageDisplayType = _configManager.CurrentConfig.DamageDisplayType;

        return ConverterNumberHelper.FormatHumanReadable(value, damageDisplayType, CultureInfo.CurrentCulture);
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

        var currentPlayerUid = _configManager.CurrentConfig.Uid;
        if (currentPlayerUid == 0) currentPlayerUid = log.AttackerUuid;

        if (log.AttackerUuid != currentPlayerUid) return;

        // ⭐ 新增：只有攻击指定木桩时才开始倒计时
        // 检查是否是攻击指定NPC的日志
        if (!log.IsHeal && !log.IsTargetPlayer)
        {
            var targetNpcId = log.TargetUuid;
            
            // 只有攻击指定木桩才启动倒计时
            if (!ShouldCountNpcDamage(targetNpcId))
            {
                _logger?.LogDebug("攻击的NPC ID={NpcId}不是指定木桩，不启动倒计时", targetNpcId);
                return;
            }
            
            _logger?.LogDebug("检测到攻击指定木桩 NPC ID={NpcId}，准备启动倒计时", targetNpcId);
        }
        else
        {
            // 不是攻击NPC的日志（治疗或攻击玩家），不启动倒计时
            _logger?.LogDebug("不是攻击NPC的日志，不启动倒计时");
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (!StartTraining) return;
            
            // 只有在尚未开始计时时才设置StartTime
            if (StartTime == null)
            {
                StartTime = DateTime.Now;
                _logger?.LogInformation("开始倒计时：{Time}", StartTime);
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

        _logger?.LogInformation("个人模式检测到脱战，设置等待新战斗标记");

        // ⭐ 关键：设置等待标记，但保持当前显示不变（使用缓存）
        _awaitingNewBattle = true;

        // 只刷新显示（会使用缓存值）
        UpdatePersonalDpsDisplay();
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
    private void Clear()
    {
        _logger?.LogInformation("个人模式Clear命令执行");

        _logger?.LogInformation("清空当前段落数据");
        _dataStorage.ClearDpsData();

        // ⭐ 修改: 重置缓存和状态
        _cachedDpsDisplay = "0 (0)";
        _cachedTeamPercent = 0;
        _cachedPercentDisplay = "0%";
        _awaitingNewBattle = false;

        // 重置计时器和训练状态
        StartTime = null;
        StartTraining = false;

        _logger?.LogInformation("个人模式Clear完成");
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