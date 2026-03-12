using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using StarResonanceDpsAnalysis.WPF.Models;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public class StatisticTypeToColoredIconConverter : IValueConverter
{
    private readonly Dictionary<StatisticType, ImageSource?> _iconCache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StatisticType statisticType) return null;

        if (_iconCache.TryGetValue(statisticType, out var cached) && cached is not null)
            return cached;

        var app = Application.Current;
        var keyToTry = $"StatisticType{statisticType}Icon";

        var res = app?.TryFindResource(keyToTry);
        if (res is not ImageSource img) return null;

        _iconCache[statisticType] = img;
        return img;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(StatisticTypeToColoredIconConverter)} does not support ConvertBack.");
    }
}