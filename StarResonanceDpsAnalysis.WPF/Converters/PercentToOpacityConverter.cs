using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public sealed class PercentToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return Math.Clamp(d / 100d, 0d, 1d);
        if (value is int i) return Math.Clamp(i / 100d, 0d, 1d);
        if (value is string s && double.TryParse(s, NumberStyles.Any, culture, out var parsed))
            return Math.Clamp(parsed / 100d, 0d, 1d);
        return 1d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return Math.Clamp(d * 100d, 0d, 100d);
        if (value is string s && double.TryParse(s, NumberStyles.Any, culture, out var parsed))
            return Math.Clamp(parsed * 100d, 0d, 100d);
        return DependencyProperty.UnsetValue;
    }
}

