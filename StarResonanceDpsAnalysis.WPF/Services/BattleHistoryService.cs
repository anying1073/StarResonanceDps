using System.Collections.ObjectModel;
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
        return new HistoryInfo($"{d.StartedAt:HH:mm:ss} ({d.Duration:mm\\:ss})", d.FilePath);
    }
}

/// <summary>
/// 战斗历史服务 - 负责保存和加载战斗历史
/// </summary>
public partial class BattleHistoryService
{
    private const int AbsoluteMinDurationSeconds = 10; // 绝对最小战斗时长(秒),低于此值的战斗永远不保存
    private readonly IConfigManager _configManager;
    private readonly IDataStorage _storage;
    private readonly ILogger<BattleHistoryService> _logger;
    private readonly string _historyDirectory;
    private readonly JsonSerializerSettings _jsonSettings;

    public BattleHistoryService(ILogger<BattleHistoryService> logger, IConfigManager configManager, IDataStorage storage)
    {
        _logger = logger;
        _configManager = configManager;
        _storage = storage;
        _historyDirectory = Path.Combine(Environment.CurrentDirectory, "BattleHistory");

        _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new PrivateSetterContractResolver()
        };

        // 确保目录存在
        if (!Directory.Exists(_historyDirectory))
        {
            Directory.CreateDirectory(_historyDirectory);
        }

        // 启动时加载现有历史
        LoadHistory();
    }

    private int MaxHistory => _configManager.CurrentConfig.MaxHistoryCount;

    /// <summary>
    /// 当前战斗历史列表(最新的N条，N由配置决定)
    /// </summary>
#pragma warning disable IDE0028
    public ObservableCollection<HistoryInfo> ScopeCurrentHistory { get; } = new();

    /// <summary>
    /// 全程战斗历史列表(最新的N条，N由配置决定)
    /// </summary>
    public ObservableCollection<HistoryInfo> ScopeTotalHistory { get; } = new();
