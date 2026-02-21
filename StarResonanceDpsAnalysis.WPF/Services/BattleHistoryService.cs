using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Config;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

public record HistoryInfo(string Title, string FilePath)
{
    public static HistoryInfo FromHistory(BattleHistoryData d)
    {
        return new HistoryInfo($"{d.StartedAt:HH:mm:ss} ({d.Duration:mm\\:ss}", d.FilePath);
    }
}

/// <summary>
/// 战斗快照服务 - 负责保存和加载战斗快照
/// </summary>
public class BattleHistoryService
{
    private const int AbsoluteMinDurationSeconds = 10; // 绝对最小战斗时长(秒),低于此值的战斗永远不保存
    private readonly IConfigManager _configManager;
    private readonly ILogger<BattleHistoryService> _logger;
    private readonly string _historyDirectory;

    public BattleHistoryService(ILogger<BattleHistoryService> logger, IConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        _historyDirectory = Path.Combine(Environment.CurrentDirectory, "BattleHistory");

        // 确保目录存在
        if (!Directory.Exists(_historyDirectory))
        {
            Directory.CreateDirectory(_historyDirectory);
        }

        // 启动时加载现有快照
        LoadHistory();
    }

    private int MaxHistorys => _configManager.CurrentConfig.MaxHistoryCount;

    /// <summary>
    /// 当前战斗快照列表(最新的N条，N由配置决定)
    /// </summary>
    public List<HistoryInfo> CurrentHistory { get; } = new();

    /// <summary>
    /// 全程快照列表(最新的N条，N由配置决定)
    /// </summary>
    public List<HistoryInfo> TotalHistorys { get; } = new();

    /// <summary>
    /// 保存当前战斗快照
    /// </summary>
    /// <param name="storage">数据存储</param>
    /// <param name="duration">战斗时长</param>
    /// <param name="minDurationSeconds">用户设置的最小时长(秒),0表示记录所有(默认记录所有)</param>
    /// <param name="forceUseFullData">强制使用FullDpsData(用于脱战时sectioned数据已被清空的情况)</param>
    public void SaveCurrentHistory(IDataStorage storage, TimeSpan duration, int minDurationSeconds = 0,
        bool forceUseFullData = false)
    {
        // ⭐ 硬性限制: 低于10秒的战斗永远不保存
        if (duration.TotalSeconds < AbsoluteMinDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足{Min}秒({Actual:F1}秒),跳过保存当前快照(硬性限制)",
                AbsoluteMinDurationSeconds, duration.TotalSeconds);
            return;
        }

