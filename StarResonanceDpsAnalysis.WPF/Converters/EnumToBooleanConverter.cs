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
        if (value == null || parameter == null)
            return Binding.DoNothing;

        var boolValue = (bool)value;
        if (!boolValue)
            return Binding.DoNothing;

        var parameterString = parameter.ToString();
        if (string.IsNullOrEmpty(parameterString))
            return Binding.DoNothing;

        return Enum.Parse(targetType, parameterString, true);
    }
}
