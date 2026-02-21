using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Properties;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public sealed class HistoryOrRankConverter : IMultiValueConverter
{
    public object? Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return null;
        }

        // 第一个参数: IsViewingHistory (是否在快照模式)
        var isHistory = values[0] is true;
        
        // 如果是快照模式,显示本地化标签
        if (isHistory)
        {
            return LocalizationManager.Instance.GetString(
                ResourcesKeys.DpsStatistics_History_Label,
                culture,
                "History");
        }

        // 战斗模式下,直接返回排名字符串(已经包含方括号)
        var rank = values[1]?.ToString() ?? "--";
        return rank;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
