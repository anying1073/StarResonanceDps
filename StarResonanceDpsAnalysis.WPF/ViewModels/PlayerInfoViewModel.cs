using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Helpers;
using StarResonanceDpsAnalysis.WPF.Localization;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class PlayerInfoViewModel : BaseViewModel
{
    private readonly LocalizationManager _localizationManager;

    [ObservableProperty] private Classes _class = Classes.Unknown;

    /// <summary>
    /// 赛季强度 Season Strength
    /// </summary>
    [ObservableProperty] private int _seasonStrength;
    /// <summary>
    /// 赛季等级 Season Level
    /// </summary>
    [ObservableProperty] private int _seasonLevel;

    [ObservableProperty] private string _guild = string.Empty;
    [ObservableProperty] private bool _isNpc;
    [ObservableProperty] private string? _name;

    [ObservableProperty] private string _playerInfo = string.Empty;
    [ObservableProperty] private int _powerLevel;
    [ObservableProperty] private ClassSpec _spec = ClassSpec.Unknown;
    [ObservableProperty] private long _uid;
    [ObservableProperty] private bool _mask;
    [ObservableProperty] private int _npcTemplateId;

    /// <summary>
    /// 自定义格式字符串
    /// </summary>
    [ObservableProperty] private string _formatString = "{Name} - {Spec} ({PowerLevel}-{SeasonStrength})";

    /// <summary>
    /// 是否使用自定义格式
    /// </summary>
    [ObservableProperty] private bool _useCustomFormat = false;

    public PlayerInfoViewModel(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager;
        _localizationManager.CultureChanged += LocalizationManagerOnCultureChanged;
        PropertyChanged += OnPropertyChanged;
    }

    private void LocalizationManagerOnCultureChanged(object? sender, CultureInfo e)
    {
        UpdatePlayerInfo();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not ("PlayerInfo"))
        {
            UpdatePlayerInfo();
        }
    }

    private void UpdatePlayerInfo()
    {
        if (IsNpc)
        {
            PlayerInfo = _localizationManager.GetString($"JsonDictionary:Monster:{NpcTemplateId}", null, "UnknownMonster");
            return;
        }

        if (UseCustomFormat && !string.IsNullOrWhiteSpace(FormatString))
        {
            PlayerInfo = ApplyFormatString(FormatString);
            return;
        }

        // 原有逻辑: 使用字段可见性配置
        PlayerInfo = $"{GetName()} - {GetSpec()} ({PowerLevel}-S{SeasonStrength})";
    }

    /// <summary>
    /// 应用自定义格式字符串
    /// 支持占位符: {Name}, {Spec}, {PowerLevel}, {SeasonStrength}, {SeasonLevel}, {Guild}, {Uid}
    /// </summary>
    private string ApplyFormatString(string format)
    {
        var result = format;

        // 替换占位符
        result = Regex.Replace(result, @"\{Name\}", GetName(), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Spec\}", GetSpec(), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{PowerLevel\}", PowerLevel.ToString(), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{SeasonStrength\}", SeasonStrength.ToString(), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{SeasonLevel\}", SeasonLevel.ToString(), RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Guild\}", Guild ?? "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{Uid\}", Uid.ToString(), RegexOptions.IgnoreCase);

        // 清理多余的空格、括号等
        result = Regex.Replace(result, @"\s+", " "); // 多个空格变为一个
        result = Regex.Replace(result, @"\(\s*\)", ""); // 空括号
        result = Regex.Replace(result, @"\[\s*\]", ""); // 空方括号
        result = Regex.Replace(result, @"\s*-\s*-\s*", " - "); // 多个连字符
        result = Regex.Replace(result, @"^\s*-\s*|\s*-\s*$", ""); // 开头结尾的连字符
        result = result.Trim();

        return result;
    }

    private string GetName()
    {
        var hasName = !string.IsNullOrWhiteSpace(Name);
        var name = hasName switch
        {
            true => Mask ? NameMasker.Mask(Name!) : Name!,
            false => $"UID:{(Mask ? NameMasker.Mask(Uid.ToString()) : Uid.ToString())}",
        };
        Debug.Assert(name != null);
        return name;
    }

    private string GetSpec()
    {
        var rr = _localizationManager.GetString("ClassSpec_" + Spec);
        return rr;
    }
}