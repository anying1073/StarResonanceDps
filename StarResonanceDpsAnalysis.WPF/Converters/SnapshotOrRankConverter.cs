using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StarResonanceDpsAnalysis.WPF.Localization;
using StarResonanceDpsAnalysis.WPF.Properties;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public sealed class SnapshotOrRankConverter : IMultiValueConverter
{
    public object? Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return null;
        }

        // 第一个参数: IsViewingSnapshot (是否在快照模式)
        var isSnapshot = values[0] is true;
        
        // 如果是快照模式,显示本地化标签
        if (isSnapshot)
        {
            return LocalizationManager.Instance.GetString(
                ResourcesKeys.DpsStatistics_Snapshot_Label,
                culture,
                "[Snapshot]");
        }

        // 第二个参数: CurrentPlayerRank (玩家排名字符串,格式已经是"[01]"或"[--]")
        if (values[1] == null || values[1] == DependencyProperty.UnsetValue)
        {
            return null;
        }

        // 战斗模式下,直接返回排名字符串(已经包含方括号)
        var rank = values[1]?.ToString();
        return rank;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
