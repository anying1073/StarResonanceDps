using System.Drawing;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Models;
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
    Active = 1
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
    private bool _clearLogAfterTeleport;

    /// <summary>
    /// 不透明度 (0-100) (有效范围: 5-95), 默认95, 0为全透明(会影响鼠标交互)
    /// </summary>
    [ObservableProperty]
    private double _opacity = 100;

    /// <summary>
    /// 玩家名脱敏
    /// </summary>
    [ObservableProperty]
    private bool _maskPlayerName = true;

    /// <summary>
    /// 鼠标穿透开关 (WPF)
    /// </summary>
    [ObservableProperty]
    private bool _mouseThroughEnabled;

    /// <summary>
    /// 是否使用浅色模式
    /// </summary>
    [ObservableProperty]
    private string _theme = "Light";

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
    /// 历史记录最大保存数量
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
    private int _defaultDummyTarget = 0; // 0=Center, 1=TDummy

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
    /// DPS统计页面 - 快照最小记录时长(秒)
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
    /// ⭐ 新增: 窗口主题颜色（顶部栏和底部栏）
    /// 默认值：#1690F8 (蓝色)
    /// </summary>
    [ObservableProperty]
    private string _themeColor = "#1690F8";

    /// <summary>
    /// ⭐ 新增: 背景图片路径
    /// 只支持PNG格式
    /// </summary>
    [ObservableProperty]
    private string _backgroundImagePath = "";

    public AppConfig Clone()
    {
        // TODO: Add unittest
        var json = JsonConvert.SerializeObject(this);
        return JsonConvert.DeserializeObject<AppConfig>(json)!;
    }
}
