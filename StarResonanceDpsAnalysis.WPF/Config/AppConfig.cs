using System.Drawing;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.Core.Statistics;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.ViewModels;
using KeyBinding = StarResonanceDpsAnalysis.WPF.Models.KeyBinding;

namespace StarResonanceDpsAnalysis.WPF.Config;

/// <summary>
/// DPS数据更新模式
/// </summary>
public enum DpsUpdateMode
{
    /// <summary>
    /// 被动模式：基于事件触发更新
    /// </summary>
    Passive = 0,

    /// <summary>
    /// 主动模式：基于定时器定期更新
    /// </summary>
    Active = 1,
}

public enum UpdateSourceType
{
    GitHub = 0,
    SelfHosted = 1,
}

public enum BackgroundImageFitMode
{
    FitWidth = 0,     // 幅に合わせる
    FitToWindow = 1 // 画面のサイズに合わせる
}

/// <summary>
/// 应用配置类
/// 集成了配置管理器功能，支持INI文件持久化和属性变更通知
/// </summary>
public partial class AppConfig : ObservableObject
{
    /// <summary>
    /// 昵称
    /// </summary>
    [ObservableProperty]
    private string _nickname = string.Empty;

    [ObservableProperty]
    private ModifierKeys _testModifier = ModifierKeys.None;

    /// <summary>
    /// 职业
    /// </summary>
    [ObservableProperty]
    private Classes _classes;

    /// <summary>
    /// 用户UID
    /// </summary>
    [ObservableProperty]
    private long _uid;

    /// <summary>
    /// DPS伤害类型显示
    /// </summary>
    [ObservableProperty]
    private NumberDisplayMode _damageDisplayType;

    /// <summary>
    /// 战斗力
    /// </summary>
    [ObservableProperty]
    private int _combatPower;

    /// <summary>
    /// 战斗计时清除延迟（秒）
    /// </summary>
    [ObservableProperty]
    private int _combatTimeClearDelay = 5;

    /// <summary>
    /// 是否过图清空全程记录
    /// </summary>
    [ObservableProperty]
    private bool _clearLogAfterTeleport = true;

    /// <summary>
    /// 窗口不透明度 (0-100) 
    /// </summary>
    [ObservableProperty]
    private double _opacity = 100;

    /// <summary>
    /// items不透明度 (0-100) 
    /// </summary>
    [ObservableProperty]
    private double _itemOpacity = 100;

    /// <summary>
    /// 中央背景颜色透明度 (0-100)
    /// 默认值：30
    /// </summary>
    [ObservableProperty]
    private double _centerBackgroundOpacity = 30;

    /// <summary>
    /// 背景图片透明度 (0-100)
    /// 默认值：50
    /// </summary>
    [ObservableProperty]
    private double _backgroundImageOpacity = 50;

    /// <summary>
    /// 玩家信息打码<br/>
    /// Mask player info
    /// </summary>
    [ObservableProperty]
    private bool _maskPlayerName = true;

    /// <summary>
    /// DPS统计页面 - 是否隐藏顶部统计切换标签
    /// 默认值：false (显示)
    /// </summary>
    [ObservableProperty]
    private bool _hideStatisticTabs = false;

    /// <summary>
    /// 鼠标穿透开关 (WPF)
    /// </summary>
    [ObservableProperty]
    [JsonIgnore]
    private bool _mouseThroughEnabled;

    /// <summary>
    /// 是否使用浅色模式
    /// </summary>
    [ObservableProperty]
    private string _theme = "Light";

    /// <summary>
    /// 自定义职业颜色
    /// </summary>
    [ObservableProperty]
    private ObservableDictionary<Classes, string> _customClassColors = new();

    /// <summary>
    /// 当前界面语言（如 zh-CN、en-US、auto）
    /// </summary>
    [ObservableProperty]
    private Language _language = Language.Auto;

    /// <summary>
    /// 启动时的窗口状态
    /// </summary>
    [ObservableProperty]
    private Rectangle? _startUpState;

    /// <summary>
    /// 首选网络适配器
    /// </summary>
    [ObservableProperty]
    private NetworkAdapterInfo? _preferredNetworkAdapter;

    /// <summary>
    /// 鼠标穿透快捷键数据
    /// </summary>
    [ObservableProperty]
    private KeyBinding _mouseThroughShortcut = new(Key.F6, ModifierKeys.None);

    /// <summary>
    /// 置顶切换快捷键
    /// </summary>
    [ObservableProperty]
    private KeyBinding _topmostShortcut = new(Key.F7, ModifierKeys.None);

    /// <summary>
    /// 清空数据快捷键数据
    /// </summary>
    [ObservableProperty]
    private KeyBinding _clearDataShortcut = new(Key.F9, ModifierKeys.None);

    /// <summary>
    /// 当前窗口是否置顶
    /// </summary>
    [ObservableProperty]
    private bool _topmostEnabled;

    [ObservableProperty]
    private bool _debugEnabled = false;

    /// <summary>
    /// DPS数据更新模式（被动/主动）
    /// </summary>
    [ObservableProperty]
    private DpsUpdateMode _dpsUpdateMode = DpsUpdateMode.Passive;

    /// <summary>
    /// DPS数据主动更新间隔（毫秒），仅在主动模式下生效
    /// 默认值：1000ms (1秒)
    /// 范围：100ms - 5000ms
    /// </summary>
    [ObservableProperty]
    private int _dpsUpdateInterval = 1000;

