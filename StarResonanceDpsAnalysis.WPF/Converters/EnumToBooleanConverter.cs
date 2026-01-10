using System;
using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converts an enum value to a boolean based on a parameter match
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        return string.Equals(enumValue, targetValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter?.ToString() is not { } param)
            return Binding.DoNothing;

        var targetEnum = Enum.Parse(targetType, param, true);
        if (boolValue) return targetEnum;

        var values = Enum.GetValues(targetType);
        return values.Length == 2 
            ? values.Cast<object>().First(v => !v.Equals(targetEnum)) 
            : Binding.DoNothing;
    }
}