#pragma warning restore IDE0028

    /// <summary>
    /// 保存当前战斗历史
    /// </summary>
    /// <param name="duration">战斗时长</param>
    /// <param name="minDurationSeconds">用户设置的最小时长(秒),0表示记录所有(默认记录所有)</param>
    /// <param name="forceUseFullData">强制使用FullDpsData(用于脱战时sectioned数据已被清空的情况)</param>
    public void SaveScopeCurrentHistory(TimeSpan duration, int minDurationSeconds = 0,
        bool forceUseFullData = false)
    {
        // ⭐ 硬性限制: 低于10秒的战斗永远不保存
        if (duration.TotalSeconds < AbsoluteMinDurationSeconds)
        {
            LogSkipHistoryHardLimit(AbsoluteMinDurationSeconds, duration.TotalSeconds, "当前");
            return;
        }

        // ⭐ 用户设置的过滤条件(可选)
        if (minDurationSeconds > 0 && duration.TotalSeconds < minDurationSeconds)
        {
            LogSkipHistoryUserLimit(minDurationSeconds, duration.TotalSeconds, "当前");
            return;
        }

        try
        {
            var scope = forceUseFullData ? ScopeTime.Total : ScopeTime.Current;
            var history = CreateHistory(_storage, duration, scope);

            // 保存到磁盘
            SaveHistoryToDisk(history);

            // 添加到内存列表(插入到开头)
            ScopeCurrentHistory.Insert(0, HistoryInfo.FromHistory(history));

            // ⭐ 只保留最新的8条,超出的释放内存并删除磁盘文件
            while (ScopeCurrentHistory.Count > MaxHistory)
            {
                var oldest = ScopeCurrentHistory[^1];
                ScopeCurrentHistory.RemoveAt(ScopeCurrentHistory.Count - 1);

                // 删除对应的磁盘文件
                TryDeleteHistoryFile(oldest.FilePath);

                LogRemoveOldHistory(oldest.FilePath);
            }

            LogSaveCurrentHistorySuccess(history.StartedAt, duration.TotalSeconds, forceUseFullData ? "FullData" : "SectionedData",
                ScopeCurrentHistory.Count, MaxHistory);
        }
        catch (Exception ex)
        {
            LogSaveCurrentHistoryError(ex);
        }
    }

    /// <summary>
    /// 保存全程历史
    /// </summary>
    /// <param name="duration">战斗时长</param>
    /// <param name="minDurationSeconds">用户设置的最小时长(秒),0表示记录所有(默认记录所有)</param>
    public void SaveScopeTotalHistory(TimeSpan duration, int minDurationSeconds = 0)
    {
        // ? 硬性限制: 低于10秒的战斗永远不保存
        if (duration.TotalSeconds < AbsoluteMinDurationSeconds)
        {
            LogSkipHistoryHardLimit(AbsoluteMinDurationSeconds, duration.TotalSeconds, "全程");
            return;
        }

        // ? 用户设置的过滤条件(可选)
        if (minDurationSeconds > 0 && duration.TotalSeconds < minDurationSeconds)
        {
            LogSkipHistoryUserLimit(minDurationSeconds, duration.TotalSeconds, "全程");
            return;
        }

        try
        {
            var history = CreateHistory(_storage, duration, ScopeTime.Total);

            // 保存到磁盘
            SaveHistoryToDisk(history);

            // 添加到内存列表(插入到开头)
            ScopeTotalHistory.Insert(0, HistoryInfo.FromHistory(history));

            // 超出的释放内存并删除磁盘文件
            while (ScopeTotalHistory.Count > MaxHistory)
            {
                var oldest = ScopeTotalHistory[^1];
                ScopeTotalHistory.RemoveAt(ScopeTotalHistory.Count - 1);

                // 删除对应的磁盘文件
                TryDeleteHistoryFile(oldest.FilePath);

                LogRemoveOldHistory(oldest.FilePath);
            }

            LogSaveTotalHistorySuccess(history.StartedAt, duration.TotalSeconds, ScopeTotalHistory.Count, MaxHistory);
        }
        catch (Exception ex)
        {
            LogSaveTotalHistoryError(ex);
        }
    }

    public BattleHistoryData? LoadHistory(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                LogHistoryFileNotFound(filePath);
                return null;
            }

            BattleHistoryData? history;
            using (var stream = File.OpenText(filePath))
            using (var reader = new JsonTextReader(stream))
            {
                var serializer = JsonSerializer.Create(_jsonSettings);
                history = serializer.Deserialize<BattleHistoryData>(reader);
            }

            if (history != null)
            {
                history.FilePath = filePath;

                LogHistoryLoadedSuccess(filePath, history.Duration);
            }
            else
            {
                LogHistoryDeserializationFailed(filePath);
            }

            return history;
        }
        catch (Exception ex)
        {
            LogLoadHistoryFileError(ex, filePath);
            return null;
        }
    }

    /// <summary>
    /// 创建历史
    /// </summary>
    private static BattleHistoryData CreateHistory(IDataStorage storage, TimeSpan duration, ScopeTime scopeType)
    {
        var now = DateTime.Now;
        
        // 根据类型选择数据源
        var dpsList = storage.GetStatistics(scopeType == ScopeTime.Total);
        var capacity = dpsList.Count;

        var players = new Dictionary<long, PlayerInfo>(capacity);
        var statistics = new Dictionary<long, PlayerStatistics>(capacity);

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
            players[dpsData.Uid] = foundPlayerInfo ? playerInfo! : new PlayerInfo { UID = dpsData.Uid };

            // Save a detached snapshot, not the live storage instance
            statistics[dpsData.Uid] = PlayerStatisticsSnapshotCloner.Clone(dpsData);
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
    /// 保存历史到磁盘
    /// </summary>
    private void SaveHistoryToDisk(BattleHistoryData history)
    {
        var fileName = $"{history.ScopeType}_{history.StartedAt:yyyy-MM-dd_HH-mm-ss}.json";
        var filePath = Path.Combine(_historyDirectory, fileName);

        using (var stream = File.CreateText(filePath))
        using (var writer = new JsonTextWriter(stream))
        {
            var serializer = JsonSerializer.Create(_jsonSettings);
            serializer.Serialize(writer, history);
        }
        
        history.FilePath = filePath;
    }

    /// <summary>
    /// 从磁盘加载历史
    /// </summary>
    private void LoadHistory()
    {
        try
        {
            if (!Directory.Exists(_historyDirectory))
            {
                Directory.CreateDirectory(_historyDirectory);
                return;
            }

            var directoryInfo = new DirectoryInfo(_historyDirectory);
            var files = directoryInfo.EnumerateFiles("*.json")
                .OrderByDescending(f => f.CreationTime);

            foreach (var file in files)
            {
                try
                {
                    // Optimization: Check filename to skip deserializing if we already reached limit for that scope
                    var fileName = Path.GetFileNameWithoutExtension(file.Name);
                    var parts = fileName.Split('_');
                    bool skipLoad = false;
                    
                    if (parts.Length > 0 && Enum.TryParse<ScopeTime>(parts[0], out var scopeHint))
                    {
                        if (scopeHint == ScopeTime.Current && ScopeCurrentHistory.Count >= MaxHistory) skipLoad = true;
                        else if (scopeHint == ScopeTime.Total && ScopeTotalHistory.Count >= MaxHistory) skipLoad = true;
                    }

                    if (skipLoad)
                    {
                        try
                        {
                            file.Delete();
                            LogDeleteOldHistoryStartup(file.FullName);
                        }
                        catch { /* ignore */ }
                        continue;
                    }

                    BattleHistoryData? history;
                    using (var stream = file.OpenText())
                    using (var reader = new JsonTextReader(stream))
                    {
                        var serializer = JsonSerializer.Create(_jsonSettings);
                        history = serializer.Deserialize<BattleHistoryData>(reader);
                    }

                    if (history == null) continue;

                    history.FilePath = file.FullName;

                    var targetList = history.ScopeType == ScopeTime.Current ? ScopeCurrentHistory : ScopeTotalHistory;

                    if (targetList.Count < MaxHistory)
                    {
                        targetList.Add(HistoryInfo.FromHistory(history));
                    }
                    else
                    {
                        // ? 超出限制,删除文件并释放内存
                        try
                        {
                            file.Delete();
                            LogDeleteOldHistoryStartup(file.FullName);
                        }
                        catch { /* ignore */ }
                    }
                }
                catch (Exception ex)
                {
                    LogLoadHistoryFileException(ex, file.FullName);
                    // 损坏的文件直接删除
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            LogHistoryLoadComplete(ScopeCurrentHistory.Count, MaxHistory, ScopeTotalHistory.Count, MaxHistory);
        }
        catch (Exception ex)
        {
            LogLoadHistoryError(ex);
        }
    }

    /// <summary>
    /// 尝试删除历史文件
    /// </summary>
    private void TryDeleteHistoryFile(string filePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
                LogDeleteHistorySuccess(filePath);
            }
        }
        catch (Exception ex)
        {
            LogDeleteHistoryError(ex, filePath);
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "战斗时长不足{Min}秒({Actual:F1}秒),跳过保存{Scope}历史(硬性限制)")]
    private partial void LogSkipHistoryHardLimit(int min, double actual, string scope);

    [LoggerMessage(Level = LogLevel.Information, Message = "战斗时长不足用户设置的{UserMin}秒({Actual:F1}秒),跳过保存{Scope}历史(用户设置)")]
    private partial void LogSkipHistoryUserLimit(int userMin, double actual, string scope);

    [LoggerMessage(Level = LogLevel.Information, Message = "保存当前战斗历史成功: {StartedAt}, 时长: {Duration:F1}秒, 数据源: {Source}, 当前保存数量: {Count}/{Max}")]
    private partial void LogSaveCurrentHistorySuccess(DateTime startedAt, double duration, string source, int count, int max);

    [LoggerMessage(Level = LogLevel.Information, Message = "保存全程历史成功: {StartedAt}, 时长: {Duration:F1}秒, 当前保存数量: {Count}/{Max}")]
    private partial void LogSaveTotalHistorySuccess(DateTime startedAt, double duration, int count, int max);

    [LoggerMessage(Level = LogLevel.Debug, Message = "移除旧历史: {FilePath}, 文件已删除")]
    private partial void LogRemoveOldHistory(string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "保存当前战斗历史失败")]
    private partial void LogSaveCurrentHistoryError(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "保存全程历史失败")]
    private partial void LogSaveTotalHistoryError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "历史文件不存在: {FilePath}")]
    private partial void LogHistoryFileNotFound(string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "成功加载历史: {FilePath}, Duration:{historyDuration}")]
    private partial void LogHistoryLoadedSuccess(string filePath, TimeSpan historyDuration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "反序列化历史失败: {FilePath}")]
    private partial void LogHistoryDeserializationFailed(string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "加载历史失败: {FilePath}")]
    private partial void LogLoadHistoryFileError(Exception ex, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "启动时删除超出限制的旧历史文件: {FilePath}")]
    private partial void LogDeleteOldHistoryStartup(string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "加载历史文件失败: {FilePath}")]
    private partial void LogLoadHistoryFileException(Exception ex, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "加载历史完成: 当前={Current}/{MaxCurrent}, 全程={Total}/{MaxTotal}")]
    private partial void LogHistoryLoadComplete(int current, int maxCurrent, int total, int maxTotal);

    [LoggerMessage(Level = LogLevel.Error, Message = "加载历史失败")]
    private partial void LogLoadHistoryError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "成功删除历史文件: {FilePath}")]
    private partial void LogDeleteHistorySuccess(string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "删除历史文件失败: {FilePath}")]
    private partial void LogDeleteHistoryError(Exception ex, string filePath);

    #endregion
}

/// <summary>
/// 历史数据模型
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
    public required Dictionary<long, PlayerInfo> Players { get; set; } 
    public required Dictionary<long, PlayerStatistics> Statistics { get; set; } 

    /// <summary>
    /// 文件路径(不序列化)
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [JsonIgnore]
    public string FilePath { get; set; } = "";

    /// <summary>
    /// 显示标签
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [JsonIgnore]
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