    /// <summary>
    ///历史记录最大保存数量
    /// 默认值：15条
    /// 范围：5 - 50
    /// </summary>
    [ObservableProperty]
    private int _maxHistoryCount = 15;

    /// <summary>
    /// 打桩模式默认木桩类型
    /// 默认值：Center (中间木桩)
    /// </summary>
    [ObservableProperty]
    private DummyTargetType _defaultDummyTarget = DummyTargetType.Center; // 0=Center, 1=TDummy

    /// <summary>
    /// DPS统计页面 - 技能显示数量
    /// 默认值：8条
    /// </summary>
    [ObservableProperty]
    private int _skillDisplayLimit = 8;

    /// <summary>
    /// DPS统计页面 - 是否统计NPC数据
    /// 默认值：false (不统计NPC)
    /// </summary>
    [ObservableProperty]
    private bool _isIncludeNpcData = false;

    /// <summary>
    /// DPS统计页面 - 是否显示团队总伤
    /// 默认值：true (显示)
    /// </summary>
    [ObservableProperty]
    private bool _showTeamTotalDamage = true;

    /// <summary>
    /// DPS统计页面 - 是否显示总伤害数值
    /// 默认值：true
    /// </summary>
    [ObservableProperty]
    private bool _showDamage = true;

    /// <summary>
    /// DPS统计页面 - 是否显示DPS数值
    /// 默认值：true
    /// </summary>
    [ObservableProperty]
    private bool _showDps = true;

    /// <summary>
    /// DPS统计页面 - 是否启用并展示玩家信息滚动特效
    /// 默认值：true (启用)
    /// </summary>
    [ObservableProperty]
    private bool _enableMarqueeText = true;

    /// <summary>
    /// DPS统计页面 - 是否显示百分比
    /// 默认值：true
    /// </summary>
    [ObservableProperty]
    private bool _showPercentage = true;

    /// <summary>
    /// DPS统计页面 - 历史最小记录时长(秒)
    /// 默认值：5秒
    /// 范围：0 - 300秒
    /// </summary>
    [ObservableProperty]
    private int _minimalDurationInSeconds = 5;

    /// <summary>
    /// new PlayerStatistics path instead of legacy DpsData
    /// Default: true (use new architecture)
    /// </summary>
    [ObservableProperty]
    private bool _usePlayerStatisticsPath = true;

    /// <summary>
    /// 玩家信息自定义格式字符串
    /// 支持的占位符:
    /// {Name} - 玩家名称
    /// {Spec} - 职业流派
    /// {PowerLevel} - 战力
    /// {SeasonStrength} - 赛季强度
    /// {SeasonLevel} - 赛季等级
    /// {Guild} - 公会
    /// {Uid} - UID
    /// </summary>
    [ObservableProperty]
    private string _playerInfoFormatString = "{Name} - {Spec} ({PowerLevel}-{SeasonStrength})";

    /// <summary>
    /// 是否使用自定义格式字符串（false 则使用字段可见性配置）
    /// </summary>
    [ObservableProperty]
    private bool _useCustomFormat = false;

    /// <summary>
    /// Time series sample capacity for DPS/HPS/DTPS charts
    /// Default: 300 samples
    /// Range: 50 - 1000 samples (null for unlimited)
    /// </summary>
    [ObservableProperty]
    private int _timeSeriesSampleCapacity = 300;

    /// <summary>
    /// 顶部栏和底部栏主题颜色
    /// 默认值：#1690F8
    /// </summary>
    [ObservableProperty]
    private string _themeColor = "#1690F8";

    /// <summary>
    /// 中央背景颜色
    /// 默认值：#2F2F2F
    /// </summary>
    [ObservableProperty]
    private string _centerBackgroundColor = "#2F2F2F";

    /// <summary>
    /// 背景图片路径
    /// </summary>
    [ObservableProperty]
    [property: Newtonsoft.Json.JsonConverter(typeof(Converters.JsonEmptyStringToNullConverter))]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private BackgroundImageFitMode _backgroundImageFitMode = BackgroundImageFitMode.FitWidth;

    /// <summary>
    /// DPS显示计算模式
    /// true = 使用转换器计算 (value/duration)
    /// false = 使用预计算值 (ValuePerSecond)
    /// 默认值：true (转换器模式)
    /// </summary>
    [ObservableProperty]
    private bool _useConverterBasedDps = true;

    [ObservableProperty]
    private int _trainingDuration = 180; // Default training duration in seconds (3 minutes)

    [ObservableProperty]
    private bool _enableAutoUpdate = true;

    [ObservableProperty]
    private bool _autoUpdateCheckOnStartup = true;

    [ObservableProperty]
    private UpdateSourceType _updateSource = UpdateSourceType.GitHub;

    [ObservableProperty]
    private string _githubRepository = "anying1073/StarResonanceDps";

    [ObservableProperty]
    private bool _githubIncludePrerelease;

    [ObservableProperty]
    private string _githubAssetNameContains = "WPF";

    [ObservableProperty]
    private string _selfHostedManifestUrl = string.Empty;

    [ObservableProperty]
    private int _updateRequestTimeoutSeconds = 10;

    public bool UseProcessPortsFilter { get; set; }

    partial void OnBackgroundImagePathChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _backgroundImagePath = null;
        }
    }

    partial void OnTimeSeriesSampleCapacityChanged(int value)
    {
        StatisticsConfiguration.TimeSeriesSampleCapacity = value;
    }

    public AppConfig Clone()
    {
        var json = JsonConvert.SerializeObject(this);
        return JsonConvert.DeserializeObject<AppConfig>(json)!;
    }
}