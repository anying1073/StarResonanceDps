using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.Exceptions;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Converters;
using StarResonanceDpsAnalysis.WPF.Data;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Services;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class DpsStatisticsOptions : BaseViewModel
{
    [ObservableProperty] private int _minimalDurationInSeconds;

    [RelayCommand]
    private void SetMinimalDuration(int duration)
    {
        MinimalDurationInSeconds = duration;
    }
}

public partial class DpsStatisticsViewModel : BaseViewModel
{
    private readonly IApplicationController _appController;
    private readonly Stopwatch _battleTimer = new();
    private readonly IConfigManager _configManager;
    private readonly IWindowManagementService _windowManagement;
    private readonly IDataSource _dataSource;

    private readonly Stopwatch _fullBattleTimer = new();
    private readonly ILogger<DpsStatisticsViewModel> _logger;
    private readonly Random _rd = new();
    private readonly Dictionary<long, StatisticDataViewModel> _slotsDictionary = new();
    private readonly IDataStorage _storage;
    private readonly long[] _totals = new long[6]; // 6位玩家示例

    [ObservableProperty] private DateTime _battleDuration;
    [ObservableProperty] private NumberDisplayMode _numberDisplayMode = NumberDisplayMode.Wan;
    [ObservableProperty] private ScopeTime _scopeTime = ScopeTime.Current;
    [ObservableProperty] private bool _showContextMenu;
    [ObservableProperty] private BulkObservableCollection<StatisticDataViewModel> _slots = new();
    [ObservableProperty] private SortDirectionEnum _sortDirection = SortDirectionEnum.Descending;
    [ObservableProperty] private string _sortMemberPath = "Value";
    [ObservableProperty] private StatisticType _statisticIndex;

