using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Extensions;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

/// <summary>
/// Helper struct for pre-processed DPS data to avoid redundant calculations
/// Immutable by design for thread-safety and performance
/// </summary>
public readonly record struct DpsDataProcessed(
    PlayerStatistics OriginalData,
    ulong Value,
    long DurationTicks,
    List<SkillItemViewModel> DamageSkillList,
    List<SkillItemViewModel> HealSkillList,
    List<SkillItemViewModel> TakenDamageSkillList,
    long Uid);

public partial class DpsStatisticsSubViewModel : BaseViewModel
{
    private readonly DebugFunctions _debugFunctions;
    private readonly Dispatcher _dispatcher;
    private readonly LocalizationManager _localizationManager;
    private readonly ILogger<DpsStatisticsViewModel> _logger;
    private readonly DpsStatisticsViewModel _parent;
    private readonly IDataStorage _storage;
    private readonly StatisticType _type;
    [ObservableProperty] private StatisticDataViewModel? _currentPlayerSlot;
    [ObservableProperty] private BulkObservableCollection<StatisticDataViewModel> _data = new();
    [ObservableProperty] private ScopeTime _scopeTime;
    [ObservableProperty] private StatisticDataViewModel? _selectedSlot;
    [ObservableProperty] private int _skillDisplayLimit = 8;
    [ObservableProperty] private SortDirectionEnum _sortDirection = SortDirectionEnum.Descending;
    [ObservableProperty] private string _sortMemberPath = "Value";
    [ObservableProperty] private bool _suppressSorting;
    [ObservableProperty] private int? _currentPlayerRank = null;

    public DpsStatisticsSubViewModel(ILogger<DpsStatisticsViewModel> logger, Dispatcher dispatcher, StatisticType type,
        IDataStorage storage,
        DebugFunctions debugFunctions, DpsStatisticsViewModel parent, LocalizationManager localizationManager)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _type = type;
        _storage = storage;
        _debugFunctions = debugFunctions;
        _parent = parent;
        _localizationManager = localizationManager;
        _data.CollectionChanged += DataChanged;
        return;

        void DataChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    Debug.Assert(e.NewItems != null, "e.NewItems != null");
                    LocalIterate(e.NewItems, item => DataDictionary[item.Player.Uid] = item);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    Debug.Assert(e.OldItems != null, "e.OldItems != null");
                    LocalIterate(e.OldItems, itm => DataDictionary.Remove(itm.Player.Uid));
                    LocalIterate(e.OldItems, itm =>
                    {
                        if (ReferenceEquals(CurrentPlayerSlot, itm))
                        {
                            CurrentPlayerSlot = null;
                        }
                    });
                    break;
                case NotifyCollectionChangedAction.Replace:
                    Debug.Assert(e.NewItems != null, "e.NewItems != null");
                    LocalIterate(e.NewItems, item => DataDictionary[item.Player.Uid] = item);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    DataDictionary.Clear();
                    CurrentPlayerSlot = null;
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

    public Dictionary<long, StatisticDataViewModel> DataDictionary { get; } = new();
    public bool Initialized { get; set; }

    public void SetPlayerInfoMask(bool mask)
    {
        foreach (var value in DataDictionary.Values)
        {
            value.Player.Mask = mask;
        }
    }

    /// <summary>
    /// Sorts the slots collection in-place based on the current sort criteria
    /// </summary>
    public void SortSlotsInPlace(bool force = false)
    {
        if (Data.Count == 0 || string.IsNullOrWhiteSpace(SortMemberPath))
            return;

        if (!force && SuppressSorting)
        {
            UpdateItemIndices();
            return;
        }

        try
        {
            // Sort the collection based on the current criteria
            _dispatcher.Invoke(() =>
            {
                switch (SortMemberPath)
                {
                    case "Value":
                        Data.SortBy(x => x.Value, SortDirection == SortDirectionEnum.Descending);
                        break;
                    case "Name":
                        Data.SortBy(x => x.Player.PlayerInfo, SortDirection == SortDirectionEnum.Descending);
                        break;
                    case "Classes":
                        Data.SortBy(x => (int)x.Player.Class, SortDirection == SortDirectionEnum.Descending);
                        break;
                    case "PercentOfMax":
                        Data.SortBy(x => x.PercentOfMax, SortDirection == SortDirectionEnum.Descending);
                        break;
                    case "Percent":
                        Data.SortBy(x => x.Percent, SortDirection == SortDirectionEnum.Descending);
                        break;
                }
            });
            // Update the Index property to reflect the new order (1-based index)
            UpdateItemIndices();
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Error during sorting: {ex.Message}");
        }
    }

