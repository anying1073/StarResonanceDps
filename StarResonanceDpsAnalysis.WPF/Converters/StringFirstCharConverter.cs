using System.Globalization;
using System.Windows.Data;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converts a string to its first character.
/// Used for displaying skill name initials in circular badges.
/// </summary>
public sealed class StringFirstCharConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return str[0].ToString();
        }
        return "?";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
