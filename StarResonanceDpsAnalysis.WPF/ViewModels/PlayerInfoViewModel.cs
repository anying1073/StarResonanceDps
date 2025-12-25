using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
        if (e.PropertyName != "PlayerInfo")
        {
            UpdatePlayerInfo();
        }
    }

    private void UpdatePlayerInfo()
    {
        PlayerInfo = IsNpc
            ? _localizationManager.GetString($"JsonDictionary:Monster:{NpcTemplateId}", null, "UnknownMonster")
            : $"{GetName()} - {GetSpec()} ({PowerLevel}-{SeasonStrength})";

        return;

        string GetName()
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

        string GetSpec()
        {
            var rr = _localizationManager.GetString("ClassSpec_" + Spec);
            return rr;
        }
    }
}