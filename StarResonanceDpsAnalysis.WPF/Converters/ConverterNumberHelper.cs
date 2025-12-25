using System.Globalization;
using System.Windows;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Converters;

internal static class ConverterNumberHelper
{
    public static bool TryToDouble(object? input, out double value)
    {
        value = double.NaN;
        if (input == null || ReferenceEquals(input, DependencyProperty.UnsetValue))
        {
            return false;
        }

        switch (input)
        {
            case double d:
                value = d;
                return !double.IsNaN(d);
            case float f:
                value = f;
                return true;
            case decimal m:
                value = (double)m;
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            case short s:
                value = s;
                return true;
            case byte b:
                value = b;
                return true;
            case string str when double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            case IConvertible convertible:
                try
                {
                    value = convertible.ToDouble(CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    // ignored
                }

                break;
        }

        try
        {
            value = Convert.ToDouble(input, CultureInfo.InvariantCulture);
            return !double.IsNaN(value);
        }
        catch
        {
            value = double.NaN;
            return false;
        }
    }

    public static NumberDisplayMode ParseDisplayMode(object? source, NumberDisplayMode fallback = NumberDisplayMode.KMB)
    {
        if (source is null)
        {
            return fallback;
        }

        if (source is NumberDisplayMode mode)
        {
            return mode;
        }

        var text = source.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return string.Equals(text, "Wan", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, "万", StringComparison.OrdinalIgnoreCase)
            ? NumberDisplayMode.Wan
            : NumberDisplayMode.KMB;
    }

    public static string FormatHumanReadable<T>(T value, NumberDisplayMode mode, CultureInfo culture)
    {
        if (!TryToDouble(value, out var doubleValue))
        {
            return value?.ToString() ?? string.Empty;
        }

        return FormatHumanReadable(doubleValue, mode, culture);
    }

    public static string FormatHumanReadable(double value, NumberDisplayMode mode, CultureInfo culture)
    {
        var sign = value < 0 ? "-" : string.Empty;
        value = Math.Abs(value);

        if (mode == NumberDisplayMode.Wan)
        {
            if (value >= 100_000_000)
            {
                return sign + (value / 100_000_000d).ToString("0.##", culture) + "亿";
            }

            if (value >= 10_000)
            {
                return sign + (value / 10_000d).ToString("0.##", culture) + "万";
            }

            return sign + value.ToString("0.##", culture);
        }

        if (value >= 1_000_000_000)
        {
            return sign + (value / 1_000_000_000d).ToString("0.##", culture) + "B";
        }

        if (value >= 1_000_000)
        {
            return sign + (value / 1_000_000d).ToString("0.##", culture) + "M";
        }

        if (value >= 1_000)
        {
            return sign + (value / 1_000d).ToString("0.##", culture) + "K";
        }

        return sign + value.ToString("0.##", culture);
    }
}