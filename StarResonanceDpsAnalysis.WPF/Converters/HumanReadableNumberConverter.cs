using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StarResonanceDpsAnalysis.WPF.Models;
using StarResonanceDpsAnalysis.WPF.Helpers;
using StarResonanceDpsAnalysis.WPF.Localization;

namespace StarResonanceDpsAnalysis.WPF.Converters;

/// <summary>
/// Converts numeric values to compact human-readable strings.
/// </summary>
public class HumanReadableNumberConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 0)
        {
            return "0";
        }

        var mode = NumberDisplayMode.KMB;
        if (values.Length > 1 &&
            values[1] != null &&
            values[1] != DependencyProperty.UnsetValue &&
            values[1] != Binding.DoNothing)
        {
            mode = NumberFormatHelper.ParseDisplayMode(values[1], mode);
        }
        else if (parameter != null)
        {
            mode = NumberFormatHelper.ParseDisplayMode(parameter, mode);
        }

        var rawValue = values[0];

        if (rawValue == null ||
            rawValue == DependencyProperty.UnsetValue ||
            rawValue == Binding.DoNothing)
        {
            return NumberFormatHelper.FormatHumanReadable(0, mode, LocalizationManager.Instance.CurrentCulture);
        }

        if (!NumberFormatHelper.TryToDouble(rawValue, out var number))
        {
            return NumberFormatHelper.FormatHumanReadable(0, mode, LocalizationManager.Instance.CurrentCulture);
        }

        return NumberFormatHelper.FormatHumanReadable(number, mode, LocalizationManager.Instance.CurrentCulture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var mode = NumberFormatHelper.ParseDisplayMode(parameter);

        if (value == null ||
            value == DependencyProperty.UnsetValue ||
            value == Binding.DoNothing)
        {
            return NumberFormatHelper.FormatHumanReadable(0, mode, LocalizationManager.Instance.CurrentCulture);
        }

        if (!NumberFormatHelper.TryToDouble(value, out var number))
        {
            return NumberFormatHelper.FormatHumanReadable(0, mode, LocalizationManager.Instance.CurrentCulture);
        }

        return NumberFormatHelper.FormatHumanReadable(number, mode, LocalizationManager.Instance.CurrentCulture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}