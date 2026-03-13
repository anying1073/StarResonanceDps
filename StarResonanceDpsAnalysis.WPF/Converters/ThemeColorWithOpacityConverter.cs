using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// 主题色 + 不透明度(0-100) -> Color
/// ただし透明ウィンドウのヒットテスト維持のため、alpha は最低 1 を残す
/// </summary>
public sealed class ThemeColorWithOpacityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var defaultColor = Color.FromRgb(186, 186, 186); // #BABABA

        var themeColor = defaultColor;
        if (values.Length > 0 && values[0] is string colorString && !string.IsNullOrWhiteSpace(colorString))
        {
            try
            {
                themeColor = (Color)ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                themeColor = defaultColor;
            }
        }

        double opacity = 100;
        if (values.Length > 1)
        {
            if (values[1] is double d)
                opacity = d;
            else if (values[1] is int i)
                opacity = i;
            else if (values[1] is string s && double.TryParse(s, NumberStyles.Any, culture, out var parsed))
                opacity = parsed;
        }

        opacity = Math.Clamp(opacity, 0, 100);

        // alpha 0 だと透明ウィンドウで D&D / hit test が死ぬことがあるので最低 1 を残す
        var alpha = opacity <= 0
            ? (byte)1
            : (byte)Math.Clamp(Math.Round(opacity / 100d * 255d), 1, 255);

        return Color.FromArgb(alpha, themeColor.R, themeColor.G, themeColor.B);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}