        // ⭐ 用户设置的过滤条件(可选)
        if (minDurationSeconds > 0 && duration.TotalSeconds < minDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足用户设置的{UserMin}秒({Actual:F1}秒),跳过保存当前快照(用户设置)",
                minDurationSeconds, duration.TotalSeconds);
            return;
        }

        try
        {
            // ⭐ 关键修复: 如果forceUseFullData=true,则使用FullDpsData创建快照
            var scope = forceUseFullData ? ScopeTime.Total : ScopeTime.Current;
            var History = CreateHistory(storage, duration, scope);

            // 保存到磁盘
            SaveHistoryToDisk(History);

            // 添加到内存列表(插入到开头)
            CurrentHistory.Insert(0, HistoryInfo.FromHistory(History));

            // ⭐ 只保留最新的8条,超出的释放内存并删除磁盘文件
            while (CurrentHistory.Count > MaxHistorys)
            {
                var oldest = CurrentHistory[CurrentHistory.Count - 1];
                CurrentHistory.RemoveAt(CurrentHistory.Count - 1);

                // 删除对应的磁盘文件
                TryDeleteHistoryFile(oldest.FilePath);

                _logger.LogDebug("移除旧快照: {Time}, 文件已删除", oldest.FilePath);
            }

            _logger.LogInformation("保存当前战斗快照成功: {Time}, 时长: {Duration:F1}秒, 数据源: {Source}, 当前保存数量: {Count}/{Max}",
                History.StartedAt, duration.TotalSeconds, forceUseFullData ? "FullData" : "SectionedData",
                CurrentHistory.Count, MaxHistorys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存当前战斗快照失败");
        }
    }

    /// <summary>
    /// 保存全程快照
    /// </summary>
    /// <param name="storage">数据存储</param>
    /// <param name="duration">战斗时长</param>
    /// <param name="minDurationSeconds">用户设置的最小时长(秒),0表示记录所有(默认记录所有)</param>
    public void SaveTotalHistory(IDataStorage storage, TimeSpan duration, int minDurationSeconds = 0)
    {
        // ? 硬性限制: 低于10秒的战斗永远不保存
        if (duration.TotalSeconds < AbsoluteMinDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足{Min}秒({Actual:F1}秒),跳过保存全程快照(硬性限制)",
                AbsoluteMinDurationSeconds, duration.TotalSeconds);
            return;
        }

        // ? 用户设置的过滤条件(可选)
        if (minDurationSeconds > 0 && duration.TotalSeconds < minDurationSeconds)
        {
            _logger.LogInformation("战斗时长不足用户设置的{UserMin}秒({Actual:F1}秒),跳过保存全程快照(用户设置)",
                minDurationSeconds, duration.TotalSeconds);
            return;
        }

        try
        {
            var History = CreateHistory(storage, duration, ScopeTime.Current);

            // 保存到磁盘
            SaveHistoryToDisk(History);

            // 添加到内存列表(插入到开头)
            TotalHistorys.Insert(0, HistoryInfo.FromHistory(History));

            // ? 只保留最新的8条,超出的释放内存并删除磁盘文件
            while (TotalHistorys.Count > MaxHistorys)
            {
                var oldest = TotalHistorys[TotalHistorys.Count - 1];
                TotalHistorys.RemoveAt(TotalHistorys.Count - 1);

                // 删除对应的磁盘文件
                TryDeleteHistoryFile(oldest.FilePath);

                _logger.LogDebug("移除旧快照: {Time}, 文件已删除", oldest.FilePath);
            }

            _logger.LogInformation("保存全程快照成功: {Time}, 时长: {Duration:F1}秒, 当前保存数量: {Count}/{Max}",
                History.StartedAt, duration.TotalSeconds, TotalHistorys.Count, MaxHistorys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存全程快照失败");
        }
    }

    public BattleHistoryData? LoadHistory(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("快照文件不存在: {File}", filePath);
                return null;
            }

            var json = File.ReadAllText(filePath);
            //var History = JsonSerializer.Deserialize<BattleHistoryData>(json);
            var History = JsonConvert.DeserializeObject<BattleHistoryData>(json);

            if (History != null)
            {
                History.FilePath = filePath;
                _logger.LogDebug("成功加载快照: {File}", filePath);
            }
            else
            {
                _logger.LogWarning("反序列化快照失败: {File}", filePath);
            }

            return History;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载快照失败: {File}", filePath);
            return null;
        }
    }

    /// <summary>
    /// 创建快照
    /// </summary>
    private BattleHistoryData CreateHistory(IDataStorage storage, TimeSpan duration, ScopeTime scopeType)
    {
        var now = DateTime.Now;
        var players = new Dictionary<long, PlayerInfo>();
        var statistics = new Dictionary<long, PlayerStatistics>();

        // 根据类型选择数据源
        var dpsList = storage.GetStatistics(scopeType == ScopeTime.Total);

        ulong teamTotalDamage = 0;
        ulong teamTotalHealing = 0;
        ulong teamTotalTaken = 0;

        foreach (var dpsData in dpsList.Values)
        {

            var damage = (ulong)Math.Max(0, dpsData.AttackDamage.Total);
            var healing = (ulong)Math.Max(0, dpsData.Healing.Total);
            var taken = (ulong)Math.Max(0, dpsData.TakenDamage.Total);

            teamTotalDamage += damage;
            teamTotalHealing += healing;
            teamTotalTaken += taken;

            var foundPlayerInfo = storage.ReadOnlyPlayerInfoDatas.TryGetValue(dpsData.Uid, out var playerInfo);
            players[dpsData.Uid] = foundPlayerInfo ? playerInfo! : new PlayerInfo() { UID = dpsData.Uid };
            statistics[dpsData.Uid] = dpsData;
        }

        return new BattleHistoryData
        {
            ScopeType = scopeType,
            StartedAt = now.AddTicks(-duration.Ticks),
            EndedAt = now,
            Duration = duration,
            TeamTotalDamage = teamTotalDamage,
            TeamTotalHealing = teamTotalHealing,
            TeamTotalTakenDamage = teamTotalTaken,
            Players = players,
            Statistics = statistics
        };
    }

    /// <summary>
    /// 保存快照到磁盘
    /// </summary>
    private void SaveHistoryToDisk(BattleHistoryData history)
    {
        var fileName = $"{history.ScopeType}_{history.StartedAt:yyyy-MM-dd_HH-mm-ss}.json";
        var filePath = Path.Combine(_historyDirectory, fileName);

        var json = JsonConvert.SerializeObject(history);

        File.WriteAllText(filePath, json);
        history.FilePath = filePath;
    }

    /// <summary>
    /// 从磁盘加载快照
    /// </summary>
    private void LoadHistory()
    {
        try
        {
            if (!Directory.Exists(_historyDirectory))
            {
                return;
            }

            var files = Directory.GetFiles(_historyDirectory, "*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    //var History = JsonSerializer.Deserialize<BattleHistoryData>(json);
                    var settings = new JsonSerializerSettings( )
                    {
                        ContractResolver = new PrivateSetterContractResolver()
                    };
                    var History = JsonConvert.DeserializeObject<BattleHistoryData>(json, settings);

                    if (History == null) continue;

                    History.FilePath = file;

                    if (History.ScopeType == ScopeTime.Current)
                    {
                        if (CurrentHistory.Count < MaxHistorys)
                        {
                            CurrentHistory.Add(HistoryInfo.FromHistory(History));
                        }
                        else
                        {
                            // ? 超出限制,删除文件并释放内存
                            File.Delete(file);
                            _logger.LogDebug("启动时删除超出限制的旧快照文件: {File}", file);
                        }
                    }
                    else
                    {
                        if (TotalHistorys.Count < MaxHistorys)
                        {
                            TotalHistorys.Add(HistoryInfo.FromHistory(History));
                        }
                        else
                        {
                            // ? 超出限制,删除文件并释放内存
                            File.Delete(file);
                            _logger.LogDebug("启动时删除超出限制的旧快照文件: {File}", file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "加载快照文件失败: {File}", file);
                    // 损坏的文件直接删除
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            _logger.LogInformation("加载快照完成: 当前={Current}/{MaxCurrent}, 全程={Total}/{MaxTotal}",
                CurrentHistory.Count, MaxHistorys, TotalHistorys.Count, MaxHistorys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载快照失败");
        }
    }

    /// <summary>
    /// 尝试删除快照文件
    /// </summary>
    private void TryDeleteHistoryFile(string filePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("成功删除快照文件: {File}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除快照文件失败: {File}", filePath);
        }
    }
}

/// <summary>
/// 快照数据模型
/// </summary>
public class BattleHistoryData
{
    public ScopeTime ScopeType { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public ulong TeamTotalDamage { get; set; }
    public ulong TeamTotalHealing { get; set; }
    public ulong TeamTotalTakenDamage { get; set; }
    public Dictionary<long, PlayerInfo> Players { get; set; } = new();
    public Dictionary<long, PlayerStatistics> Statistics { get; set; } = new();

    /// <summary>
    /// 文件路径(不序列化)
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string FilePath { get; set; } = "";

    /// <summary>
    /// 显示标签
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayLabel =>
        $"{(ScopeType == ScopeTime.Current ? "当前" : "全程")} {StartedAt:HH:mm:ss} ({Duration:mm\\:ss})";

     public static explicit operator HistoryInfo(BattleHistoryData d) => HistoryInfo.FromHistory(d);
}

public class PrivateSetterContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);
        
        if (!property.Writable)
        {
            var propertyInfo = member as PropertyInfo;
            if (propertyInfo?.GetSetMethod(true) != null)
            {
                property.Writable = true;
            }
        }
        
        return property;
    }
}