    /// <inheritdoc/>
    public DpsStatisticsViewModel(IApplicationController appController,
        IDataSource dataSource,
        IDataStorage storage,
        ILogger<DpsStatisticsViewModel> logger,
        IConfigManager configManager,
        IWindowManagementService windowManagement)
    {
        _appController = appController;
        _storage = storage;
        _logger = logger;
        _dataSource = dataSource;
        _configManager = configManager;
        _windowManagement = windowManagement;
        _slots.CollectionChanged += SlotsChanged;
        return;

        void SlotsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Debug.Assert(e.NewItems != null, "e.NewItems != null");
                    LocalIterate(e.NewItems, item => _slotsDictionary.Add(item.Player.Uid, item));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    Debug.Assert(e.OldItems != null, "e.OldItems != null");
                    LocalIterate(e.OldItems, itm => _slotsDictionary.Remove(itm.Player.Uid));
                    break;
                case NotifyCollectionChangedAction.Replace:
                    Debug.Assert(e.OldItems != null, "e.OldItems != null");
                    LocalIterate(e.OldItems, item => _slotsDictionary[item.Player.Uid] = item);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    _slotsDictionary.Clear();
                    break;
                case NotifyCollectionChangedAction.Move:
                    // just ignore
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return;

            void LocalIterate(IList list, Action<StatisticDataViewModel> action)
            {
                foreach (StatisticDataViewModel item in list)
                {
                    action.Invoke(item);
                }
            }
        }
    }

    public DpsStatisticsOptions Options { get; } = new();
    private Stopwatch InUsingTimer => ScopeTime == ScopeTime.Total ? _fullBattleTimer : _battleTimer;

    /// <summary>
    /// 读取用户缓存
    /// </summary>
    private void LoadPlayerCache()
    {
        try
        {
            _storage.LoadPlayerInfoFromFile();
        }
        catch (FileNotFoundException)
        {
            // 没有缓存
        }
        catch (DataTamperedException)
        {
            _storage.ClearAllPlayerInfos();
            _storage.SavePlayerInfoToFile();
        }
    }

    [RelayCommand]
    private void OnLoaded()
    {
        _logger.LogDebug("VM Loaded");
        // 开始监听DPS更新事件
        _storage.DpsDataUpdated += DataStorage_DpsDataUpdated;
    }


    private void DataStorage_DpsDataUpdated()
    {
        if (!_fullBattleTimer.IsRunning)
        {
            _fullBattleTimer.Restart();
        }

        if (!_battleTimer.IsRunning)
        {
            _battleTimer.Restart();
        }

        var dpsList = ScopeTime == ScopeTime.Total
            ? DataStorage.ReadOnlyFullDpsDataList
            : DataStorage.ReadOnlySectionedDpsDataList;
        dpsList = new List<DpsData>()
        {
            new DpsData() {TotalAttackDamage = Random.Shared.Next(19999), UID = 1},
            new DpsData() {TotalAttackDamage = Random.Shared.Next(19999), UID = 2},
            new DpsData() {TotalAttackDamage = Random.Shared.Next(19999), UID=3}
        };
        foreach (var item in dpsList)
        {
            item.SkillDic[123] = new SkillData()
            {
                CritTimes = 10,
                LuckyTimes = 3,
                SkillId = 1000,
                TotalValue = 10000,
                UseTimes = 100,
            };
            item.SkillDic[456] = new SkillData() { CritTimes = 5, LuckyTimes = 1, SkillId = 2000, TotalValue = 10000, UseTimes = 983 };
            item.SkillDic[789] = new SkillData() { CritTimes = 8, LuckyTimes = 2, SkillId = 3000, TotalValue = 10000, UseTimes = 18 };
            item.SkillDic[101112] = new SkillData() { CritTimes = 12, LuckyTimes = 4, SkillId = 4000, TotalValue = 1023, UseTimes = 123 };
        }
        UpdateData(dpsList);
    }


    private void UpdateData(IReadOnlyList<DpsData> data)
    {
        _logger.LogDebug("Enter updatedata");
        // 根据数据更新数据
        foreach (var dpsData in data)
        {
            var value = GetValue(dpsData, ScopeTime, StatisticIndex);
            PlayerInfo? playerInfo;
            if (!_slotsDictionary.TryGetValue(dpsData.UID, out var slot))
            {
                var ret = _storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.UID, out playerInfo);
                var @class = ret ? ((int)playerInfo!.ProfessionID!).GetClassNameById() : Classes.Unknown;
                slot = new StatisticDataViewModel
                {
                    Index = 999,
                    Value = (ulong)value, // TODO: 将 long 转为 ulong
                    Duration = (ulong)(dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0)),
                    Player = new PlayerInfoViewModel
                    {
                        Uid = dpsData.UID,
                        Class = @class,
                        Guild = "Unknown",
                        Name = ret ? playerInfo?.Name ?? $"UID: {dpsData.UID}" : $"UID: {dpsData.UID}",
                        Spec = playerInfo?.Spec ?? ClassSpec.Unknown
                    },
                    GetSkillList = (player) =>
                    {
                        // Try to get real data first, fallback to test data
                        if (_storage.ReadOnlyFullDpsDatas.ContainsKey(player.Uid))
                        {
                            return _storage.ReadOnlyFullDpsDatas[player.Uid].ReadOnlySkillDataList.Select(item =>
                            {
                                return new SkillItemViewModel()
                                {
                                    SkillName = item.SkillId.ToString(),
                                    AvgDamage = 1000,
                                    CritCount = item.CritTimes,
                                    HitCount = item.UseTimes,
                                    TotalDamage = item.TotalValue,
                                };
                            }).ToList();
                        }
                        else
                        {
                            // Fallback test data for debugging
                            return new List<SkillItemViewModel>
                            {
                                new() { SkillName = $"Skill 1000", TotalDamage = 10000, HitCount = 100, CritCount = 10, AvgDamage = 100 },
                                new() { SkillName = $"Skill 2000", TotalDamage = 10000, HitCount = 983, CritCount = 5, AvgDamage = 10 },
                                new() { SkillName = $"Skill 3000", TotalDamage = 10000, HitCount = 18, CritCount = 8, AvgDamage = 555 },
                                new() { SkillName = $"Skill 4000", TotalDamage = 1023, HitCount = 123, CritCount = 12, AvgDamage = 8 }
                            };
                        }
                    },
                };
                Slots.Add(slot);
            }
            // Simplified update of existing slot (replaces the selected block)
            var unsignedValue = value < 0 ? 0UL : (ulong)value;
            var durationTicks = dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0);
            var duration = durationTicks < 0 ? 0UL : (ulong)durationTicks;

            // use the out variable `slot` from TryGetValue above for in-place update
            slot.Value = unsignedValue;
            slot.Duration = duration;

            if (_storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.UID, out playerInfo))
            {
                slot.Player.Name = playerInfo.Name ?? $"UID: {dpsData.UID}";
                slot.Player.Class = playerInfo.ProfessionID.HasValue == true
                    ? ((int)playerInfo.ProfessionID!).GetClassNameById()
                    : Classes.Unknown;
                slot.Player.Spec = playerInfo?.Spec ?? ClassSpec.Unknown;
            }
            else
            {
                slot.Player.Name = $"UID: {dpsData.UID}";
                slot.Player.Class = Classes.Unknown;
                slot.Player.Spec = ClassSpec.Unknown;
            }
        }

        // Calculate percentage of max
        if (Slots.Count > 0)
        {
            var maxValue = Slots.Max(d => d.Value);
            foreach (var slot in Slots)
            {
                slot.PercentOfMax = maxValue > 0 ? slot.Value / (double)maxValue * 100 : 0;
            }

            // Calculate percentage of total
            var totalValue = Slots.Sum(d => Convert.ToDouble(d.Value));
            foreach (var slot in Slots)
            {
                slot.Percent = totalValue > 0 ? slot.Value / totalValue : 0;
            }
        }

        // Sort data in place 
        SortSlotsInPlace();

        _logger.LogDebug("Exit updatedata");

        return;

        long GetValue(DpsData dpsData, ScopeTime scopeTime, StatisticType statisticType)
        {
            Debug.Assert(dpsData.IsNpcData == (statisticType == StatisticType.NpcTakenDamage),
                "dpsData.IsNpcData && statisticType == StatisticType.NpcTakenDamage"); // 保证是NPC承伤
            return (scopeTime, statisticType) switch
            {
                (ScopeTime.Current, StatisticType.Damage) => dpsData.TotalAttackDamage,
                (ScopeTime.Current, StatisticType.Healing) => dpsData.TotalHeal,
                (ScopeTime.Current, StatisticType.TakenDamage) => dpsData.TotalTakenDamage,
                (ScopeTime.Current, StatisticType.NpcTakenDamage) => dpsData.IsNpcData ? dpsData.TotalTakenDamage : 0,
                (ScopeTime.Total, StatisticType.Damage) => dpsData.TotalAttackDamage,
                (ScopeTime.Total, StatisticType.Healing) => dpsData.TotalHeal,
                (ScopeTime.Total, StatisticType.TakenDamage) => dpsData.TotalTakenDamage,
                (ScopeTime.Total, StatisticType.NpcTakenDamage) => dpsData.IsNpcData ? dpsData.TotalTakenDamage : 0,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    /// <summary>
    /// 获取每个统计类别的默认筛选器
    /// </summary>
    /// <param name="list"></param>
    /// <param name="type"></param>
    /// <param name="scopeTime"></param>
    /// <returns></returns>
    private static IEnumerable<DpsData> GetDefaultFilter(IEnumerable<DpsData> list, StatisticType type,
        ScopeTime scopeTime)
    {
        return (scopeTime, type) switch
        {
            (ScopeTime.Total, StatisticType.Damage) => list.Where(e => !e.IsNpcData && e.TotalAttackDamage != 0),
            (ScopeTime.Total, StatisticType.Healing) => list.Where(e => !e.IsNpcData && e.TotalHeal != 0),
            (ScopeTime.Total, StatisticType.TakenDamage) => list.Where(e => !e.IsNpcData && e.TotalTakenDamage != 0),
            (ScopeTime.Total, StatisticType.NpcTakenDamage) => list.Where(e => e.IsNpcData && e.TotalTakenDamage != 0),
            _ => list
        };
    }

    private static (long max, long sum) GetMaxSumValueByType(IEnumerable<DpsData> list, StatisticType type,
        ScopeTime scopeTime)
    {
        return type switch
        {
            StatisticType.Damage => (list.Max(e => e.TotalAttackDamage), list.Sum(e => e.TotalAttackDamage)),
            StatisticType.Healing => (list.Max(e => e.TotalHeal), list.Sum(e => e.TotalHeal)),
            StatisticType.TakenDamage or StatisticType.NpcTakenDamage => (list.Max(e => e.TotalTakenDamage),
                list.Sum(e => e.TotalTakenDamage)),
            _ => (long.MaxValue, long.MaxValue)
        };
    }

    private static long GetValueByType(DpsData data, StatisticType type)
    {
        return type switch
        {
            StatisticType.Damage => data.TotalAttackDamage,
            StatisticType.Healing => data.TotalHeal,
            StatisticType.TakenDamage or StatisticType.NpcTakenDamage => data.TotalTakenDamage,
            _ => long.MaxValue
        };
    }


    [RelayCommand]
    public void AddRandomData()
    {
        UpdateData();
    }

    // Test command to manually add an item
    [RelayCommand]
    public void AddTestItem()
    {
        var newItem = new StatisticDataViewModel
        {
            Index = Slots.Count + 1,
            Value = (ulong)_rd.Next(100, 2000),
            Duration = 60000,
            Player = new PlayerInfoViewModel
            {
                Uid = _rd.Next(100, 999),
                Class = Classes.Marksman,
                Guild = "Test Guild",
                Name = $"Test Player {Slots.Count + 1}",
                Spec = ClassSpec.Unknown
            },
            // Add test skill data
            GetSkillList = (player) => new List<SkillItemViewModel>
            {
                new() { SkillName = "Test Skill A", TotalDamage = 15000, HitCount = 25, CritCount = 8, AvgDamage = 600 },
                new() { SkillName = "Test Skill B", TotalDamage = 8500, HitCount = 15, CritCount = 4, AvgDamage = 567 },
                new() { SkillName = "Test Skill C", TotalDamage = 12300, HitCount = 30, CritCount = 12, AvgDamage = 410 }
            }
        };

        // Calculate percentages
        if (Slots.Count > 0)
        {
            var maxValue = Math.Max(Slots.Max(d => d.Value), newItem.Value);
            var totalValue = Slots.Sum(d => Convert.ToDouble(d.Value)) + newItem.Value;

            // Update all existing items
            foreach (var slot in Slots)
            {
                slot.PercentOfMax = maxValue > 0 ? slot.Value / (double)maxValue * 100 : 0;
                slot.Percent = totalValue > 0 ? slot.Value / totalValue : 0;
            }

            // Set new item percentages
            newItem.PercentOfMax = maxValue > 0 ? newItem.Value / (double)maxValue * 100 : 0;
            newItem.Percent = totalValue > 0 ? newItem.Value / totalValue : 0;
        }
        else
        {
            newItem.PercentOfMax = 100;
            newItem.Percent = 1;
        }

        Slots.Add(newItem);
        SortSlotsInPlace();
    }

    [RelayCommand]
    private void NextMetricType()
    {
        StatisticIndex = StatisticIndex.Next();
    }

    [RelayCommand]
    private void PreviousMetricType()
    {
        StatisticIndex = StatisticIndex.Previous();
    }

    [RelayCommand]
    private void ToggleScopeTime()
    {
        ScopeTime = ScopeTime.Next();
    }

    protected void UpdateData()
    {
        DataStorage_DpsDataUpdated();
    }

    /// <summary>
    /// Updates the Index property of items to reflect their current position in the collection
    /// </summary>
    private void UpdateItemIndices()
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            Slots[i].Index = i + 1; // 1-based index
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        // 手动触发一次更新（如果需要）
        throw new NotImplementedException();
    }


    [RelayCommand]
    private void OpenContextMenu()
    {
        ShowContextMenu = true;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _windowManagement.SettingsView.Show();
    }

    [RelayCommand]
    private void Shutdown()
    {
        _appController.Shutdown();
    }

    #region Sort

    /// <summary>
    /// Changes the sort member path and re-sorts the data
    /// </summary>
    [RelayCommand]
    private void SetSortMemberPath(string memberPath)
    {
        if (SortMemberPath == memberPath)
        {
            // Toggle sort direction if the same property is clicked
            SortDirection = SortDirection == SortDirectionEnum.Ascending
                ? SortDirectionEnum.Descending
                : SortDirectionEnum.Ascending;
        }
        else
        {
            SortMemberPath = memberPath;
            SortDirection = SortDirectionEnum.Descending; // Default to descending for new properties
        }

        // Trigger immediate re-sort
        SortSlotsInPlace();
    }

    /// <summary>
    /// Manually triggers a sort operation
    /// </summary>
    [RelayCommand]
    private void ManualSort()
    {
        SortSlotsInPlace();
    }

    /// <summary>
    /// Sorts by Value in descending order (highest DPS first)
    /// </summary>
    [RelayCommand]
    private void SortByValue()
    {
        SetSortMemberPath("Value");
    }

    /// <summary>
    /// Sorts by Name in ascending order
    /// </summary>
    [RelayCommand]
    private void SortByName()
    {
        SortMemberPath = "Name";
        SortDirection = SortDirectionEnum.Ascending;
        SortSlotsInPlace();
    }

    /// <summary>
    /// Sorts by Classes
    /// </summary>
    [RelayCommand]
    private void SortByClass()
    {
        SetSortMemberPath("Classes");
    }

    /// <summary>
    /// Sorts the slots collection in-place based on the current sort criteria
    /// </summary>
    private void SortSlotsInPlace()
    {
        if (Slots.Count == 0 || string.IsNullOrWhiteSpace(SortMemberPath))
            return;

        try
        {
            // Sort the collection based on the current criteria
            switch (SortMemberPath)
            {
                case "Value":
                    Slots.SortBy(x => x.Value, SortDirection == SortDirectionEnum.Descending);
                    break;
                case "Name":
                    Slots.SortBy(x => x.Player.Name, SortDirection == SortDirectionEnum.Descending);
                    break;
                case "Classes":
                    Slots.SortBy(x => (int)x.Player.Class, SortDirection == SortDirectionEnum.Descending);
                    break;
                case "PercentOfMax":
                    Slots.SortBy(x => x.PercentOfMax, SortDirection == SortDirectionEnum.Descending);
                    break;
                case "Percent":
                    Slots.SortBy(x => x.Percent, SortDirection == SortDirectionEnum.Descending);
                    break;
            }

            // Update the Id property to reflect the new order (1-based index)
            UpdateItemIndices();
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Error during sorting: {ex.Message}");
        }
    }

    #endregion
}

public sealed class DpsStatisticsDesignTimeViewModel()
    : DpsStatisticsViewModel(null!, null!, new InstantizedDataStorage(), null!, null!, null!)
{
    [Conditional("DEBUG")]
    private void InitDemoProgressBars()
    {
        // 2) 造几位玩家（随便举例，图标请换成你项目里存在的）
        var players = new[]
        {
            ("惊奇猫猫盒-狼弓(23207)", Classes.Marksman),
            ("无双重剑-测试(19876)", Classes.ShieldKnight),
            ("奥术回响-测试(20111)", Classes.FrostMage),
            ("圣光之约-测试(18770)", Classes.VerdantOracle),
            ("影袭-测试(20990)", Classes.Stormblade),
            ("Jojo-未知(20990)", Classes.Unknown)
        };

        Slots.BeginUpdate();
        for (var i = 0; i < players.Length; i++)
        {
            var (nick, @class) = players[i];
            var barData = new StatisticDataViewModel
            {
                Index = i + 1, // 1-based index
                Player = new PlayerInfoViewModel
                {
                    Uid = i + 1,
                    Class = @class,
                    Name = nick
                }
            };
            Slots.Add(barData);
        }

        UpdateData();
        Slots.EndUpdate();
    }
}