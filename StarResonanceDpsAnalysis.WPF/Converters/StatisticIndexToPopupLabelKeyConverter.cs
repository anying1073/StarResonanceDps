using System;
using System.Globalization;
using System.Windows.Data;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public class StatisticIndexToPopupLabelKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var kind = parameter as string ?? string.Empty;

        StatisticType statisticType = value switch
        {
            StatisticType st => st,
            int i when Enum.IsDefined(typeof(StatisticType), i) => (StatisticType)i,
            _ => StatisticType.Damage
        };

        return kind switch
        {
            "Total" => value switch
            {
                StatisticType.Damage => "DpsDetail_Popup_TotalDamage",   // Damage
                StatisticType.Healing => "DpsDetail_Popup_TotalHealing",  // Healing
                StatisticType.TakenDamage => "DpsDetail_Popup_TotalDamageTaken",    // Taken
                StatisticType.NpcTakenDamage => "DpsDetail_Popup_TotalDamageTaken",    // Taken
                _ => "DpsDetail_Popup_TotalDamage"
            },

            "HitCount" => value switch
            {
                StatisticType.Damage => "DpsDetail_Popup_HitCount",      // Damage
                StatisticType.Healing => "DpsDetail_Popup_HealCount",  // Healing
                StatisticType.TakenDamage => "DpsDetail_Popup_DamageTakenCount",    // Taken
                StatisticType.NpcTakenDamage => "DpsDetail_Popup_DamageTakenCount",    // Taken
                _ => "DpsDetail_Popup_HitCount"
            },

            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