    protected StatisticDataViewModel GetOrAddStatisticDataViewModel(DpsData dpsData)
    {
        if (DataDictionary.TryGetValue(dpsData.UID, out var slot)) return slot;

        // Debug.Assert(playerInfo != null, nameof(playerInfo) + " != null");
        var ret = _storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.UID, out var playerInfo);
        slot = new StatisticDataViewModel(_debugFunctions, _localizationManager)
        {
            Index = 999,
            Value = 0,
            DurationTicks = dpsData.LastLoggedTick - (dpsData.StartLoggedTick ?? 0),
            Player = new PlayerInfoViewModel(_localizationManager)
            {
                Uid = dpsData.UID,
                Guild = "Unknown",
                Name = playerInfo?.Name,
                Spec = playerInfo?.Spec ?? ClassSpec.Unknown,
                IsNpc = dpsData.IsNpcData,
                NpcTemplateId = playerInfo?.NpcTemplateId ?? 0,
                Mask = _parent.AppConfig.MaskPlayerName
            },
            // Set the hover action to call parent's SetIndicatorHover
            SetHoverStateAction = isHovering => _parent.SetIndicatorHover(isHovering)
        };

        _dispatcher.Invoke(() => { Data.Add(slot); });

        return slot;
    }

    /// <summary>
    /// ? NEW: Get or add StatisticDataViewModel from PlayerStatistics (new architecture)
    /// </summary>
    protected StatisticDataViewModel GetOrAddStatisticDataViewModel(PlayerStatistics playerStats)
    {
        if (DataDictionary.TryGetValue(playerStats.Uid, out var slot))
            return slot;

        var ret = _storage.ReadOnlyPlayerInfoDatas.TryGetValue(playerStats.Uid, out var playerInfo);
        slot = new StatisticDataViewModel(_debugFunctions, _localizationManager)
        {
            Index = 999,
            Value = 0,
            DurationTicks = playerStats.LastTick - (playerStats.StartTick ?? 0),
            Player = new PlayerInfoViewModel(_localizationManager)
            {
                Uid = playerStats.Uid,
                Guild = "Unknown",
                Name = playerInfo?.Name,
                Spec = playerInfo?.Spec ?? ClassSpec.Unknown,
                IsNpc = playerStats.IsNpc,
                NpcTemplateId = playerInfo?.NpcTemplateId ?? 0,
                Mask = _parent.AppConfig.MaskPlayerName
            },
            SetHoverStateAction = isHovering => _parent.SetIndicatorHover(isHovering)
        };

        _dispatcher.Invoke(() => { Data.Add(slot); });

        return slot;
    }

    private void UpdatePlayerInfo(StatisticDataViewModel slot, PlayerInfo? playerInfo)
    {
        if (playerInfo != null)
        {
            Debug.Assert(playerInfo != null, nameof(playerInfo) + " != null");
            slot.Player.Name = playerInfo.Name;
            slot.Player.Class = playerInfo.Class;
            slot.Player.Spec = playerInfo.Spec;
            slot.Player.PowerLevel = playerInfo.CombatPower ?? 0;
            slot.Player.SeasonLevel = playerInfo.SeasonLevel;
            slot.Player.SeasonStrength = playerInfo.SeasonStrength;
        }
        else
        {
            slot.Player.Name = null;
            slot.Player.Class = Classes.Unknown;
            slot.Player.Spec = ClassSpec.Unknown;
            slot.Player.PowerLevel = 0;
            slot.Player.SeasonLevel = 0;
            slot.Player.SeasonStrength = 0;
        }
    }

    /// <summary>
    /// ⭐ 更新当前玩家排名(使用用户在设置中配置的UID)
    /// </summary>
    /// <param name="currentPlayerUid"></param>
    private void UpdateCurrentPlayerRank(long currentPlayerUid)
    {
        var found = false;
        int i;
        for (i = 0; i < Data.Count; i++)
        {
            if (Data[i].Player.Uid != currentPlayerUid) continue;
            found = true;
            break;
        }

        if (found)
            CurrentPlayerRank = i + 1;
        else
            CurrentPlayerRank = null;

        // ⭐ 调试日志
        _logger.LogDebug(
            "排名更新: UserUID={UserUid}, Rank={Rank}, Total={Total}, Type={Type}",
            currentPlayerUid,
            CurrentPlayerRank ?? -1,
            Data.Count,
            _type);
    }
    /// <summary>
    /// Updates data with pre-computed values for efficient batch processing
    /// </summary>
    internal void UpdateDataOptimized(Dictionary<long, DpsDataProcessed> processedData, long currentPlayerUid)
    {
        var hasCurrentPlayer = currentPlayerUid != 0;

        // Update all slots with pre-processed data
        foreach (var (uid, processed) in processedData)
        {
            // Skip if this statistic type has no value
            if (processed.Value == 0)
                continue;

            var slot = GetOrAddStatisticDataViewModel(processed.OriginalData);

            // Update player info
            var ret = _storage.ReadOnlyPlayerInfoDatas.TryGetValue(processed.Uid, out var playerInfo);
            if (!ret) continue;
            UpdatePlayerInfo(slot, playerInfo);

            // Update slot values with pre-computed data
            slot.Value = processed.Value;
            slot.DurationTicks = processed.DurationTicks;

            slot.Damage.TotalSkillList = processed.DamageSkillList;
            slot.Damage.RefreshFilteredList(SkillDisplayLimit);

            slot.Heal.TotalSkillList = processed.HealSkillList;
            slot.Heal.RefreshFilteredList(SkillDisplayLimit);

            slot.TakenDamage.TotalSkillList = processed.TakenDamageSkillList;
            slot.TakenDamage.RefreshFilteredList(SkillDisplayLimit);

            // Set current player slot if this is the current player
            if (hasCurrentPlayer && uid == currentPlayerUid)
            {
                SelectedSlot = slot;
                CurrentPlayerSlot = slot;
            }
        }

        // Batch calculate percentages
        if (Data.Count > 0)
        {
            var maxValue = Data.Max(d => d.Value);
            var totalValue = Data.Sum(d => Convert.ToDouble(d.Value));

            var hasMaxValue = maxValue > 0;
            var hasTotalValue = totalValue > 0;

            foreach (var slot in Data)
            {
                slot.PercentOfMax = hasMaxValue ? slot.Value / (double)maxValue * 100 : 0;
                slot.Percent = hasTotalValue ? slot.Value / totalValue : 0;
            }
        }

        // Sort data in place 
        SortSlotsInPlace();
        UpdateCurrentPlayerRank(currentPlayerUid);
    }

    private ulong GetValueForType(DpsData dpsData)
    {
        return _type switch
        {
            StatisticType.Damage => dpsData.TotalAttackDamage.ConvertToUnsigned(),
            StatisticType.Healing => dpsData.TotalHeal.ConvertToUnsigned(),
            StatisticType.TakenDamage => dpsData.TotalTakenDamage.ConvertToUnsigned(),
            StatisticType.NpcTakenDamage => dpsData.IsNpcData ? dpsData.TotalTakenDamage.ConvertToUnsigned() : 0UL,
            _ => throw new ArgumentOutOfRangeException(nameof(_type), _type, "Invalid statistic type")
        };
    }

    /// <summary>
    /// Updates the Index property of items to reflect their current position in the collection
    /// </summary>
    private void UpdateItemIndices()
    {
        var data = Data;
        for (var i = 0; i < data.Count; i++)
        {
            data[i].Index = i + 1; // 1-based index
        }
    }

    public void AddTestItem()
    {
        var slots = Data;
        var newItem = new StatisticDataViewModel(_debugFunctions, _localizationManager)
        {
            Index = slots.Count + 1,
            Value = (ulong)Random.Shared.Next(100, 2000),
            DurationTicks = 60000,
            Player = new PlayerInfoViewModel(LocalizationManager.Instance)
            {
                Uid = Random.Shared.Next(100, 999),
                Class = RandomClass(),
                Guild = "Test Guild",
                Name = $"Test Player {slots.Count + 1}",
                Spec = ClassSpecHelper.Random(),
                PowerLevel = Random.Shared.Next(5000, 39000)
            }
        };
        newItem.Damage.FilteredSkillList =
        [
            new SkillItemViewModel
            {
                SkillName = "Test Skill A",
                Damage = new SkillItemViewModel.SkillValue { TotalValue = 15000, HitCount = 25, CritCount = 8, Average = 600 } },
            new SkillItemViewModel
            {
                SkillName = "Test Skill B",
                Damage = new SkillItemViewModel.SkillValue { TotalValue = 8500, HitCount = 15, CritCount = 4, Average = 567 }
            },
            new SkillItemViewModel
            {
                SkillName = "Test Skill C",
                Damage = new SkillItemViewModel.SkillValue { TotalValue = 12300, HitCount = 30, CritCount = 12, Average = 410 }
            }
        ];
        newItem.Heal.FilteredSkillList =
        [
            new SkillItemViewModel
            {
                SkillName = "Test Heal Skill A", Heal = new() { TotalValue = 15000, HitCount = 25, CritCount = 8, Average = 600 }
            },
            new SkillItemViewModel
            {
                SkillName = "Test Heal Skill B", Heal = new() { TotalValue = 8500, HitCount = 15, CritCount = 4, Average = 567 }
            },
            new SkillItemViewModel
            {
                SkillName = "Test Heal Skill C",Heal = new() { TotalValue = 12300, HitCount = 30, CritCount = 12, Average = 410 }
            }
        ];
        newItem.TakenDamage.FilteredSkillList =
        [
            new SkillItemViewModel
            {
                SkillName = "Test Taken Skill A", TakenDamage = new() { TotalValue = 15000, HitCount = 25, CritCount = 8, Average = 600 }
            },
            new SkillItemViewModel
            {
                SkillName = "Test Taken Skill B", TakenDamage =new() { TotalValue = 8500, HitCount = 15, CritCount = 4, Average = 567 }
            },
            new SkillItemViewModel
            {
                SkillName = "Test Taken Skill C", TakenDamage = new() { TotalValue = 12300, HitCount = 30, CritCount = 12, Average = 410 }
            }
        ];

        // Calculate percentages
        if (slots.Count > 0)
        {
            var maxValue = Math.Max(slots.Max(d => d.Value), newItem.Value);
            var totalValue = slots.Sum(d => Convert.ToDouble(d.Value)) + newItem.Value;

            // Update all existing items
            foreach (var slot in slots)
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

        slots.Add(newItem);
        SortSlotsInPlace();
    }

    private Classes RandomClass()
    {
        var values = Enum.GetValues(typeof(Classes));
        return (Classes)values.GetValue(Random.Shared.Next(values.Length))!;
    }

    public void Reset()
    {
        // Ensure collection modifications happen on the UI thread
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Reset);
            return;
        }

        // Clear items (will also clear DataDictionary via CollectionChanged Reset handler)
        Data.Clear();
        SelectedSlot = null;
        CurrentPlayerSlot = null;
    }

    public void RefreshSkillDisplayLimit()
    {
        foreach (var vm in Data)
        {
            vm.RefreshSkillLists(SkillDisplayLimit);
        }
    }

    partial void OnSkillDisplayLimitChanged(int value)
    {
        RefreshSkillDisplayLimit();
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
        SortSlotsInPlace(true);
    }

    /// <summary>
    /// Manually triggers a sort operation
    /// </summary>
    [RelayCommand]
    private void ManualSort()
    {
        SortSlotsInPlace(true);
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
        SortSlotsInPlace(true);
    }

    /// <summary>
    /// Sorts by Classes
    /// </summary>
    [RelayCommand]
    private void SortByClass()
    {
        SetSortMemberPath("Classes");
    }

    #endregion
}