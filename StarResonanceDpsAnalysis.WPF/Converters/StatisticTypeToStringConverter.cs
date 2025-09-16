using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Converters;

internal class StatisticTypeToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is StatisticType type)
        {
            return type switch
            {
                StatisticType.Damage => "Damage",
                StatisticType.Healing => "Healing",
                StatisticType.TakenDamage => "Taken Damage",
                StatisticType.NpcTakenDamage => "NPC Taken Damage",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported StatisticType")
            };
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Does support convert string back to StatisticType");
    }
}

internal class ScopeTimeToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScopeTime type)
        {
            return type switch
            {
                ScopeTime.Current => "Current",
                ScopeTime.Total => "Total",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported ScopeTime")
            };
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Does support convert string back to StatisticType");
    }
}

[Obsolete("Not elegant method, do not use")]
internal class StatisticTypeNScopeTimeToStringMultiConverter : IMultiValueConverter
{
    public object? Convert(object?[]? values, Type targetType, object parameter, CultureInfo culture)
    {
        // Validate input
        if (values == null || values.Length == 0)
            return null;

        Debug.Assert(values.Length != 2);
        // Determine statistic part
        string statisticPart;
        if (values[0] is StatisticType statType)
        {
            statisticPart = statType switch
            {
                StatisticType.Damage => "Damage",
                StatisticType.Healing => "Healing",
                StatisticType.TakenDamage => "Taken Damage",
                _ => statType.ToString()
            };
        }
        else
        {
            statisticPart = values[0]?.ToString() ?? string.Empty;
        }

        // Determine scope part (if available)
        string? scopePart = null;
        if (values.Length > 1 && values[1] != null)
        {
            // If the application defines a ScopeTime enum, prefer its string value.
            if (values[1] is ScopeTime scope)
            {
                // Use the enum's name (e.g., "Current", "Overall"). Adjust mapping here if you need different display text.
                scopePart = scope.ToString();
            }
            else
            {
                scopePart = values[1]?.ToString();
            }
        }

        // Combine parts
        if (!string.IsNullOrEmpty(scopePart))
        {
            return $"{statisticPart} ({scopePart})";
        }

        return statisticPart;
    }

    public object[]? ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException(
            "ConvertBack is not supported for StatisticTypeNScopeTimeToStringMultiConverter");
    }
}