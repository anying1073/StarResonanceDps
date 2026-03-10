using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Helpers;
using StarResonanceDpsAnalysis.WPF.Localization;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace StarResonanceDpsAnalysis.WPF.ViewModels;

public partial class PlayerInfoViewModel : BaseViewModel
{

    private readonly LocalizationManager _localizationManager;

    // NPC名（中国語）→ Classes_xxx の対応表（同じインデックスで対応）
    private static readonly string[] SpecialNpcChineseNames =
    [
        "冰魔导师",
        "巨刃守护者",
        "神射手",
        "神盾骑士",
        "灵魂乐手",
        "雷影剑士",
        "森语者",
        "青岚骑士"
    ];

    private static readonly string[] SpecialNpcResxKeys =
    [
        "Classes_FrostMage",
        "Classes_HeavyGuardian",
        "Classes_Marksman",
        "Classes_ShieldKnight",
        "Classes_SoulMusician",
        "Classes_Stormblade",
        "Classes_VerdantOracle",
        "Classes_WindKnight"
    ];

    private static readonly HashSet<string> SpecialNpcChineseNameSet =
    new(SpecialNpcChineseNames, StringComparer.Ordinal);

    internal static bool IsSpecialNpcChineseName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && SpecialNpcChineseNameSet.Contains(name);
    }

    [ObservableProperty] private Classes _class = Classes.Unknown;

    /// <summary>
    /// 自定义格式字符串
    /// </summary>
    [ObservableProperty] private string _formatString = "{Name} - {Spec} ({PowerLevel}-{SeasonStrength})";

    [ObservableProperty] private string _guild = string.Empty;
    [ObservableProperty] private bool _isNpc;
    [ObservableProperty] private bool _mask;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private int _npcTemplateId;

    [ObservableProperty] private string _playerInfo = string.Empty;
    [ObservableProperty] private int _powerLevel;

    /// <summary>
    /// 赛季等级 Season Level
    /// </summary>
    [ObservableProperty] private int _seasonLevel;

    /// <summary>
    /// 赛季强度 Season Strength
    /// </summary>
    [ObservableProperty] private int _seasonStrength;

    [ObservableProperty] private ClassSpec _spec = ClassSpec.Unknown;
    [ObservableProperty] private long _uid;

    /// <summary>
    /// 是否使用自定义格式
    /// </summary>
    [ObservableProperty] private bool _useCustomFormat;

    [ObservableProperty] private bool _forceNpcTakenDisplay;

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
        if (e.PropertyName is not "PlayerInfo")
        {
            UpdatePlayerInfo();
        }
    }

    private void UpdatePlayerInfo()
    {
        if (ForceNpcTakenDisplay || IsNpc)
        {
            var unknownNpcPrefix = _localizationManager.GetString("JsonDictionary:Monster:0", null, "UnknownMonster");
            PlayerInfo =
                _localizationManager.GetString($"JsonDictionary:Monster:{NpcTemplateId}", null, $"{unknownNpcPrefix}:{NpcTemplateId}");
            return;
        }

        if (UseCustomFormat && !string.IsNullOrWhiteSpace(FormatString))
        {
            PlayerInfo = ApplyFormatString(FormatString);
            return;
        }

        // 原有逻辑: 使用字段可见性配置
        PlayerInfo = $"{GetFinalName()} - {GetSpec()} ({PowerLevel}-S{SeasonStrength})";
    }

    /// <summary>
    /// 最終的に処理した名前（GetNameの結果）をベースに、
    /// 特定の中国語名なら Classes_xxx の resx キーで再翻訳する。
    /// </summary>
    private string GetFinalName()
    {
        var processedName = GetName();
        return TranslateSpecialNpcNameIfNeeded(processedName);
    }

    /// <summary>
    /// NPC名が特定の中国語クラス名なら、対応する Classes_xxx の resx キーに変換して現在言語で再翻訳
    /// 一致しなければ、そのまま返す
    /// </summary>
    private string TranslateSpecialNpcNameIfNeeded(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        for (var i = 0; i < SpecialNpcChineseNames.Length && i < SpecialNpcResxKeys.Length; i++)
        {
            if (name == SpecialNpcChineseNames[i])
            {
                // resxキーを現在言語で解決。キー未存在時は元の名前をそのまま返す
                return _localizationManager.GetString(SpecialNpcResxKeys[i], null, name);
            }
        }

        return name;
    }

    /// <summary>
    /// 应用自定义格式字符串
    /// 支持占位符: {Name}, {Spec}, {PowerLevel}, {SeasonStrength}, {SeasonLevel}, {Guild}, {Uid}
    /// </summary>
    private string ApplyFormatString(string format)
    {
        var result = format;

        // 替换占位符
        result = GetNameRegex().Replace(result, GetFinalName());
        result = GetSpecRegex().Replace(result, GetSpec());
        result = GetPowerLevelRegex().Replace(result, PowerLevel.ToString());
        result = GetSeasonStrengthRegex().Replace(result, SeasonStrength.ToString());
        result = GetSeasonLevelRegex().Replace(result, SeasonLevel.ToString());
        //result = GetGuildRegex().Replace(result, Guild);
        result = GetUidRegex().Replace(result, Uid.ToString());

        // 清理多余的空格、括号等
        result = GetCollapseWhitespaceRegex().Replace(result, " "); // 多个空格变为一个
        result = GetEmptyParenthesisRegex().Replace(result, ""); // 空括号
        result = GetEmptyBracketRegex().Replace(result, ""); // 空方括号
        result = GetRepeatedHyphensRegex().Replace(result, " - "); // 多个连字符
        result = GetLeadingOrTrailingHyphenRegex().Replace(result, ""); // 开头结尾的连字符
        result = result.Trim();

        return result;
    }

    private string GetName()
    {
        var hasName = !string.IsNullOrWhiteSpace(Name);

        if (hasName)
        {
            // 特殊NPC中国語名はマスク対象外
            if (IsSpecialNpcChineseName(Name))
            {
                return Name!;
            }

            return Mask ? NameMasker.Mask(Name!) : Name!;
        }

        return $"UID:{(Mask ? NameMasker.Mask(Uid.ToString()) : Uid.ToString())}";
    }

    private string GetSpec()
    {
        var rr = _localizationManager.GetString("ClassSpec_" + Spec);
        return rr;
    }

    #region Regex

    [GeneratedRegex(@"\{Name\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNameRegex();

    [GeneratedRegex(@"\{Spec\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSpecRegex();

    [GeneratedRegex(@"\{PowerLevel\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetPowerLevelRegex();

    [GeneratedRegex(@"\{SeasonStrength\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSeasonStrengthRegex();

    [GeneratedRegex(@"\{SeasonLevel\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetSeasonLevelRegex();

    [GeneratedRegex(@"\{Guild\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetGuildRegex();

    [GeneratedRegex(@"\{Uid\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetUidRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex GetCollapseWhitespaceRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex GetEmptyParenthesisRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex GetEmptyBracketRegex();

    [GeneratedRegex(@"\s*-\s*-\s*")]
    private static partial Regex GetRepeatedHyphensRegex();

    [GeneratedRegex(@"^\s*-\s*|\s*-\s*$")]
    private static partial Regex GetLeadingOrTrailingHyphenRegex();

    #endregion
}